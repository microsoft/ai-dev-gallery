// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 路由服务 - 根据用户意图选择最合适的 server 和 tool
/// </summary>
public class McpRoutingService
{
    private readonly McpDiscoveryService _discoveryService;
    private readonly ILogger<McpRoutingService>? _logger;
    private readonly IChatClient? _chatClient;

    // 预定义的意图到工具的映射规则 - 更详细和具体
    private readonly Dictionary<string, List<string>> _intentKeywords = new()
    {
        ["memory"] = ["ram", "memory", "内存", "meminfo", "内存使用", "内存状态", "available", "used"],
        ["storage"] = ["disk", "storage", "硬盘", "磁盘", "存储", "space", "free", "usage", "drive", "volume"],
        ["cpu"] = ["cpu", "processor", "处理器", "性能", "load", "usage", "cores", "frequency", "ghz"],
        ["system"] = ["system", "computer", "pc", "电脑", "系统", "info", "information", "overview", "status"],
        ["process"] = ["process", "task", "program", "进程", "程序", "running", "pid", "application"],
        ["file"] = ["file", "folder", "directory", "文件", "文件夹", "path", "read", "write", "list", "exists"],
        ["network"] = ["network", "internet", "connection", "网络", "连接", "adapter", "ip", "interface"],
        ["settings"] = ["settings", "config", "configuration", "设置", "配置", "registry", "policy", "preference"],
        ["hardware"] = ["hardware", "硬件", "device", "component", "gpu", "motherboard", "bios"]
    };

