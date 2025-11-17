// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 路由服务 - 根据用户意图选择最合适的 server 和 tool
/// </summary>
public class McpRoutingService
{
    private readonly McpDiscoveryService _discoveryService;
    private readonly McpAIService _aiService;
    private readonly McpScoringService _scoringService;
    private readonly ILogger<McpRoutingService>? _logger;

    public McpRoutingService(
        McpDiscoveryService discoveryService, 
        ILogger<McpRoutingService>? logger = null, 
        IChatClient? chatClient = null)
    {
        _discoveryService = discoveryService;
        _logger = logger;
        _aiService = new McpAIService(chatClient, logger);
        _scoringService = new McpScoringService(logger);
    }

    /// <summary>
    /// 使用多步骤AI决策流程根据用户查询找到最佳的 server 和 tool
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<RoutingDecision?> RouteQueryAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return null;
        }

        _logger?.LogInformation($"🔍 Starting routing for: '{userQuery}'");

        var servers = _discoveryService.GetConnectedServers();
        if (!servers.Any())
        {
            _logger?.LogWarning("❌ No servers available for routing");
            return null;
        }

        // 使用AI模型进行多步骤决策，如果失败则降级到关键词匹配
        if (_aiService.HasAIClient)
        {
            var aiResult = await RouteWithMultiStepAIAsync(userQuery, servers);
            if (aiResult != null)
            {
                return aiResult;
            }
        }

        _logger?.LogWarning("⚠️ AI routing failed or unavailable, falling back to keyword matching");
        return await RouteWithKeywordsAsync(userQuery, servers);
    }

    /// <summary>
    /// 使用多步骤AI决策进行智能路由
    /// </summary>
    private async Task<RoutingDecision?> RouteWithMultiStepAIAsync(string userQuery, List<McpServerInfo> servers)
    {
        try
        {
            // 步骤1: 意图识别
            _logger?.LogInformation("🎯 Step 1: Intent Classification");
            var intentResult = await _aiService.ClassifyIntentAsync(userQuery);
            if (!intentResult.Success || intentResult.Result == null)
            {
                _logger?.LogWarning("❌ Failed to classify user intent");
                return null;
            }

            var intent = intentResult.Result;
            _logger?.LogInformation($"📊 Intent: needTool={intent.NeedTool}, topic={intent.Topic}, confidence={intent.Confidence:F2}");

            if (!intent.NeedTool)
            {
                _logger?.LogInformation("ℹ️ AI determined no tool is needed for this query");
                return null;
            }

            // 步骤2: 服务器选择
            _logger?.LogInformation("🖥️ Step 2: Server Selection");
            var serverResult = await _aiService.SelectServerAsync(userQuery, servers, intent);
            if (!serverResult.Success || serverResult.Result == null)
            {
                _logger?.LogWarning("❌ Failed to select appropriate server");
                return null;
            }

            var selectedServer = servers.FirstOrDefault(s => s.Id == serverResult.Result.ChosenServerId);
            if (selectedServer == null)
            {
                _logger?.LogWarning($"❌ Selected server not found: {serverResult.Result.ChosenServerId}");
                return null;
            }

            _logger?.LogInformation($"🏆 Selected server: {selectedServer.Name} (confidence: {serverResult.Confidence:F2})");

            // 步骤3: 工具选择
            _logger?.LogInformation("🔧 Step 3: Tool Selection");
            var availableTools = _discoveryService.GetServerTools(selectedServer.Id);
            var toolResult = await _aiService.SelectToolAsync(userQuery, selectedServer, availableTools, intent);
            if (!toolResult.Success || toolResult.Result == null)
            {
                _logger?.LogWarning("❌ Failed to select appropriate tool");
                return null;
            }

            var selectedTool = availableTools.FirstOrDefault(t => t.Name == toolResult.Result.ChosenToolName);
            if (selectedTool == null)
            {
                _logger?.LogWarning($"❌ Selected tool not found: {toolResult.Result.ChosenToolName}");
                return null;
            }

            _logger?.LogInformation($"⚙️ Selected tool: {selectedTool.Name} (confidence: {toolResult.Confidence:F2})");

            // 步骤4: 参数提取
            var argumentsResult = await ExtractArgumentsForToolAsync(userQuery, selectedTool, intent);
            if (argumentsResult == null)
            {
                return null;
            }

            // 设置选择的服务器
            argumentsResult.SelectedServer = selectedServer;

            // 检查是否需要澄清
            if (argumentsResult.RequiresClarification)
            {
                return argumentsResult;
            }

            // 步骤5: 生成工具调用计划
            _logger?.LogInformation("📋 Step 5: Tool Invocation Planning");
            var planResult = await _aiService.CreateInvocationPlanAsync(userQuery, selectedServer, selectedTool, argumentsResult.Parameters);
            
            var overallConfidence = Math.Min(
                Math.Min(intentResult.Confidence, serverResult.Confidence),
                Math.Min(toolResult.Confidence, argumentsResult.Confidence));

            _logger?.LogInformation($"✅ Multi-step AI routing completed successfully");

            return new RoutingDecision
            {
                SelectedServer = selectedServer,
                SelectedTool = selectedTool,
                Parameters = argumentsResult.Parameters,
                Confidence = overallConfidence,
                Reasoning = $"AI多步骤决策: 意图={intent.Topic}, 服务器={selectedServer.Name}, 工具={selectedTool.Name}",
                RequiresClarification = false,
                InvocationPlan = planResult.Result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Error during multi-step AI routing");
            return null;
        }
    }

    /// <summary>
    /// 处理工具参数提取，包括检查是否需要澄清
    /// </summary>
    private async Task<RoutingDecision?> ExtractArgumentsForToolAsync(string userQuery, McpToolInfo selectedTool, IntentClassificationResponse intent)
    {
        _logger?.LogInformation("📝 Step 4: Argument Extraction");
        
        // 检查工具是否需要参数
        _logger?.LogInformation($"🔍 Checking if tool needs parameters...");
        _logger?.LogInformation($"📋 InputSchema is null: {selectedTool.InputSchema is null}");

        if (selectedTool.InputSchema is not null
            && selectedTool.InputSchema.TryGetValue("properties", out var props)
            && HasValidProperties(props))
        {
            var argumentResult = await _aiService.ExtractArgumentsAsync(userQuery, selectedTool, intent);
            if (!argumentResult.Success || argumentResult.Result == null)
            {
                _logger?.LogWarning("❌ Failed to extract arguments");
                return null;
            }

            // 检查是否有缺失参数需要用户澄清
            if (argumentResult.Result.Missing.Any())
            {
                _logger?.LogInformation($"❓ Missing parameters: {string.Join(", ", argumentResult.Result.Missing)}");
                return new RoutingDecision
                {
                    SelectedServer = null!,
                    SelectedTool = selectedTool,
                    Parameters = argumentResult.Result.Arguments,
                    Confidence = argumentResult.Confidence,
                    Reasoning = $"需要澄清: {argumentResult.Result.ClarifyQuestion}",
                    RequiresClarification = true,
                    ClarificationQuestion = argumentResult.Result.ClarifyQuestion
                };
            }

            return new RoutingDecision
            {
                SelectedServer = null!,
                SelectedTool = selectedTool,
                Parameters = argumentResult.Result.Arguments,
                Confidence = argumentResult.Confidence,
                RequiresClarification = false
            };
        }
        else
        {
            _logger?.LogInformation("ℹ️ Selected tool has no input parameters to extract");
            return new RoutingDecision
            {
                SelectedServer = null!, // 将在调用方设置
                SelectedTool = selectedTool,
                Parameters = new Dictionary<string, object>(),
                Confidence = 1.0,
                RequiresClarification = false
            };
        }
    }

    /// <summary>
    /// 检查 properties 对象是否有效（不为空且包含至少一个属性）
    /// </summary>
    private bool HasValidProperties(object props)
    {
        try
        {
            // 尝试不同的类型转换
            if (props is JsonElement elem)
            {
                _logger?.LogInformation("📋 Properties is JsonElement");
                return elem.ValueKind == JsonValueKind.Object && elem.EnumerateObject().MoveNext();
            }
            else if (props is Dictionary<string, object> dict)
            {
                _logger?.LogInformation($"📋 Properties is Dictionary with {dict.Count} items");
                return dict.Count > 0;
            }
            else if (props is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
            {
                _logger?.LogInformation("📋 Properties is JSON string, attempting to parse");
                var parsed = JsonDocument.Parse(jsonString);
                return parsed.RootElement.ValueKind == JsonValueKind.Object 
                    && parsed.RootElement.EnumerateObject().MoveNext();
            }
            else
            {
                _logger?.LogInformation($"📋 Properties type not supported: {props?.GetType()?.Name ?? "null"}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"❌ Error checking properties: {ex.Message}");
            return false;
        }
    }



    /// <summary>
    /// 降级方案：使用关键词匹配进行路由
    /// </summary>
    private Task<RoutingDecision?> RouteWithKeywordsAsync(string userQuery, List<McpServerInfo> servers)
    {
        var query = userQuery.ToLowerInvariant();
        var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score)>();

        foreach (var server in servers)
        {
            var tools = _discoveryService.GetServerTools(server.Id);
            foreach (var tool in tools)
            {
                var score = _scoringService.CalculateSimpleMatchScore(query, server, tool);
                if (score > 0)
                {
                    candidates.Add((server, tool, score));
                }
            }
        }

        if (!candidates.Any())
        {
            return Task.FromResult<RoutingDecision?>(null);
        }

        var best = candidates.OrderByDescending(c => c.score).First();
        _logger?.LogInformation($"✅ Keyword matching selected: {best.server.Name}.{best.tool.Name}");

        return Task.FromResult<RoutingDecision?>(new RoutingDecision
        {
            SelectedServer = best.server,
            SelectedTool = best.tool,
            Parameters = _scoringService.ExtractParameters(userQuery, _scoringService.AnalyzeUserIntent(userQuery), best.tool),
            Confidence = best.score / 100.0, // 标准化到0-1
            Reasoning = "Keyword-based fallback selection"
        });
    }



    /// <summary>
    /// 获取候选的 server-tool 组合用于调试
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task<List<(McpServerInfo server, McpToolInfo tool, double score)>> GetRoutingCandidatesAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return Task.FromResult<List<(McpServerInfo server, McpToolInfo tool, double score)>>(new List<(McpServerInfo, McpToolInfo, double)>());
        }

        var intent = _scoringService.AnalyzeUserIntent(userQuery);
        var servers = _discoveryService.GetConnectedServers();
        var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score)>();

        foreach (var server in servers)
        {
            var serverTools = _discoveryService.GetServerTools(server.Id);
            foreach (var tool in serverTools)
            {
                var (score, reasoning) = _scoringService.CalculateMatchScore(userQuery, intent, server, tool);
                candidates.Add((server, tool, score));
            }
        }

        return Task.FromResult(candidates.OrderByDescending(c => c.score).ToList());
    }
}