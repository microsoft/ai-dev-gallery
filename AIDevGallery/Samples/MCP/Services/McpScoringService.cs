// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 评分服务 - 负责计算服务器和工具的匹配分数
/// </summary>
public class McpScoringService
{
    private readonly McpScoringConfiguration _config;

    private readonly ConcurrentDictionary<string, string> _intentCache = new();
    private readonly ConcurrentDictionary<string, double> _scoreCache = new();

    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex PathRegex = new(@"[A-Za-z]:\\[^<>:""|?*\n\r]*", RegexOptions.Compiled);
    private static readonly Regex WordBoundaryRegex = new(@"\W+", RegexOptions.Compiled);

    private const double ExactMatchScore = 10.0;
    private const double WordMatchScore = 8.0;
    private const double PartialMatchScore = 5.0;
    private const double FuzzyMatchScore = 2.0;

    private const double MaxServerTypeScore = 40.0;
    private const double MaxToolNameScore = 35.0;
    private const double MaxKeywordScore = 25.0;
    private const double MaxDescriptionScore = 20.0;
    private const double MaxActionScore = 15.0;

    private const double PriorityMultiplier = 2.0;
    private const double MaxResponseTimePenalty = 0.2;
    private const double ResponseTimeThreshold = 1000.0;
    private const double MinHealthThreshold = 0.5;

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

    public McpScoringService(McpScoringConfiguration? config = null)
    {
        _config = config ?? new McpScoringConfiguration();
    }