    /// <summary>
    /// 意图识别响应模型
    /// </summary>
    private class IntentClassificationResponse
    {
        public bool NeedTool { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string[] Keywords { get; set; } = [];
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 服务器选择响应模型
    /// </summary>
    private class ServerSelectionResponse
    {
        public string ChosenServerId { get; set; } = string.Empty;
        public ServerRanking[] Ranking { get; set; } = [];
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 服务器排名模型
    /// </summary>
    private class ServerRanking
    {
        public string ServerId { get; set; } = string.Empty;
        public double Score { get; set; }
        public string[] Reasons { get; set; } = [];
    }

    /// <summary>
    /// 工具选择响应模型
    /// </summary>
    private class ToolSelectionResponse
    {
        public string ChosenToolName { get; set; } = string.Empty;
        public string[] Alternatives { get; set; } = [];
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 参数提取响应模型
    /// </summary>
    private class ArgumentExtractionResponse
    {
        public Dictionary<string, object> Arguments { get; set; } = new();
        public string[] Missing { get; set; } = [];
        public string ClarifyQuestion { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 工具调用计划响应模型
    /// </summary>
    private class ToolInvocationPlanResponse
    {
        public string Action { get; set; } = "call_tool";
        public string ServerId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object> Arguments { get; set; } = new();
        public int TimeoutMs { get; set; } = 120000;
        public int Retries { get; set; } = 1;
    }



    public McpRoutingService(McpDiscoveryService discoveryService, ILogger<McpRoutingService>? logger = null, IChatClient? chatClient = null)
    {
        _discoveryService = discoveryService;
        _logger = logger;
        _chatClient = chatClient;
    }

    /// <summary>
    /// 使用多步骤AI决策流程根据用户查询找到最佳的 server 和 tool
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<RoutingDecision?> RouteQueryAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return null;
        }

        _logger?.LogInformation($"🔍 Starting AI-driven multi-step routing for: '{userQuery}'");

        // 获取所有可用的 servers
        var servers = _discoveryService.GetConnectedServers();
        
        if (!servers.Any())
        {
            _logger?.LogWarning("❌ No servers available for routing");
            return null;
        }

        // 使用AI模型进行多步骤决策
        if (_chatClient != null)
        {
            return await RouteWithMultiStepAIAsync(userQuery, servers);
        }
        else
        {
            // 降级到简单的关键词匹配
            _logger?.LogWarning("⚠️ No AI chat client available, falling back to keyword matching");
            return await RouteWithKeywordsAsync(userQuery, servers);
        }
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
            var intent = await ClassifyIntentAsync(userQuery);
            if (intent == null)
            {
                _logger?.LogWarning("❌ Failed to classify user intent");
                return null;
            }

            _logger?.LogInformation($"📊 Intent: needTool={intent.NeedTool}, topic={intent.Topic}, confidence={intent.Confidence:F2}");

            if (!intent.NeedTool)
            {
                _logger?.LogInformation("ℹ️ AI determined no tool is needed for this query");
                return null;
            }

            // 步骤2: 服务器选择
            _logger?.LogInformation("🖥️ Step 2: Server Selection");
            var serverSelection = await SelectServerAsync(userQuery, servers, intent);
            if (serverSelection == null || string.IsNullOrEmpty(serverSelection.ChosenServerId))
            {
                _logger?.LogWarning("❌ Failed to select appropriate server");
                return null;
            }

            var selectedServer = servers.FirstOrDefault(s => s.Id == serverSelection.ChosenServerId);
            if (selectedServer == null)
            {
                _logger?.LogWarning($"❌ Selected server not found: {serverSelection.ChosenServerId}");
                return null;
            }

            _logger?.LogInformation($"🏆 Selected server: {selectedServer.Name} (confidence: {serverSelection.Confidence:F2})");

            // 步骤3: 工具选择
            _logger?.LogInformation("🔧 Step 3: Tool Selection");
            var availableTools = _discoveryService.GetServerTools(selectedServer.Id);
            var toolSelection = await SelectToolAsync(userQuery, selectedServer, availableTools, intent);
            if (toolSelection == null || string.IsNullOrEmpty(toolSelection.ChosenToolName))
            {
                _logger?.LogWarning("❌ Failed to select appropriate tool");
                return null;
            }

            var selectedTool = availableTools.FirstOrDefault(t => t.Name == toolSelection.ChosenToolName);
            if (selectedTool == null)
            {
                _logger?.LogWarning($"❌ Selected tool not found: {toolSelection.ChosenToolName}");
                return null;
            }

            _logger?.LogInformation($"⚙️ Selected tool: {selectedTool.Name} (confidence: {toolSelection.Confidence:F2})");

            // 步骤4: 参数提取
            _logger?.LogInformation("📝 Step 4: Argument Extraction");
            var argumentExtraction = await ExtractArgumentsAsync(userQuery, selectedTool, intent);
            if (argumentExtraction == null)
            {
                _logger?.LogWarning("❌ Failed to extract arguments");
                return null;
            }

            // 检查是否有缺失参数需要用户澄清
            if (argumentExtraction.Missing.Any())
            {
                _logger?.LogInformation($"❓ Missing parameters: {string.Join(", ", argumentExtraction.Missing)}");
                return new RoutingDecision
                {
                    SelectedServer = selectedServer,
                    SelectedTool = selectedTool,
                    Parameters = argumentExtraction.Arguments,
                    Confidence = argumentExtraction.Confidence,
                    Reasoning = $"需要澄清: {argumentExtraction.ClarifyQuestion}",
                    RequiresClarification = true,
                    ClarificationQuestion = argumentExtraction.ClarifyQuestion
                };
            }

            // 步骤5: 生成工具调用计划
            _logger?.LogInformation("📋 Step 5: Tool Invocation Planning");
            var invocationPlan = await CreateInvocationPlanAsync(userQuery, selectedServer, selectedTool, argumentExtraction.Arguments);
            if (invocationPlan == null)
            {
                _logger?.LogWarning("❌ Failed to create invocation plan");
                return null;
            }

            _logger?.LogInformation($"✅ Multi-step AI routing completed successfully");

            return new RoutingDecision
            {
                SelectedServer = selectedServer,
                SelectedTool = selectedTool,
                Parameters = argumentExtraction.Arguments,
                Confidence = Math.Min(intent.Confidence, Math.Min(serverSelection.Confidence, Math.Min(toolSelection.Confidence, argumentExtraction.Confidence))),
                Reasoning = $"AI多步骤决策: 意图={intent.Topic}, 服务器={selectedServer.Name}, 工具={selectedTool.Name}",
                RequiresClarification = false,
                InvocationPlan = invocationPlan
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Error during multi-step AI routing, falling back to keyword matching");
            return await RouteWithKeywordsAsync(userQuery, servers);
        }
    }

    /// <summary>
    /// 步骤1: 使用AI进行意图识别
    /// </summary>
    private async Task<IntentClassificationResponse?> ClassifyIntentAsync(string userQuery)
    {
        var systemPrompt = """
            你是一个 MCP-aware 助手，只能通过 MCP 协议调用已注册的工具来完成用户请求。不得绕过 MCP 执行命令，也不得在未调用工具时编造答案。

            请根据用户问题识别是否需要通过 MCP 工具来完成任务，并给出主题与关键词。

            返回严格的JSON格式，不得输出任何解释性文字：
            {
              "need_tool": true | false,
              "topic": "systeminfo | filesystem | settings | hardware | network | other",
              "keywords": ["string", ...],
              "confidence": 0.0-1.0
            }
            """;

        var userPrompt = $"用户问题：{userQuery}";

        return await CallAIWithJsonResponse<IntentClassificationResponse>(systemPrompt, userPrompt, "意图识别");
    }

    /// <summary>
    /// 步骤2: 使用AI选择最佳服务器
    /// </summary>
    private async Task<ServerSelectionResponse?> SelectServerAsync(string userQuery, List<McpServerInfo> servers, IntentClassificationResponse intent)
    {
        var serversJson = JsonSerializer.Serialize(servers.Select(s => new
        {
            id = s.Id,
            name = s.Name,
            description = s.Description,
            tags = s.Categories,
            health_score = s.HealthScore,
            success_rate = s.SuccessRate,
            avg_response_time = s.AverageResponseTime
        }), new JsonSerializerOptions { WriteIndented = true });

        var systemPrompt = """
            从以下可用 MCP servers 中选择最适合完成任务的一个。请依据名称、标签、能力、健康度等进行排序，并返回最佳者。

            返回严格的JSON格式，不得输出任何解释性文字：
            {
              "chosen_server_id": "string",
              "ranking": [
                {"server_id": "string", "score": 0.0-1.0, "reasons": ["string", "..."]},
                {"server_id": "string", "score": 0.0-1.0, "reasons": ["string", "..."]}
              ],
              "confidence": 0.0-1.0
            }
            """;

        var userPrompt = $"""
            用户问题：{userQuery}
            检测到的意图：{JsonSerializer.Serialize(intent)}
            可用 servers（JSON 数组）：{serversJson}
            """;

        return await CallAIWithJsonResponse<ServerSelectionResponse>(systemPrompt, userPrompt, "服务器选择");
    }

    /// <summary>
    /// 步骤3: 使用AI选择最佳工具
    /// </summary>
    private async Task<ToolSelectionResponse?> SelectToolAsync(string userQuery, McpServerInfo server, List<McpToolInfo> tools, IntentClassificationResponse intent)
    {
        var toolsJson = JsonSerializer.Serialize(tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            input_schema = t.InputSchema
        }), new JsonSerializerOptions { WriteIndented = true });

        var systemPrompt = """
            基于选定的 MCP server 的可用工具列表，选择一个最能满足用户需求的工具。优先参数少、权限小、成功率高的工具。

            返回严格的JSON格式，不得输出任何解释性文字：
            {
              "chosen_tool_name": "string",
              "alternatives": ["string", "..."],
              "confidence": 0.0-1.0
            }
            """;

        var userPrompt = $"""
            用户问题：{userQuery}
            检测到的意图：{JsonSerializer.Serialize(intent)}
            选定 server：{server.Id}
            可用工具列表：{toolsJson}
            """;

        return await CallAIWithJsonResponse<ToolSelectionResponse>(systemPrompt, userPrompt, "工具选择");
    }

    /// <summary>
    /// 步骤4: 使用AI提取工具参数
    /// </summary>
    private async Task<ArgumentExtractionResponse?> ExtractArgumentsAsync(string userQuery, McpToolInfo tool, IntentClassificationResponse intent)
    {
        var systemPrompt = """
            请根据工具的参数定义，从用户问题与上下文中提取或推断调用所需的**最小充分参数**。不得臆造未知值；若缺失请返回缺失字段并附一条中文澄清问题。

            返回严格的JSON格式，不得输出任何解释性文字：
            {
              "arguments": { /* 满足 schema 的键值 */ },
              "missing": ["fieldA", "fieldB"],       // 若无缺失则为空数组
              "clarify_question": "仅当missing非空时给出的一条中文澄清问题",
              "confidence": 0.0-1.0
            }
            """;

        var userPrompt = $"""
            用户问题：{userQuery}
            检测到的意图：{JsonSerializer.Serialize(intent)}
            工具：{tool.Name}
            参数Schema：{JsonSerializer.Serialize(tool.InputSchema)}
            """;

        return await CallAIWithJsonResponse<ArgumentExtractionResponse>(systemPrompt, userPrompt, "参数提取");
    }

    /// <summary>
    /// 步骤5: 使用AI生成工具调用计划
    /// </summary>
    private async Task<ToolInvocationPlanResponse?> CreateInvocationPlanAsync(string userQuery, McpServerInfo server, McpToolInfo tool, Dictionary<string, object> arguments)
    {
        var systemPrompt = """
            请生成一次可执行的 MCP 工具调用计划。计划必须可由客户端直接执行，不包含自由文本。

            返回严格的JSON格式，不得输出任何解释性文字：
            {
              "action": "call_tool",
              "server_id": "string",
              "tool_name": "string",
              "arguments": {},
              "timeout_ms": 120000,
              "retries": 1
            }
            """;

        var userPrompt = $"""
            用户问题：{userQuery}
            已选 server/tool/args：
            - server: {server.Id}
            - tool: {tool.Name}
            - args: {JsonSerializer.Serialize(arguments)}
            """;

        return await CallAIWithJsonResponse<ToolInvocationPlanResponse>(systemPrompt, userPrompt, "调用计划");
    }

    /// <summary>
    /// 通用AI调用方法，解析JSON响应
    /// </summary>
    private async Task<T?> CallAIWithJsonResponse<T>(string systemPrompt, string userPrompt, string stepName) where T : class
    {
        try
        {
            var messages = new[]
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var response = await _chatClient!.GetResponseAsync(messages);
            var aiResponse = response.Text ?? string.Empty;

            _logger?.LogDebug($"🤖 {stepName} AI Response: {aiResponse}");

            // 尝试提取并解析JSON
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd >= jsonStart)
            {
                var jsonPart = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<T>(jsonPart, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (result != null)
                {
                    _logger?.LogDebug($"✅ {stepName} parsed successfully");
                    return result;
                }
            }

            _logger?.LogWarning($"⚠️ Could not parse {stepName} response: {aiResponse}");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"❌ Error during {stepName}");
            return null;
        }
    }



