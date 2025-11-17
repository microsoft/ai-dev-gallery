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
        _discoveryService = new McpDiscoveryService(_logger as ILogger<McpDiscoveryService>);
        _routingService = new McpRoutingService(_discoveryService, _logger as ILogger<McpRoutingService>, chatClient);
        _invocationService = new McpInvocationService(_discoveryService, _logger as ILogger<McpInvocationService>);
    }

    /// <summary>
    /// 初始化 MCP 管理器
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<McpResponse> ProcessQueryAsync(string userQuery, IChatClient? chatClient, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new McpResponse
            {
                Answer = "请输入一个有效的查询。",
                Source = "System"
            };
        }

        try
        {
            _logger?.LogInformation($"Processing query: {userQuery}");

            // 1. 路由决策 - 选择最佳的 server 和 tool
            var routingDecision = await _routingService.RouteQueryAsync(userQuery);
            if (routingDecision == null)
            {
                _logger?.LogWarning($"No routing decision found for query: {userQuery}");
                return await HandleNoRouteFoundAsync(userQuery, chatClient, cancellationToken);
            }

            // 检查是否需要用户澄清
            if (routingDecision.RequiresClarification)
            {
                _logger?.LogInformation($"Routing requires clarification: {routingDecision.ClarificationQuestion}");
                return new McpResponse
                {
                    Answer = $"💬 需要更多信息：{routingDecision.ClarificationQuestion}",
                    Source = "AI路由系统",
                    RawResult = new McpInvocationResult
                    {
                        IsSuccess = false,
                        Data = "需要用户澄清",
                        RoutingInfo = routingDecision,
                        ExecutionTime = TimeSpan.Zero
                    }
                };
            }

            _logger?.LogInformation($"🎯 Multi-step AI routing decision: {routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name} (confidence: {routingDecision.Confidence:F2})");
            
            // 添加可用候选的调试信息
            var candidates = await _routingService.GetRoutingCandidatesAsync(userQuery);
            if (candidates.Count > 1)
            {
                _logger?.LogDebug($"Alternative candidates for '{userQuery}':");
                foreach (var candidate in candidates.Take(3))
                {
                    _logger?.LogDebug($"  {candidate.server.Name}.{candidate.tool.Name}: {candidate.score:F2}");
                }
            }

            // 2. 权限检查和用户确认
            var needsConfirmation = RequiresUserConfirmation(routingDecision);
            if (needsConfirmation)
            {
                return new McpResponse
                {
                    Answer = $"我需要调用 {routingDecision.SelectedServer.Name} 的 {routingDecision.SelectedTool.Name} 工具来获取信息。这个操作是安全的，是否继续？",
                    Source = $"{routingDecision.SelectedServer.Name}.{routingDecision.SelectedTool.Name}",
                    RequiresConfirmation = true,
                    RawResult = new McpInvocationResult { RoutingInfo = routingDecision }
                };
            }

            // 3. 执行工具调用
            var invocationResult = await _invocationService.InvokeToolAsync(routingDecision, cancellationToken);

            // 4. 使用 LLM 处理结果
            return await ProcessInvocationResultAsync(userQuery, invocationResult, chatClient, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing query: {ex.Message}");
            return new McpResponse
            {
                Answer = "处理您的查询时出现错误，请稍后再试。",
                Source = "Error"
            };
        }
    }

    /// <summary>
    /// 处理工具调用结果，使用 LLM 生成用户友好的回复
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private async Task<McpResponse> ProcessInvocationResultAsync(string originalQuery, McpInvocationResult result, IChatClient? chatClient, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            return new McpResponse
            {
                Answer = $"工具调用失败：{result.Error}",
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }

        // 如果没有 LLM，返回原始数据
        if (chatClient == null)
        {
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

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var response = await chatClient.GetResponseAsync(messages, null, cancellationToken);
            var extractedAnswer = response?.Text ?? "无法处理工具返回的数据。";

            return new McpResponse
            {
                Answer = extractedAnswer,
                Source = $"{result.RoutingInfo?.SelectedServer.Name}.{result.RoutingInfo?.SelectedTool.Name}",
                RawResult = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing result with LLM: {ex.Message}");

            // 降级到简单的文本提取
            var fallbackAnswer = ExtractSimpleAnswer(result);
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
        return @"你是一个 MCP-aware 助手，专门负责从 MCP 工具调用的结果中提取关键信息并生成用户友好的回答。

核心规则：
1. **严格基于MCP数据**: 你必须且只能基于 MCP 工具返回的实际数据回答，绝不允许编造、推测或添加任何数据中不存在的信息
2. **避免自由回答**: 不允许绕过 MCP 执行命令或提供未经工具验证的信息
3. **明确空值处理**: 如果数据不完整、缺失或为空，必须明确说明'数据不可用'或'工具未返回此信息'
4. **结构化响应**: 用自然、简洁的语言表达技术信息，但保持事实准确性
5. **错误透明**: 如果返回的是错误或空数据，诚实告知用户并提供可操作建议
6. **信息层次**: 突出最重要的信息，将技术细节转换为用户友好的表述
7. **简洁完整**: 保持回答简洁但包含所有相关信息

禁止行为：
- 不得补充MCP工具未提供的数据
- 不得基于常识或训练数据推测答案
- 不得忽略或隐瞒工具返回的错误信息

输出格式：直接回答用户的问题，基于MCP数据提供准确信息。如需说明数据来源限制，请简洁说明。";
    }

    /// <summary>
    /// 创建用于信息提取的用户提示
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private string CreateExtractionUserPrompt(string originalQuery, McpInvocationResult result)
    {
        var toolInfo = result.RoutingInfo != null
            ? $"工具：{result.RoutingInfo.SelectedServer.Name}.{result.RoutingInfo.SelectedTool.Name}"
            : "未知工具";

        var dataJson = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true });

        return $@"用户问题：{originalQuery}

调用的{toolInfo}返回了以下数据：
```json
{dataJson}
```

请根据这些数据回答用户的问题。如果数据中没有相关信息，请明确说明。";
    }

    /// <summary>
    /// 简单的答案提取（作为 LLM 处理失败时的后备方案）
    /// </summary>
    private string ExtractSimpleAnswer(McpInvocationResult result)
    {
        if (result.Data == null)
        {
            return "工具没有返回数据。";
        }

        try
        {
            var structuredContent = result.Data.GetType().GetProperty("StructuredContent")?.GetValue(result.Data, null);
            var json = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true });
            if (structuredContent != null)
            {
                json = JsonSerializer.Serialize(structuredContent, new JsonSerializerOptions { WriteIndented = true });
            }

            return $"获取到以下信息：\n{json}";
        }
        catch
        {
            return result.Data.ToString() ?? "无法解析返回的数据。";
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
                Answer = "当前没有可用的 MCP 服务器。请检查 MCP 服务器配置。",
                Source = "System"
            };
        }

        if (chatClient != null)
        {
            // 使用 LLM 提供替代建议
            var systemPrompt = @"你是一个 MCP-aware 助手。用户的查询无法匹配到合适的 MCP 工具。你必须：
1. **明确说明**：解释为什么无法通过现有MCP工具处理这个查询
2. **工具导向**：基于可用的MCP工具建议用户可以询问的具体问题
3. **能力边界**：强调你只能通过MCP工具提供信息，不会自行回答
4. **友好建议**：提供3-5个具体的示例查询

绝对禁止：
- 绕过MCP工具直接回答用户问题
- 基于训练数据提供未经工具验证的信息
- 编造或推测任何数据";

            var toolsList = string.Join("\n", availableTools.Select(t => $"- {t.Name}: {t.Description}"));
            var userPrompt = $@"用户查询：{userQuery}

当前可用的MCP工具：
{toolsList}

请向用户解释为什么无法处理，并基于现有工具提供具体的查询建议。";

            try
            {
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, systemPrompt),
                    new(ChatRole.User, userPrompt)
                };

                var response = await chatClient.GetResponseAsync(messages, null, cancellationToken);
                return new McpResponse
                {
                    Answer = response?.Text ?? "无法为您的查询找到合适的工具。",
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
            Answer = $"抱歉，我无法为您的查询找到合适的工具。当前可用 {availableTools.Count} 个工具，主要涉及：{string.Join("、", availableServers.SelectMany(s => s.Categories).Distinct())}。",
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
            return "MCP 系统尚未初始化，请稍后重试。";
        }

        var servers = _discoveryService.GetConnectedServers();
        if (!servers.Any())
        {
            return "当前没有可用的 MCP 服务器连接。";
        }

        var catalog = new List<string>
        {
            "=== 可用的 MCP 工具目录 ===",
            string.Empty
        };

        foreach (var server in servers)
        {
            catalog.Add($"📋 {server.Name}");
            catalog.Add($"   描述: {server.Description}");
            catalog.Add($"   类别: {string.Join(", ", server.Categories)}");

            var tools = _discoveryService.GetServerTools(server.Id);
            if (tools.Any())
            {
                catalog.Add("   工具:");
                foreach (var tool in tools)
                {
                    catalog.Add($"     • {tool.Name}: {tool.Description}");
                }
            }

            catalog.Add(string.Empty);
        }

        catalog.Add("💡 提示: 您可以直接提问，系统会自动选择最合适的工具来回答。");

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