    public string AnalyzeUserIntent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "general";
        }

        var cacheKey = query.ToLowerInvariant();
        if (_intentCache.TryGetValue(cacheKey, out var cachedIntent))
        {
            return cachedIntent;
        }

        var queryLower = cacheKey;
        var queryWords = WordBoundaryRegex.Split(queryLower).Where(w => w.Length > 1).ToHashSet();
        var scores = new Dictionary<string, double>();

        foreach (var (intent, keywords) in _intentKeywords)
        {
            var score = CalculateIntentScore(queryLower, queryWords, keywords);
            if (score > 0)
            {
                scores[intent] = score;
            }
        }

        var bestIntent = scores.Any()
            ? scores.OrderByDescending(kvp => kvp.Value).First().Key
            : "general";

        _intentCache.TryAdd(cacheKey, bestIntent);

        return bestIntent;
    }

    private double CalculateIntentScore(string queryLower, HashSet<string> queryWords, List<string> keywords)
    {
        double score = 0;

        foreach (var keyword in keywords)
        {
            var keywordLower = keyword.ToLower();

            if (queryLower.Contains(keywordLower))
            {
                if (queryLower == keywordLower)
                {
                    score += ExactMatchScore;
                }
                else if (queryWords.Contains(keywordLower))
                {
                    score += WordMatchScore;
                }
                else
                {
                    score += PartialMatchScore;
                }
            }

            if (keywordLower.Length > 3)
            {
                var stem = keywordLower.Substring(0, Math.Min(keywordLower.Length - 1, 4));
                if (queryLower.Contains(stem))
                {
                    score += FuzzyMatchScore;
                }
            }
        }

        return score;
    }

    public (double score, string reasoning) CalculateMatchScore(string query, string intent, McpServerInfo server, McpToolInfo tool)
    {
        if (string.IsNullOrWhiteSpace(query) || server == null || tool == null)
        {
            return (0, "Invalid input parameters");
        }

        var cacheKey = $"{query}:{intent}:{server.Id}:{tool.Name}";
        if (_scoreCache.TryGetValue(cacheKey, out var cachedScore))
        {
            return (cachedScore, "Cached result");
        }

        try
        {
            double score = 0;
            var reasons = new List<string>();
            var queryLower = query.ToLowerInvariant();

            var intentKeywords = _intentKeywords.ContainsKey(intent) ? _intentKeywords[intent] : new List<string>();
            var serverTypeScore = CalculateServerTypeScore(server.Id, intent, intentKeywords);
            var normalizedServerScore = Math.Min(serverTypeScore, MaxServerTypeScore) * _config.ServerTypeWeight;
            score += normalizedServerScore;
            if (serverTypeScore > 0)
            {
                reasons.Add($"Server type match: {normalizedServerScore:F1}");
            }

            var toolNameScore = CalculateToolNameScore(tool.Name, queryLower, intentKeywords);
            var normalizedToolScore = Math.Min(toolNameScore, MaxToolNameScore) * _config.ToolNameWeight;
            score += normalizedToolScore;
            if (toolNameScore > 0)
            {
                reasons.Add($"Tool name relevance: {normalizedToolScore:F1}");
            }

            var descriptionScore = CalculateDescriptionScore(tool.Description, queryLower, intentKeywords);
            var normalizedDescScore = Math.Min(descriptionScore, MaxDescriptionScore) * _config.DescriptionWeight;
            score += normalizedDescScore;
            if (descriptionScore > 0)
            {
                reasons.Add($"Description match: {normalizedDescScore:F1}");
            }

            var keywordScore = CalculateKeywordScore(tool.Keywords, queryLower, intentKeywords);
            var normalizedKeywordScore = Math.Min(keywordScore, MaxKeywordScore) * _config.KeywordWeight;
            score += normalizedKeywordScore;
            if (keywordScore > 0)
            {
                reasons.Add($"Keyword match: {normalizedKeywordScore:F1}");
            }

            var priorityBonus = tool.Priority * PriorityMultiplier * _config.PriorityWeight;
            score += priorityBonus;
            if (priorityBonus > 0)
            {
                reasons.Add($"Priority bonus: {priorityBonus:F1}");
            }

            if (server.SuccessRate < MinHealthThreshold)
            {
                var healthPenalty = (MinHealthThreshold - server.SuccessRate) * 50 * _config.HealthWeight;
                score -= healthPenalty;
                reasons.Add($"Health penalty: -{healthPenalty:F1}");
            }
            else if (server.SuccessRate > 0.9)
            {
                var healthBonus = (server.SuccessRate - 0.9) * 20 * _config.HealthWeight;
                score += healthBonus;
                reasons.Add($"Health bonus: +{healthBonus:F1}");
            }

            if (server.ResponseTime.HasValue)
            {
                var responseMs = server.ResponseTime.Value.TotalMilliseconds;
                if (responseMs > ResponseTimeThreshold)
                {
                    var penalty = Math.Min((responseMs - ResponseTimeThreshold) / ResponseTimeThreshold * 20, 30) * _config.PerformanceWeight;
                    score -= penalty;
                    reasons.Add($"Response time penalty: -{penalty:F1}");
                }
                else if (responseMs < 200)
                {
                    var bonus = (200 - responseMs) / 200 * 10 * _config.PerformanceWeight;
                    score += bonus;
                    reasons.Add($"Fast response bonus: +{bonus:F1}");
                }
            }

            score = Math.Max(0, Math.Min(score, _config.MaxScore));

            var reasoning = reasons.Any() ? string.Join("; ", reasons) : "No specific match found";

            _scoreCache.TryAdd(cacheKey, score);

            return (score, reasoning);
        }
        catch (Exception ex)
        {
            return (0, $"Calculation error: {ex.Message}");
        }
    }

    public double CalculateSimpleMatchScore(string query, McpServerInfo server, McpToolInfo tool)
    {
        var score = 0.0;

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

        if (tool.Name.ToLowerInvariant().Contains("get") && (query.Contains("show") || query.Contains("display") || query.Contains("显示")))
        {
            score += 5;
        }

        return score;
    }

    private double CalculateServerTypeScore(string serverId, string intent, List<string> intentKeywords)
    {
        var score = 0.0;

        var serverTypeMatches = new Dictionary<string, string[]>
        {
            ["system-info"] = ["system", "memory", "cpu", "storage", "hardware", "process"],
            ["file-system"] = ["file", "storage"],
            ["settings"] = ["settings"],
            ["network"] = ["network"],
            ["database"] = ["storage", "file"],
            ["monitoring"] = ["system", "process", "network"]
        };

        if (serverTypeMatches.ContainsKey(serverId))
        {
            var serverTypes = serverTypeMatches[serverId];

            if (serverTypes.Contains(intent))
            {
                score += MaxServerTypeScore * 0.8;
            }

            var matchCount = intentKeywords.Count(kw =>
                serverTypes.Any(st => kw.Contains(st, StringComparison.OrdinalIgnoreCase)));
            score += Math.Min(matchCount * 8, MaxServerTypeScore * 0.6);

            var specialtyBonus = serverTypes.Length > 3 ? 4 : serverTypes.Length * 2;
            score += specialtyBonus;
        }
        else
        {
            score += 5;
        }

        return Math.Min(score, MaxServerTypeScore);
    }

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

    public Dictionary<string, object> ExtractParameters(string query, string intent, McpToolInfo tool)
    {
        var parameters = new Dictionary<string, object>();

        if (tool.InputSchema?.ContainsKey("properties") != true || string.IsNullOrWhiteSpace(query))
        {
            return parameters;
        }

        try
        {
            var queryLower = query.ToLowerInvariant();

            ExtractNumericParameters(query, parameters);
            ExtractPathParameters(query, parameters);
            ExtractFileExtensions(query, parameters);
            ExtractBooleanParameters(queryLower, parameters);
            ExtractTimeParameters(query, parameters);
            ExtractIntentSpecificParameters(queryLower, intent, parameters);
        }
        catch (Exception ex)
        {
        }

        return parameters;
    }

    private void ExtractNumericParameters(string query, Dictionary<string, object> parameters)
    {
        var numbers = NumberRegex.Matches(query).Cast<Match>().Select(m => m.Value).ToList();

        if (numbers.Any())
        {
            parameters["value"] = numbers.First();

            if (numbers.Count > 1)
            {
                parameters["values"] = numbers.ToArray();
            }

            if (int.TryParse(numbers.First(), out var intValue))
            {
                parameters["intValue"] = intValue;
            }
            else if (double.TryParse(numbers.First(), out var doubleValue))
            {
                parameters["doubleValue"] = doubleValue;
            }
        }
    }

    private void ExtractPathParameters(string query, Dictionary<string, object> parameters)
    {
        var pathMatch = PathRegex.Match(query);
        if (pathMatch.Success)
        {
            parameters["path"] = pathMatch.Value;
        }

        var unixPathPattern = @"/[\w\-_./]+";
        var unixPathMatch = Regex.Match(query, unixPathPattern);
        if (unixPathMatch.Success && unixPathMatch.Value.Length > 2)
        {
            parameters["unixPath"] = unixPathMatch.Value;
        }
    }

    private void ExtractFileExtensions(string query, Dictionary<string, object> parameters)
    {
        var extensionPattern = @"\.([a-zA-Z]{1,4})\b";
        var extensionMatches = Regex.Matches(query, extensionPattern);

        if (extensionMatches.Any())
        {
            var extensions = extensionMatches.Cast<Match>()
                .Select(m => m.Groups[1].Value.ToLowerInvariant())
                .Distinct()
                .ToArray();

            parameters["fileExtensions"] = extensions;
            parameters["fileExtension"] = extensions.First();
        }
    }

    private void ExtractBooleanParameters(string queryLower, Dictionary<string, object> parameters)
    {
        var positiveKeywords = new[] { "yes", "true", "enable", "on", "是", "开启", "启用" };
        var negativeKeywords = new[] { "no", "false", "disable", "off", "否", "关闭", "禁用" };

        var hasPositive = positiveKeywords.Any(k => queryLower.Contains(k));
        var hasNegative = negativeKeywords.Any(k => queryLower.Contains(k));

        if (hasPositive && !hasNegative)
        {
            parameters["enabled"] = true;
        }
        else if (hasNegative && !hasPositive)
        {
            parameters["enabled"] = false;
        }
    }

    private void ExtractTimeParameters(string query, Dictionary<string, object> parameters)
    {
        // 匹配时间格式如 "12:30", "2:45 PM"
        var timePattern = @"\b(\d{1,2}):(\d{2})(?:\s*(AM|PM))?\b";
        var timeMatch = Regex.Match(query, timePattern, RegexOptions.IgnoreCase);

        if (timeMatch.Success)
        {
            parameters["time"] = timeMatch.Value;
        }

        var durationPattern = @"\b(\d+)\s+(second|minute|hour|day|week|month|year)s?\b";
        var durationMatch = Regex.Match(query, durationPattern, RegexOptions.IgnoreCase);

        if (durationMatch.Success)
        {
            parameters["duration"] = durationMatch.Value;
        }
    }

    private void ExtractIntentSpecificParameters(string queryLower, string intent, Dictionary<string, object> parameters)
    {
        switch (intent)
        {
            case "file":
                if (queryLower.Contains("recursive") || queryLower.Contains("递归"))
                {
                    parameters["recursive"] = true;
                }

                break;

            case "memory":
                if (queryLower.Contains("percent") || queryLower.Contains("percentage") || queryLower.Contains("百分比"))
                {
                    parameters["showPercentage"] = true;
                }

                break;

            case "network":
                if (queryLower.Contains("verbose") || queryLower.Contains("详细"))
                {
                    parameters["verbose"] = true;
                }

                break;
        }
    }

    public async Task<List<(McpServerInfo server, McpToolInfo tool, double score, string reasoning)>> CalculateMatchScoresAsync(
        string query,
        string intent,
        IEnumerable<(McpServerInfo server, McpToolInfo tool)> candidates)
    {
        if (string.IsNullOrWhiteSpace(query) || candidates == null)
        {
            return new List<(McpServerInfo, McpToolInfo, double, string)>();
        }

        var tasks = candidates.Select(candidate => Task.Run(() =>
        {
            var (score, reasoning) = CalculateMatchScore(query, intent, candidate.server, candidate.tool);
            return (candidate.server, candidate.tool, score, reasoning);
        }));

        var results = await Task.WhenAll(tasks);
        return results.OrderByDescending(r => r.score).ToList();
    }

    public void ClearCache()
    {
        _intentCache.Clear();
        _scoreCache.Clear();
    }

    public (int intentCacheSize, int scoreCacheSize) GetCacheStats()
    {
        return (_intentCache.Count, _scoreCache.Count);
    }
}

/// <summary>
/// MCP 评分服务配置
/// </summary>
public class McpScoringConfiguration
{
    public double ServerTypeWeight { get; set; } = 1.2;
    public double ToolNameWeight { get; set; } = 1.5;
    public double DescriptionWeight { get; set; } = 1.0;
    public double KeywordWeight { get; set; } = 1.3;
    public double PriorityWeight { get; set; } = 0.8;
    public double HealthWeight { get; set; } = 1.0;
    public double PerformanceWeight { get; set; } = 0.6;
    public double MaxScore { get; set; } = 200.0;
    public int MaxCacheSize { get; set; } = 1000;
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(30);
    public bool DebugMode { get; set; } = false;
    public double MinAcceptableScore { get; set; } = 10.0;
}