    /// <summary>
    /// 降级方案：使用关键词匹配进行路由
    /// </summary>
    private async Task<RoutingDecision?> RouteWithKeywordsAsync(string userQuery, List<McpServerInfo> servers)
    {
        var query = userQuery.ToLowerInvariant();
        
        // 简单的关键词匹配逻辑
        var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score)>();
        
        foreach (var server in servers)
        {
            var tools = _discoveryService.GetServerTools(server.Id);
            foreach (var tool in tools)
            {
                var score = CalculateSimpleMatchScore(query, server, tool);
                if (score > 0)
                {
                    candidates.Add((server, tool, score));
                }
            }
        }

        if (!candidates.Any())
        {
            return null;
        }

        var best = candidates.OrderByDescending(c => c.score).First();
        
        _logger?.LogInformation($"✅ Keyword matching selected: {best.server.Name}.{best.tool.Name}");

        return new RoutingDecision
        {
            SelectedServer = best.server,
            SelectedTool = best.tool,
            Parameters = new Dictionary<string, object>(),
            Confidence = best.score,
            Reasoning = "Keyword-based fallback selection"
        };
    }

    /// <summary>
    /// 简单的匹配分数计算
    /// </summary>
    private double CalculateSimpleMatchScore(string query, McpServerInfo server, McpToolInfo tool)
    {
        var score = 0.0;
        
        // 服务器名称匹配
        if (query.Contains("system") || query.Contains("系统") || query.Contains("电脑"))
        {
            if (server.Name.Contains("system-info")) score += 10;
        }
        if (query.Contains("file") || query.Contains("文件") || query.Contains("folder"))
        {
            if (server.Name.Contains("file-system")) score += 10;
        }
        if (query.Contains("setting") || query.Contains("设置") || query.Contains("配置"))
        {
            if (server.Name.Contains("settings")) score += 10;
        }

        // 工具名称匹配
        if (tool.Name.ToLowerInvariant().Contains("get") && (query.Contains("show") || query.Contains("display") || query.Contains("显示")))
        {
            score += 5;
        }

        return score;
    }

