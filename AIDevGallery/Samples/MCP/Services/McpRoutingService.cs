// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

    // 预定义的意图到工具的映射规则
    private readonly Dictionary<string, List<string>> _intentKeywords = new()
    {
        ["memory"] = ["ram", "memory", "内存"],
        ["storage"] = ["disk", "storage", "硬盘", "磁盘", "存储"],
        ["cpu"] = ["cpu", "processor", "处理器", "性能"],
        ["system"] = ["system", "computer", "pc", "电脑", "系统"],
        ["process"] = ["process", "task", "program", "进程", "程序"],
        ["file"] = ["file", "folder", "directory", "文件", "文件夹"],
        ["network"] = ["network", "internet", "connection", "网络", "连接"]
    };

    public McpRoutingService(McpDiscoveryService discoveryService, ILogger<McpRoutingService>? logger = null)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// 根据用户查询找到最佳的 server 和 tool
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<RoutingDecision?> RouteQueryAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return Task.FromResult<RoutingDecision?>(null);
        }

        _logger?.LogInformation($"Routing query: {userQuery}");

        // 1. 分析用户意图
        var intent = AnalyzeUserIntent(userQuery);
        _logger?.LogDebug($"Detected intent: {intent}");

        // 2. 获取所有可用的 servers 和 tools
        var servers = _discoveryService.GetConnectedServers();
        var allTools = _discoveryService.GetAllTools();

        if (!servers.Any() || !allTools.Any())
        {
            _logger?.LogWarning("No servers or tools available for routing");
            return Task.FromResult<RoutingDecision?>(null);
        }

        // 3. 为每个 server-tool 组合计算匹配分数
        var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score, string reasoning)>();

        foreach (var server in servers)
        {
            var serverTools = _discoveryService.GetServerTools(server.Id);
            foreach (var tool in serverTools)
            {
                var (score, reasoning) = CalculateMatchScore(userQuery, intent, server, tool);
                if (score > 0)
                {
                    candidates.Add((server, tool, score, reasoning));
                }
            }
        }

        // 4. 选择最佳候选
        if (!candidates.Any())
        {
            _logger?.LogWarning("No suitable server-tool candidates found");
            return Task.FromResult<RoutingDecision?>(null);
        }

        var best = candidates.OrderByDescending(c => c.score).First();
        _logger?.LogInformation($"Selected {best.server.Name}.{best.tool.Name} with score {best.score:F2}");

        // 5. 提取参数
        var parameters = ExtractParameters(userQuery, intent, best.tool);

        return Task.FromResult<RoutingDecision?>(new RoutingDecision
        {
            SelectedServer = best.server,
            SelectedTool = best.tool,
            Parameters = parameters,
            Confidence = best.score,
            Reasoning = best.reasoning
        });
    }

    /// <summary>
    /// 分析用户意图
    /// </summary>
    private string AnalyzeUserIntent(string query)
    {
        var queryLower = query.ToLower();
        var scores = new Dictionary<string, int>();

        foreach (var (intent, keywords) in _intentKeywords)
        {
            var score = keywords.Count(keyword => queryLower.Contains(keyword.ToLower()));
            if (score > 0)
            {
                scores[intent] = score;
            }
        }

        return scores.Any() ? scores.OrderByDescending(kvp => kvp.Value).First().Key : "general";
    }

    /// <summary>
    /// 计算 server-tool 组合与查询的匹配分数
    /// </summary>
    private (double score, string reasoning) CalculateMatchScore(string query, string intent, McpServerInfo server, McpToolInfo tool)
    {
        double score = 0;
        var reasons = new List<string>();

        // 1. 基于意图的服务器匹配
        var serverCategoryMatch = server.Categories.Any(cat => cat.Equals(intent, StringComparison.OrdinalIgnoreCase));
        if (serverCategoryMatch)
        {
            score += 30;
            reasons.Add($"Server category matches intent '{intent}'");
        }

        // 2. 基于标签的服务器匹配
        var intentKeywords = _intentKeywords.ContainsKey(intent) ? _intentKeywords[intent] : new List<string>();
        var serverTagMatches = server.Tags.Count(tag => intentKeywords.Any(kw => tag.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        score += serverTagMatches * 10;
        if (serverTagMatches > 0)
        {
            reasons.Add($"Server has {serverTagMatches} matching tags");
        }

        // 3. 基于工具名称的匹配
        var toolNameMatch = intentKeywords.Any(kw => tool.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
        if (toolNameMatch)
        {
            score += 25;
            reasons.Add("Tool name matches intent keywords");
        }

        // 4. 基于工具关键词的匹配
        var toolKeywordMatches = tool.Keywords.Count(kw => intentKeywords.Any(ikw => kw.Contains(ikw, StringComparison.OrdinalIgnoreCase)));
        score += toolKeywordMatches * 8;
        if (toolKeywordMatches > 0)
        {
            reasons.Add($"Tool has {toolKeywordMatches} matching keywords");
        }

        // 5. 基于工具描述的匹配
        if (!string.IsNullOrEmpty(tool.Description))
        {
            var descriptionMatches = intentKeywords.Count(kw => tool.Description.Contains(kw, StringComparison.OrdinalIgnoreCase));
            score += descriptionMatches * 5;
            if (descriptionMatches > 0)
            {
                reasons.Add($"Tool description has {descriptionMatches} keyword matches");
            }
        }

        // 6. 直接查询文本匹配
        var queryWords = Regex.Split(query.ToLower(), @"\W+").Where(w => w.Length > 2);
        var directMatches = queryWords.Count(word =>
            tool.Name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            tool.Description.Contains(word, StringComparison.OrdinalIgnoreCase));
        score += directMatches * 3;
        if (directMatches > 0)
        {
            reasons.Add($"Direct query text matches: {directMatches}");
        }

        // 7. 服务器健康分和优先级
        score += server.SuccessRate * 10;
        score += tool.Priority;

        // 8. 响应时间惩罚
        if (server.ResponseTime.HasValue)
        {
            var responseMs = server.ResponseTime.Value.TotalMilliseconds;
            score -= Math.Min(responseMs / 100, 10); // 最多扣10分
        }

        var reasoning = reasons.Any() ? string.Join("; ", reasons) : "General compatibility";
        return (score, reasoning);
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