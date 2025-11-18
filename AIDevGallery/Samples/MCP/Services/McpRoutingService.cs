// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private readonly McpAIDecisionEngine _aiDecisionEngine;
    private readonly McpScoringService _scoringService;
    private readonly ILogger<McpRoutingService>? _logger;

    public McpRoutingService(
        McpDiscoveryService discoveryService,
        ILogger<McpRoutingService>? logger = null,
        IChatClient? chatClient = null)
    {
        _discoveryService = discoveryService;
        _logger = logger;
        _aiDecisionEngine = new McpAIDecisionEngine(chatClient, logger);
        _scoringService = new McpScoringService(logger);
    }

    /// <summary>
    /// 使用多步骤AI决策流程根据用户查询找到最佳的 server 和 tool
    /// </summary>
    /// <param name="userQuery">用户查询内容</param>
    /// <param name="thinkAreaCallback">用于更新思考区域内容的回调函数</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<RoutingDecision?> RouteQueryAsync(string userQuery, Action<string>? thinkAreaCallback = null)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return null;
        }

        var message = $"🔍 Starting routing for: '{userQuery}'";
        _logger?.LogInformation(message);

        var servers = _discoveryService.GetConnectedServers();
        if (!servers.Any())
        {
            var noServerMessage = "❌ No servers available for routing";
            _logger?.LogWarning(noServerMessage);
            thinkAreaCallback?.Invoke(noServerMessage);
            return null;
        }

        // 使用AI模型进行多步骤决策，如果失败则降级到关键词匹配
        if (_aiDecisionEngine.HasAIClient)
        {
            var aiResult = await RouteWithMultiStepAIAsync(userQuery, servers, thinkAreaCallback);
            if (aiResult != null)
            {
                return aiResult;
            }
        }

        var fallbackMessage = "⚠️ AI routing failed or unavailable, falling back to keyword matching";
        _logger?.LogWarning(fallbackMessage);
        return await RouteWithKeywordsAsync(userQuery, servers, thinkAreaCallback);
    }

    /// <summary>
    /// 使用多步骤AI决策进行智能路由
    /// </summary>
    [RequiresDynamicCode("Calls AIDevGallery.Samples.MCP.Services.McpAIDecisionEngine.CreateInvocationPlanAsync(String, McpServerInfo, McpToolInfo, Dictionary<String, Object>)")]
    private async Task<RoutingDecision?> RouteWithMultiStepAIAsync(string userQuery, List<McpServerInfo> servers, Action<string>? thinkAreaCallback = null)
    {
        try
        {
            // 步骤1: 意图识别
            var step1Message = "🎯 Step 1: Intent Classification";
            _logger?.LogInformation(step1Message);
            thinkAreaCallback?.Invoke(step1Message);
            
            var intentResult = await _aiDecisionEngine.ClassifyIntentAsync(userQuery);
            if (!intentResult.Success || intentResult.Result == null)
            {
                var failMessage = "❌ Failed to classify user intent";
                _logger?.LogWarning(failMessage);
                thinkAreaCallback?.Invoke(failMessage);
                return null;
            }

            var intent = intentResult.Result;
            var intentMessage = $"📊 Intent: needTool={intent.NeedTool}, topic={intent.Topic}, confidence={intent.Confidence:F2}";
            _logger?.LogInformation(intentMessage);
            thinkAreaCallback?.Invoke(intentMessage);

            if (!intent.NeedTool)
            {
                var noToolMessage = "ℹ️ AI determined no tool is needed for this query";
                _logger?.LogInformation(noToolMessage);
                thinkAreaCallback?.Invoke(noToolMessage);
                return null;
            }

            // 步骤2: 服务器选择
            var step2Message = "🖥️ Step 2: Server Selection";
            _logger?.LogInformation(step2Message);
            thinkAreaCallback?.Invoke(step2Message);
            
            var serverResult = await _aiDecisionEngine.SelectServerAsync(userQuery, servers, intent);
            if (!serverResult.Success || serverResult.Result == null)
            {
                var serverFailMessage = "❌ Failed to select appropriate server";
                _logger?.LogWarning(serverFailMessage);
                thinkAreaCallback?.Invoke(serverFailMessage);
                return null;
            }

            var selectedServer = servers.FirstOrDefault(s => s.Id == serverResult.Result.ChosenServerId);
            if (selectedServer == null)
            {
                var notFoundMessage = $"❌ Selected server not found: {serverResult.Result.ChosenServerId}";
                _logger?.LogWarning(notFoundMessage);
                thinkAreaCallback?.Invoke(notFoundMessage);
                return null;
            }

            var serverSelectedMessage = $"🏆 Selected server: {selectedServer.Name} (confidence: {serverResult.Confidence:F2})";
            _logger?.LogInformation(serverSelectedMessage);
            thinkAreaCallback?.Invoke(serverSelectedMessage);

            // 步骤3: 工具选择
            var step3Message = "🔧 Step 3: Tool Selection";
            _logger?.LogInformation(step3Message);
            thinkAreaCallback?.Invoke(step3Message);
            
            var availableTools = _discoveryService.GetServerTools(selectedServer.Id);
            var toolResult = await _aiDecisionEngine.SelectToolAsync(userQuery, selectedServer, availableTools, intent);
            if (!toolResult.Success || toolResult.Result == null)
            {
                var toolFailMessage = "❌ Failed to select appropriate tool";
                _logger?.LogWarning(toolFailMessage);
                thinkAreaCallback?.Invoke(toolFailMessage);
                return null;
            }

            var selectedTool = availableTools.FirstOrDefault(t => t.Name == toolResult.Result.ChosenToolName);
            if (selectedTool == null)
            {
                var toolNotFoundMessage = $"❌ Selected tool not found: {toolResult.Result.ChosenToolName}";
                _logger?.LogWarning(toolNotFoundMessage);
                thinkAreaCallback?.Invoke(toolNotFoundMessage);
                return null;
            }

            var toolSelectedMessage = $"⚙️ Selected tool: {selectedTool.Name} (confidence: {toolResult.Confidence:F2})";
            _logger?.LogInformation(toolSelectedMessage);
            thinkAreaCallback?.Invoke(toolSelectedMessage);

            // 步骤4: 参数提取
            var argumentsResult = await ExtractArgumentsForToolAsync(userQuery, selectedTool, intent, thinkAreaCallback);
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
            var step5Message = "📋 Step 5: Tool Invocation Planning";
            _logger?.LogInformation(step5Message);
            thinkAreaCallback?.Invoke(step5Message);
            
            var planResult = await _aiDecisionEngine.CreateInvocationPlanAsync(userQuery, selectedServer, selectedTool, argumentsResult.Parameters);

            var overallConfidence = Math.Min(
                Math.Min(intentResult.Confidence, serverResult.Confidence),
                Math.Min(toolResult.Confidence, argumentsResult.Confidence));

            var completedMessage = "✅ Multi-step AI routing completed successfully";
            _logger?.LogInformation(completedMessage);
            thinkAreaCallback?.Invoke(completedMessage);

            return new RoutingDecision
            {
                SelectedServer = selectedServer,
                SelectedTool = selectedTool,
                Parameters = argumentsResult.Parameters,
                Confidence = overallConfidence,
                Reasoning = $"AI multi-step decision: intent={intent.Topic}, server={selectedServer.Name}, tool={selectedTool.Name}",
                RequiresClarification = false,
                InvocationPlan = planResult.Result
            };
        }
        catch (Exception ex)
        {
            var errorMessage = "❌ Error during multi-step AI routing";
            _logger?.LogError(ex, errorMessage);
            thinkAreaCallback?.Invoke($"{errorMessage}");
            return null;
        }
    }

    /// <summary>
    /// 处理工具参数提取，包括检查是否需要澄清
    /// </summary>
    private async Task<RoutingDecision?> ExtractArgumentsForToolAsync(string userQuery, McpToolInfo selectedTool, IntentClassificationResponse intent, Action<string>? thinkAreaCallback = null)
    {
        var step4Message = "📝 Step 4: Argument Extraction";
        _logger?.LogInformation(step4Message);
        thinkAreaCallback?.Invoke(step4Message);

        // 检查工具是否需要参数
        var checkMessage = "🔍 Checking if tool needs parameters...";
        _logger?.LogInformation(checkMessage);
        
        var schemaMessage = $"📋 InputSchema is null: {selectedTool.InputSchema is null}";
        _logger?.LogInformation(schemaMessage);

        if (selectedTool.InputSchema is not null
            && selectedTool.InputSchema.TryGetValue("properties", out var props)
            && HasValidProperties(props, thinkAreaCallback))
        {
            var argumentResult = await _aiDecisionEngine.ExtractArgumentsAsync(userQuery, selectedTool, intent);
            
            if (!argumentResult.Success || argumentResult.Result == null)
            {
                var extractFailMessage = "❌ Failed to extract arguments";
                _logger?.LogWarning(extractFailMessage);
                thinkAreaCallback?.Invoke(extractFailMessage);
                return null;
            }

            // 检查是否有缺失参数需要用户澄清
            if (argumentResult.Result.Missing.Any())
            {
                var missingMessage = $"❓ Missing parameters: {string.Join(", ", argumentResult.Result.Missing)}";
                _logger?.LogInformation(missingMessage);
                return new RoutingDecision
                {
                    SelectedServer = null!,
                    SelectedTool = selectedTool,
                    Parameters = argumentResult.Result.Arguments,
                    Confidence = argumentResult.Confidence,
                    Reasoning = $"Need clarification: {argumentResult.Result.ClarifyQuestion}",
                    RequiresClarification = true,
                    ClarificationQuestion = argumentResult.Result.ClarifyQuestion
                };
            }
            thinkAreaCallback?.Invoke(JsonSerializer.Serialize(argumentResult.Result.Arguments));
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
            var noParamsMessage = "ℹ️ Selected tool has no input parameters to extract";
            _logger?.LogInformation(noParamsMessage);
            thinkAreaCallback?.Invoke(noParamsMessage);
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
    private bool HasValidProperties(object props, Action<string>? thinkAreaCallback = null)
    {
        try
        {
            // 尝试不同的类型转换
            if (props is JsonElement elem)
            {
                var elemMessage = "📋 Properties is JsonElement";
                _logger?.LogInformation(elemMessage);
                return elem.ValueKind == JsonValueKind.Object && elem.EnumerateObject().MoveNext();
            }
            else if (props is Dictionary<string, object> dict)
            {
                var dictMessage = $"📋 Properties is Dictionary with {dict.Count} items";
                _logger?.LogInformation(dictMessage);
                return dict.Count > 0;
            }
            else if (props is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
            {
                var jsonMessage = "📋 Properties is JSON string, attempting to parse";
                _logger?.LogInformation(jsonMessage);
                var parsed = JsonDocument.Parse(jsonString);
                return parsed.RootElement.ValueKind == JsonValueKind.Object
                    && parsed.RootElement.EnumerateObject().MoveNext();
            }
            else
            {
                var unsupportedMessage = $"📋 Properties type not supported: {props?.GetType()?.Name ?? "null"}";
                _logger?.LogInformation(unsupportedMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ Error checking properties: {ex.Message}";
            _logger?.LogWarning(errorMessage);
            thinkAreaCallback?.Invoke(errorMessage);
            return false;
        }
    }

    /// <summary>
    /// 降级方案：使用关键词匹配进行路由
    /// </summary>
    private Task<RoutingDecision?> RouteWithKeywordsAsync(string userQuery, List<McpServerInfo> servers, Action<string>? thinkAreaCallback = null)
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
        var keywordMessage = $"✅ Keyword matching selected: {best.server.Name}.{best.tool.Name}";
        _logger?.LogInformation(keywordMessage);
        thinkAreaCallback?.Invoke(keywordMessage);

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