    /// <summary>
    /// 分析用户意图 - 改进的多维度分析
    /// </summary>
    private string AnalyzeUserIntent(string query)
    {
        var queryLower = query.ToLower();
        var scores = new Dictionary<string, double>();

        foreach (var (intent, keywords) in _intentKeywords)
        {
            double score = 0;
            
            foreach (var keyword in keywords)
            {
                var keywordLower = keyword.ToLower();
                
                // 完全匹配得分最高
                if (queryLower.Contains(keywordLower))
                {
                    if (queryLower == keywordLower)
                    {
                        score += 10; // 完整匹配
                    }
                    else if (queryLower.Split(' ').Contains(keywordLower))
                    {
                        score += 8; // 单词匹配
                    }
                    else
                    {
                        score += 5; // 部分匹配
                    }
                }
                
                // 模糊匹配（词干）
                if (keywordLower.Length > 3 && queryLower.Contains(keywordLower.Substring(0, Math.Min(keywordLower.Length - 1, 4))))
                {
                    score += 2;
                }
            }
            
            if (score > 0)
            {
                scores[intent] = score;
            }
        }

        if (!scores.Any())
        {
            return "general";
        }

        var bestIntent = scores.OrderByDescending(kvp => kvp.Value).First();
        _logger?.LogInformation($"Intent analysis for '{query}': {bestIntent.Key} (score: {bestIntent.Value:F1})");
        
        return bestIntent.Key;
    }

