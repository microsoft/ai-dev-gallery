// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 评分服务 - 负责计算服务器和工具的匹配分数
/// </summary>
public class McpScoringService
{
    private readonly ILogger? _logger;

    // 预定义的意图到工具的映射规则
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

    public McpScoringService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 分析用户意图
    /// </summary>
    public string AnalyzeUserIntent(string query)
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
    /// 计算 server-tool 组合与查询的匹配分数
    /// </summary>
    public (double score, string reasoning) CalculateMatchScore(string query, string intent, McpServerInfo server, McpToolInfo tool)
    {
        double score = 0;
        var reasons = new List<string>();
        var queryLower = query.ToLower();

        // 1. 服务器类型与意图的强匹配
        var intentKeywords = _intentKeywords.ContainsKey(intent) ? _intentKeywords[intent] : new List<string>();
        var serverTypeScore = CalculateServerTypeScore(server.Id, intent, intentKeywords);
        score += serverTypeScore;
        if (serverTypeScore > 0)
        {
            reasons.Add($"Server type match: {serverTypeScore:F1}");
        }

        // 2. 工具名称的精确匹配
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
        score += tool.Priority * 0.5;

        // 6. 服务器健康度调整
        score *= server.SuccessRate;

        // 7. 响应时间惩罚
        if (server.ResponseTime.HasValue)
        {
            var responseMs = server.ResponseTime.Value.TotalMilliseconds;
            var penalty = Math.Min(responseMs / 500, 0.3);
            score *= 1 - penalty;
        }

        var reasoning = reasons.Any() ? string.Join("; ", reasons) : "No specific match found";
        _logger?.LogDebug($"Score for {server.Name}.{tool.Name}: {score:F2} ({reasoning})");

        return (score, reasoning);
    }

    /// <summary>
    /// 简单的匹配分数计算（降级方案）
    /// </summary>
    public double CalculateSimpleMatchScore(string query, McpServerInfo server, McpToolInfo tool)
    {
        var score = 0.0;

        // 服务器名称匹配
        if (query.Contains("system") || query.Contains("系统") || query.Contains("电脑"))
        {
            if (server.Name.Contains("system-info"))
            {
                score += 10;
            }
        }

        if (query.Contains("file") || query.Contains("文件") || query.Contains("folder"))
        {
            if (server.Name.Contains("file-system"))
            {
                score += 10;
            }
        }

        if (query.Contains("setting") || query.Contains("设置") || query.Contains("配置"))
        {
            if (server.Name.Contains("settings"))
            {
                score += 10;
            }
        }

        // 工具名称匹配
        if (tool.Name.ToLowerInvariant().Contains("get") && (query.Contains("show") || query.Contains("display") || query.Contains("显示")))
        {
            score += 5;
        }

        return score;
    }

    /// <summary>
    /// 计算服务器类型匹配分数
    /// </summary>
    private double CalculateServerTypeScore(string serverId, string intent, List<string> intentKeywords)
    {
        var score = 0.0;

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
                score += 50;
            }
            else
            {
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

        if (toolNameLower.Contains(queryLower) || queryLower.Contains(toolNameLower))
        {
            score += 30;
        }

        foreach (var keyword in intentKeywords)
        {
            if (toolNameLower.Contains(keyword.ToLower()))
            {
                score += 20;
            }
        }

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

        foreach (var keyword in intentKeywords)
        {
            if (descriptionLower.Contains(keyword.ToLower()))
            {
                score += 8;
            }
        }

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

            foreach (var intentKeyword in intentKeywords)
            {
                if (toolKeywordLower.Contains(intentKeyword.ToLower()) ||
                    intentKeyword.ToLower().Contains(toolKeywordLower))
                {
                    score += 10;
                }
            }

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
    public Dictionary<string, object> ExtractParameters(string query, string intent, McpToolInfo tool)
    {
        var parameters = new Dictionary<string, object>();

        if (tool.InputSchema?.ContainsKey("properties") == true)
        {
            var queryLower = query.ToLower();

            // 提取数字参数
            var numbers = Regex.Matches(query, @"\d+").Cast<Match>().Select(m => m.Value).ToList();
            if (numbers.Any())
            {
                parameters["value"] = numbers.First();
            }

            // 提取路径参数
            var pathPattern = @"[A-Za-z]:\\[^<>:""|?*\n\r]*";
            var pathMatch = Regex.Match(query, pathPattern);
            if (pathMatch.Success)
            {
                parameters["path"] = pathMatch.Value;
            }
        }

        return parameters;
    }
}