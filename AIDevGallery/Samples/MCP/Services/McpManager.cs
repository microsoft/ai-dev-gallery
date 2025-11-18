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
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 管理器 - 协调发现、路由、调用和 LLM 集成
/// </summary>
public class McpManager : IDisposable
{
    private readonly McpDiscoveryService _discoveryService;
    private readonly McpRoutingService _routingService;
    private readonly McpInvocationService _invocationService;
    private readonly ILogger<McpManager>? _logger;
    private bool _initialized;
    private bool _disposed;

    public McpManager(ILogger<McpManager>? logger = null, IChatClient? chatClient = null)
    {
        _logger = logger;

        // 为每个服务创建专用的 logger，如果需要的话
        // 这里我们传递一个通用的 logger 实现
        var loggerFactory = logger != null ? new WrappedLoggerFactory(logger) : null;

        _discoveryService = new McpDiscoveryService(loggerFactory?.CreateLogger<McpDiscoveryService>());
        _routingService = new McpRoutingService(_discoveryService, loggerFactory?.CreateLogger<McpRoutingService>(), chatClient);
        _invocationService = new McpInvocationService(_discoveryService, loggerFactory?.CreateLogger<McpInvocationService>());
    }

    /// <summary>
    /// 初始化 MCP 管理器
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return true;
        }

        try
        {
            _logger?.LogInformation("Initializing MCP Manager...");

            var servers = await _discoveryService.DiscoverServersAsync(cancellationToken);
            _logger?.LogInformation($"Discovered {servers.Count} MCP servers");

            _initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to initialize MCP Manager: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 处理用户查询的主要方法
    /// </summary>
    /// <param name="userQuery">用户查询内容</param>
    /// <param name="chatClient">聊天客户端</param>
    /// <param name="thinkAreaCallback">用于更新思考区域内容的回调函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RequiresDynamicCode("Uses JSON serialization for MCP protocol which may require dynamic code generation")]
    public async Task<McpResponse> ProcessQueryAsync(string userQuery, IChatClient? chatClient, Action<string>? thinkAreaCallback = null, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new McpResponse
            {
                Answer = "Please enter a valid query.",
                Source = "System"
            };
        }

        try
        {
            _logger?.LogInformation($"Processing query: {userQuery}");
            thinkAreaCallback?.Invoke("🔍 Analyzing query and routing to appropriate MCP tool...");

            // 1. 路由决策 - 选择最佳的 server 和 tool
            var routingDecision = await _routingService.RouteQueryAsync(userQuery, thinkAreaCallback);
            if (routingDecision == null)
            {
                _logger?.LogWarning($"No routing decision found for query: {userQuery}");
                thinkAreaCallback?.Invoke("⚠️ No suitable MCP tool found to handle this query");
                return await HandleNoRouteFoundAsync(userQuery, chatClient, cancellationToken);
            }

            // // 检查是否需要用户澄清
            // if (routingDecision.RequiresClarification)
            // {
            //     _logger?.LogInformation($"Routing requires clarification: {routingDecision.ClarificationQuestion}");
            //     return new McpResponse
            //     {
            //         Answer = $"💬 More information needed: {routingDecision.ClarificationQuestion}",
            //         Source = "AI Routing System",
            //         RawResult = new McpInvocationResult
            //         {
            //             IsSuccess = false,
            //             Data = "需要用户澄清",
            //             RoutingInfo = routingDecision,
            //             ExecutionTime = TimeSpan.Zero
            //         }
            //     };
            // }
            _logger?.LogInformation($"🎯 Multi-step AI routing decision: {routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name} (confidence: {routingDecision.Confidence:F2})");

            thinkAreaCallback?.Invoke($"✅ Tool selected: {routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name}\n📊 Confidence: {routingDecision.Confidence:F2}\n💭 Reasoning: {routingDecision.Reasoning}");

            // 添加可用候选的调试信息
            var candidates = await _routingService.GetRoutingCandidatesAsync(userQuery);
            if (candidates.Count > 1)
            {
                _logger?.LogDebug($"Alternative candidates for '{userQuery}':");
                var alternativesInfo = string.Join("\n", candidates.Take(3).Select(c => $"  • {c.server.Name}.{c.tool.Name}: {c.score:F2}"));

                foreach (var candidate in candidates.Take(3))
                {
                    _logger?.LogDebug($"  {candidate.server.Name}.{candidate.tool.Name}: {candidate.score:F2}");
                }
            }

            // // 2. 权限检查和用户确认
            // var needsConfirmation = RequiresUserConfirmation(routingDecision);
            // if (needsConfirmation)
            // {
            //     return new McpResponse
            //     {
            //         Answer = $"I need to call the {routingDecision.SelectedTool.Name} tool from {routingDecision.SelectedServer.Name} to get information. This operation is safe, would you like to continue?",
            //         Source = $"{routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name}",
            //         RequiresConfirmation = true,
            //         RawResult = new McpInvocationResult { RoutingInfo = routingDecision }
            //     };
            // }

            // 3. 执行工具调用
            thinkAreaCallback?.Invoke($"🔧 Invoking tool {routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name}...");
            var invocationResult = await _invocationService.InvokeToolAsync(routingDecision, cancellationToken);

            if (invocationResult.IsSuccess)
            {
                thinkAreaCallback?.Invoke($"✅ Tool invocation successful (took: {invocationResult.ExecutionTime.TotalMilliseconds:0}ms)\n🤖 Processing results with AI...");
            }
            else
            {
                thinkAreaCallback?.Invoke($"❌ Tool invocation failed: {invocationResult.Error}");
            }

            // 4. 使用 LLM 处理结果
            return await ProcessInvocationResultAsync(userQuery, invocationResult, chatClient, thinkAreaCallback, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing query: {ex.Message}");
            return new McpResponse
            {
                Answer = "An error occurred while processing your query. Please try again later.",
                Source = "Error"
            };
        }
    }

    /// <summary>
    /// 处理工具调用结果，使用 LLM 生成用户友好的回复
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private async Task<McpResponse> ProcessInvocationResultAsync(string originalQuery, McpInvocationResult result, IChatClient? chatClient, Action<string>? thinkAreaCallback, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            return new McpResponse
            {
                Answer = $"Tool invocation failed: {result.Error}",
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }

        // 如果没有 LLM，返回原始数据
        if (chatClient == null)
        {
            thinkAreaCallback?.Invoke("⚠️ No AI model available, returning raw JSON data");
            return new McpResponse
            {
                Answer = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true }),
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }

        try
        {
            // 使用 LLM 处理和提取信息
            var systemPrompt = CreateExtractionSystemPrompt(result);
            var userPrompt = CreateExtractionUserPrompt(originalQuery, result);

            // 合并全局系统提示和结果提取提示
            var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[结果提取]\n{systemPrompt}";

            thinkAreaCallback?.Invoke("🧠 Requesting AI model to analyze and process results...");
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, combinedSystemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var response = await chatClient.GetResponseAsync(messages, null, cancellationToken);
            var extractedAnswer = response?.Text ?? "Unable to extract answer from tool result.";

            thinkAreaCallback?.Invoke("✅ AI processing complete, formatting final answer...");

            // 获取原始 JSON 数据并组合回答
            var rawJson = SerializeResultData(result);
            var combinedAnswer = $"{extractedAnswer}\n\n---------\n{rawJson}";

            return new McpResponse
            {
                Answer = combinedAnswer,
                Source = $"{result.RoutingInfo?.SelectedServer.Name}.{result.RoutingInfo?.SelectedTool.Name}",
                RawResult = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing result with LLM: {ex.Message}");
            thinkAreaCallback?.Invoke($"⚠️ AI processing error, trying simple text extraction: {ex.Message}");

            // 降级到简单的文本提取
            var fallbackAnswer = await ExtractSimpleAnswerAsync(originalQuery, result, chatClient, thinkAreaCallback, cancellationToken);
            return new McpResponse
            {
                Answer = fallbackAnswer,
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }
    }

    /// <summary>
    /// 创建用于信息提取的系统提示
    /// </summary>
    private string CreateExtractionSystemPrompt(McpInvocationResult result)
    {
        return McpPromptTemplateManager.GetResultExtractionSystemPrompt();
    }

    /// <summary>
    /// 从 MCP 调用结果中提取并序列化数据
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private string SerializeResultData(McpInvocationResult result)
    {
        var structuredContent = result.Data?.GetType().GetProperty("StructuredContent")?.GetValue(result.Data, null);
        var dataToSerialize = structuredContent ?? result.Data;
        return JsonSerializer.Serialize(dataToSerialize, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// 创建用于信息提取的用户提示
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private string CreateExtractionUserPrompt(string originalQuery, McpInvocationResult result)
    {
        return McpPromptTemplateManager.FormatResultExtractionUserPrompt(originalQuery, result);
    }

    /// <summary>
    /// 智能答案提取（使用 AI 分析工具返回的数据）
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private async Task<string> ExtractSimpleAnswerAsync(string originalQuery, McpInvocationResult result, IChatClient? chatClient, Action<string>? thinkAreaCallback, CancellationToken cancellationToken)
    {
        if (result.Data == null)
        {
            return "The tool did not return any data.";
        }

        // 如果有 AI 客户端，尝试使用 AI 分析数据
        if (chatClient != null)
        {
            try
            {
                thinkAreaCallback?.Invoke("🔄 Attempting simple AI analysis...");

                // 复用现有的系统提示创建方法
                var systemPrompt = CreateExtractionSystemPrompt(result);

                // 复用现有的用户提示创建方法
                var userPrompt = CreateExtractionUserPrompt(originalQuery, result);

                // 合并全局系统提示和结果提取提示
                var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[简单结果提取]\n{systemPrompt}";

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, combinedSystemPrompt),
                    new(ChatRole.User, userPrompt)
                };

                var response = await chatClient.GetResponseAsync(messages, null, cancellationToken);
                var aiAnswer = response?.Text?.Trim();

                if (!string.IsNullOrEmpty(aiAnswer))
                {
                    _logger?.LogDebug("Successfully extracted answer using AI analysis");
                    thinkAreaCallback?.Invoke("✅ Simple AI analysis completed successfully");

                    // 获取原始 JSON 数据
                    var rawJson = SerializeResultData(result);

                    // 组合 AI 回答和原始数据
                    return $"{aiAnswer}\n\n--- API ---\n{rawJson}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"AI analysis failed, falling back to basic extraction: {ex.Message}");
                thinkAreaCallback?.Invoke($"⚠️ Simple AI analysis failed, falling back to basic extraction: {ex.Message}");
            }
        }

        // 降级到基本的数据提取（无 AI 或 AI 失败时）
        thinkAreaCallback?.Invoke("📄 Using basic data extraction...");
        try
        {
            var json = SerializeResultData(result);
            thinkAreaCallback?.Invoke("✅ Basic data extraction completed");
            return $"Retrieved the following information:\n{json}";
        }
        catch
        {
            thinkAreaCallback?.Invoke("❌ Data extraction failed, returning raw data");
            return result.Data?.ToString() ?? "Unable to parse the returned data.";
        }
    }

    /// <summary>
    /// 处理没有找到合适路由的情况
    /// </summary>
    private async Task<McpResponse> HandleNoRouteFoundAsync(string userQuery, IChatClient? chatClient, CancellationToken cancellationToken)
    {
        var availableServers = _discoveryService.GetConnectedServers();
        var availableTools = _discoveryService.GetAllTools();

        if (!availableServers.Any())
        {
            return new McpResponse
            {
                Answer = "No MCP servers are currently available. Please check the MCP server configuration.",
                Source = "System"
            };
        }

        if (chatClient != null)
        {
            // 使用 LLM 提供替代建议
            var systemPrompt = McpPromptTemplateManager.GetNoRouteFoundSystemPrompt();
            var userPrompt = McpPromptTemplateManager.FormatNoRouteFoundUserPrompt(userQuery, availableTools);

            try
            {
                // 合并全局系统提示和无路由处理提示
                var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[无路由处理]\n{systemPrompt}";

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, combinedSystemPrompt),
                    new(ChatRole.User, userPrompt)
                };

                var response = await chatClient.GetResponseAsync(messages, null, cancellationToken);
                return new McpResponse
                {
                    Answer = response?.Text ?? "Unable to find a suitable tool for your query.",
                    Source = "System"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error generating suggestion with LLM: {ex.Message}");
            }
        }

        // 降级到简单的文本回复
        return new McpResponse
        {
            Answer = $"Sorry, I cannot find a suitable tool for your query. Currently {availableTools.Count} tools are available, mainly covering: {string.Join(", ", availableServers.SelectMany(s => s.Categories).Distinct())}.",
            Source = "System"
        };
    }

    /// <summary>
    /// 检查是否需要用户确认 - 实现最小权限原则
    /// </summary>
    private bool RequiresUserConfirmation(RoutingDecision decision)
    {
        // 基于工具类型或服务器类别决定是否需要确认
        var sensitiveCategories = new[] { "system", "file", "network", "process", "settings", "configuration" };
        var sensitiveToolPatterns = new[] { "delete", "remove", "kill", "stop", "modify", "write", "set", "update", "change", "install", "uninstall" };
        var readOnlyToolPatterns = new[] { "get", "list", "show", "read", "info", "status", "view" };

        var isSensitiveCategory = decision.SelectedServer.Categories
            .Any(cat => sensitiveCategories.Contains(cat.ToLower()));

        var isSensitiveTool = sensitiveToolPatterns
            .Any(pattern => decision.SelectedTool.Name.ToLower().Contains(pattern));

        var isReadOnlyTool = readOnlyToolPatterns
            .Any(pattern => decision.SelectedTool.Name.ToLower().Contains(pattern));

        // 只读工具通常不需要确认，除非涉及敏感系统信息
        if (isReadOnlyTool && !decision.SelectedTool.Name.ToLower().Contains("password"))
        {
            return false;
        }

        // 高风险操作必须确认
        if (isSensitiveTool)
        {
            return true;
        }

        // 敏感类别的非只读操作需要确认
        return isSensitiveCategory && !isReadOnlyTool;
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<Dictionary<string, object>> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new Dictionary<string, object>
        {
            ["initialized"] = _initialized,
            ["connected_servers"] = _discoveryService.GetConnectedServers().Count,
            ["total_tools"] = _discoveryService.GetAllTools().Count
        };

        if (_initialized)
        {
            var servers = _discoveryService.GetConnectedServers();
            var serverStatuses = new List<object>();

            foreach (var server in servers)
            {
                var serverStatus = await _invocationService.GetServerStatusAsync(server.Id, cancellationToken);
                serverStatuses.Add(serverStatus);
            }

            status["servers"] = serverStatuses;
        }

        return status;
    }

    /// <summary>
    /// 获取可用工具目录 - 用于向用户展示能力范围
    /// </summary>
    /// <returns></returns>
    public string GetToolCatalog()
    {
        if (!_initialized)
        {
            return "MCP system has not been initialized yet. Please try again later.";
        }

        var servers = _discoveryService.GetConnectedServers();
        if (!servers.Any())
        {
            return "No MCP server connections are currently available.";
        }

        var catalog = new List<string>
        {
            "=== Available MCP Tools Catalog ===",
            string.Empty
        };

        foreach (var server in servers)
        {
            catalog.Add($"📋 {server.Name}");
            catalog.Add($"   Description: {server.Description}");
            catalog.Add($"   Categories: {string.Join(", ", server.Categories)}");

            var tools = _discoveryService.GetServerTools(server.Id);
            if (tools.Any())
            {
                catalog.Add("   Tools:");
                foreach (var tool in tools)
                {
                    catalog.Add($"     • {tool.Name}: {tool.Description}");
                }
            }

            catalog.Add(string.Empty);
        }

        catalog.Add("💡 Tip: You can ask questions directly, and the system will automatically select the most appropriate tool to answer.");

        return string.Join("\n", catalog);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _discoveryService?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// 包装现有的 logger 来创建不同泛型类型的 logger
/// </summary>
internal class WrappedLoggerFactory
{
    private readonly ILogger _baseLogger;

    public WrappedLoggerFactory(ILogger baseLogger)
    {
        _baseLogger = baseLogger;
    }

    public ILogger<T> CreateLogger<T>()
    {
        return new WrappedLogger<T>(_baseLogger);
    }
}

/// <summary>
/// Logger 包装器，将一个 logger 包装成不同泛型类型
/// </summary>
/// <typeparam name="T"></typeparam>
internal class WrappedLogger<T> : ILogger<T>
{
    private readonly ILogger _baseLogger;

    public WrappedLogger(ILogger baseLogger)
    {
        _baseLogger = baseLogger;
    }

    public IDisposable BeginScope<TState>(TState state) => _baseLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _baseLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _baseLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}