    /// <summary>
    /// 计算 server-tool 组合与查询的匹配分数 - 改进的评分算法
    /// </summary>
    private (double score, string reasoning) CalculateMatchScore(string query, string intent, McpServerInfo server, McpToolInfo tool)
    {
        double score = 0;
        var reasons = new List<string>();
        var queryLower = query.ToLower();

        // 1. 服务器类型与意图的强匹配 - 这是最重要的因子
        var intentKeywords = _intentKeywords.ContainsKey(intent) ? _intentKeywords[intent] : new List<string>();
        var serverTypeScore = CalculateServerTypeScore(server.Id, intent, intentKeywords);
        score += serverTypeScore;
        if (serverTypeScore > 0)
        {
            reasons.Add($"Server type match: {serverTypeScore:F1}");
        }

        // 2. 工具名称的精确匹配 - 第二重要的因子
        var toolNameScore = CalculateToolNameScore(tool.Name, queryLower, intentKeywords);
        score += toolNameScore;
        if (toolNameScore > 0)
        {
            reasons.Add($"Tool name relevance: {toolNameScore:F1}");
        }

        // 3. 工具描述匹配
        var descriptionScore = CalculateDescriptionScore(tool.Description, queryLower, intentKeywords);
        score += descriptionScore;
        if (descriptionScore > 0)
        {
            reasons.Add($"Description match: {descriptionScore:F1}");
        }

        // 4. 关键词匹配
        var keywordScore = CalculateKeywordScore(tool.Keywords, queryLower, intentKeywords);
        score += keywordScore;
        if (keywordScore > 0)
        {
            reasons.Add($"Keyword match: {keywordScore:F1}");
        }

        // 5. 工具优先级加成
        score += tool.Priority * 0.5; // 缩小优先级的影响

        // 6. 服务器健康度调整
        score *= server.SuccessRate; // 乘法而非加法，确保不健康的服务器得分显著降低

        // 7. 响应时间惩罚
        if (server.ResponseTime.HasValue)
        {
            var responseMs = server.ResponseTime.Value.TotalMilliseconds;
            var penalty = Math.Min(responseMs / 500, 0.3); // 最多30%的惩罚
            score *= (1 - penalty);
        }

        var reasoning = reasons.Any() ? string.Join("; ", reasons) : "No specific match found";
        _logger?.LogDebug($"Score for {server.Name}.{tool.Name}: {score:F2} ({reasoning})");
        
        return (score, reasoning);
    }

    /// <summary>
    /// 计算服务器类型匹配分数
    /// </summary>
    private double CalculateServerTypeScore(string serverId, string intent, List<string> intentKeywords)
    {
        var score = 0.0;

        // 直接服务器类型匹配
        var serverTypeMatches = new Dictionary<string, string[]>
        {
            ["system-info"] = ["system", "memory", "cpu", "storage", "hardware"],
            ["file-system"] = ["file"],
            ["settings"] = ["settings"]
        };

        if (serverTypeMatches.ContainsKey(serverId))
        {
            var serverTypes = serverTypeMatches[serverId];
            if (serverTypes.Contains(intent))
            {
                score += 50; // 直接匹配得最高分
            }
            else
            {
                // 检查关键词匹配
                var matchCount = intentKeywords.Count(kw => 
                    serverTypes.Any(st => kw.Contains(st, StringComparison.OrdinalIgnoreCase)));
                score += matchCount * 15;
            }
        }

        return score;
    }

    /// <summary>
    /// 计算工具名称匹配分数
    /// </summary>
    private double CalculateToolNameScore(string toolName, string queryLower, List<string> intentKeywords)
    {
        var score = 0.0;
        var toolNameLower = toolName.ToLower();

        // 直接查询匹配
        if (toolNameLower.Contains(queryLower) || queryLower.Contains(toolNameLower))
        {
            score += 30;
        }

        // 关键词匹配
        foreach (var keyword in intentKeywords)
        {
            if (toolNameLower.Contains(keyword.ToLower()))
            {
                score += 20;
            }
        }

        // 操作类型匹配
        var actionWords = new[] { "get", "set", "list", "info", "status", "read", "write", "create", "delete" };
        var queryWords = Regex.Split(queryLower, @"\W+").Where(w => w.Length > 2);
        
        foreach (var action in actionWords)
        {
            if (toolNameLower.Contains(action) && queryWords.Contains(action))
            {
                score += 15;
            }
        }

        return score;
    }

    /// <summary>
    /// 计算描述匹配分数
    /// </summary>
    private double CalculateDescriptionScore(string description, string queryLower, List<string> intentKeywords)
    {
        if (string.IsNullOrEmpty(description))
        {
            return 0;
        }

        var score = 0.0;
        var descriptionLower = description.ToLower();

        // 关键词匹配
        foreach (var keyword in intentKeywords)
        {
            if (descriptionLower.Contains(keyword.ToLower()))
            {
                score += 8;
            }
        }

        // 查询词匹配
        var queryWords = Regex.Split(queryLower, @"\W+").Where(w => w.Length > 2);
        var matchCount = queryWords.Count(word => descriptionLower.Contains(word));
        score += matchCount * 5;

        return score;
    }

    /// <summary>
    /// 计算关键词匹配分数
    /// </summary>
    private double CalculateKeywordScore(string[] toolKeywords, string queryLower, List<string> intentKeywords)
    {
        var score = 0.0;

        foreach (var toolKeyword in toolKeywords)
        {
            var toolKeywordLower = toolKeyword.ToLower();
            
            // 与意图关键词匹配
            foreach (var intentKeyword in intentKeywords)
            {
                if (toolKeywordLower.Contains(intentKeyword.ToLower()) || 
                    intentKeyword.ToLower().Contains(toolKeywordLower))
                {
                    score += 10;
                }
            }

            // 与查询直接匹配
            if (queryLower.Contains(toolKeywordLower))
            {
                score += 12;
            }
        }

        return score;
    }

    /// <summary>
    /// 从用户查询中提取工具参数
    /// </summary>
    private Dictionary<string, object> ExtractParameters(string query, string intent, McpToolInfo tool)
    {
        var parameters = new Dictionary<string, object>();

        // 基于工具的输入 schema 尝试提取参数
        if (tool.InputSchema?.ContainsKey("properties") == true)
        {
            // 这里可以实现更复杂的参数提取逻辑
            // 现在只是简单的示例

            // 如果工具需要特定类型的参数，可以从查询中提取
            var queryLower = query.ToLower();

            // 示例：提取数字参数
            var numbers = Regex.Matches(query, @"\d+").Cast<Match>().Select(m => m.Value).ToList();
            if (numbers.Any())
            {
                parameters["value"] = numbers.First();
            }

            // 示例：提取路径参数
            var pathPattern = @"[A-Za-z]:\\[^<>:""|?*\n\r]*";
            var pathMatch = Regex.Match(query, pathPattern);
            if (pathMatch.Success)
            {
                parameters["path"] = pathMatch.Value;
            }
        }

        return parameters;
    }

    /// <summary>
    /// 获取候选的 server-tool 组合用于调试
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<List<(McpServerInfo server, McpToolInfo tool, double score)>> GetRoutingCandidatesAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return Task.FromResult<List<(McpServerInfo server, McpToolInfo tool, double score)>>(new List<(McpServerInfo, McpToolInfo, double)>());
        }

        var intent = AnalyzeUserIntent(userQuery);
        var servers = _discoveryService.GetConnectedServers();
        var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score)>();

        foreach (var server in servers)
        {
            var serverTools = _discoveryService.GetServerTools(server.Id);
            foreach (var tool in serverTools)
            {
                var (score, reasoning) = CalculateMatchScore(userQuery, intent, server, tool);
                candidates.Add((server, tool, score));
            }
        }

        return Task.FromResult(candidates.OrderByDescending(c => c.score).ToList());
    }
}