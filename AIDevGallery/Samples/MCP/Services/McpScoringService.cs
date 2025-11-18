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

    // 缓存计算结果以提升性能
    private readonly ConcurrentDictionary<string, string> _intentCache = new();
    private readonly ConcurrentDictionary<string, double> _scoreCache = new();

    // 预编译的正则表达式
    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex PathRegex = new(@"[A-Za-z]:\\[^<>:""|?*\n\r]*", RegexOptions.Compiled);
    private static readonly Regex WordBoundaryRegex = new(@"\W+", RegexOptions.Compiled);

    // 评分权重常量 - 标准化到相似范围
    private const double ExactMatchScore = 10.0;
    private const double WordMatchScore = 8.0;
    private const double PartialMatchScore = 5.0;
    private const double FuzzyMatchScore = 2.0;

    // 主要评分因素的基础分数 - 标准化到0-100范围
    private const double MaxServerTypeScore = 40.0;    // 降低服务器类型的过度影响
    private const double MaxToolNameScore = 35.0;      // 工具名称匹配很重要
    private const double MaxKeywordScore = 25.0;       // 关键词匹配重要性
    private const double MaxDescriptionScore = 20.0;   // 提升描述匹配的权重
    private const double MaxActionScore = 15.0;

    // 调整因子
    private const double PriorityMultiplier = 2.0;     // 提升优先级影响
    private const double MaxResponseTimePenalty = 0.2; // 降低响应时间惩罚
    private const double ResponseTimeThreshold = 1000.0; // 提升阈值到1秒
    private const double MinHealthThreshold = 0.5;     // 最低健康度阈值

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

    public McpScoringService(McpScoringConfiguration? config = null)
    {
        _config = config ?? new McpScoringConfiguration();
    }

    /// <summary>
    /// 分析用户意图
    /// </summary>
    /// <returns></returns>
    public string AnalyzeUserIntent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "general";
        }

        // 检查缓存
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

        // 缓存结果
        _intentCache.TryAdd(cacheKey, bestIntent);

        return bestIntent;
    }

    /// <summary>
    /// 计算意图匹配分数
    /// </summary>
    private double CalculateIntentScore(string queryLower, HashSet<string> queryWords, List<string> keywords)
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
                    score += ExactMatchScore; // 完整匹配
                }
                else if (queryWords.Contains(keywordLower))
                {
                    score += WordMatchScore; // 单词匹配
                }
                else
                {
                    score += PartialMatchScore; // 部分匹配
                }
            }

            // 模糊匹配（词干）
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

    /// <summary>
    /// 计算 server-tool 组合与查询的匹配分数
    /// </summary>
    /// <returns></returns>
    public (double score, string reasoning) CalculateMatchScore(string query, string intent, McpServerInfo server, McpToolInfo tool)
    {
        if (string.IsNullOrWhiteSpace(query) || server == null || tool == null)
        {
            return (0, "Invalid input parameters");
        }

        // 检查缓存
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

            // 1. 服务器类型与意图的强匹配
            var intentKeywords = _intentKeywords.ContainsKey(intent) ? _intentKeywords[intent] : new List<string>();
            var serverTypeScore = CalculateServerTypeScore(server.Id, intent, intentKeywords);
            var normalizedServerScore = Math.Min(serverTypeScore, MaxServerTypeScore) * _config.ServerTypeWeight;
            score += normalizedServerScore;
            if (serverTypeScore > 0)
            {
                reasons.Add($"Server type match: {normalizedServerScore:F1}");
            }

            // 2. 工具名称的精确匹配
            var toolNameScore = CalculateToolNameScore(tool.Name, queryLower, intentKeywords);
            var normalizedToolScore = Math.Min(toolNameScore, MaxToolNameScore) * _config.ToolNameWeight;
            score += normalizedToolScore;
            if (toolNameScore > 0)
            {
                reasons.Add($"Tool name relevance: {normalizedToolScore:F1}");
            }

            // 3. 工具描述匹配
            var descriptionScore = CalculateDescriptionScore(tool.Description, queryLower, intentKeywords);
            var normalizedDescScore = Math.Min(descriptionScore, MaxDescriptionScore) * _config.DescriptionWeight;
            score += normalizedDescScore;
            if (descriptionScore > 0)
            {
                reasons.Add($"Description match: {normalizedDescScore:F1}");
            }

            // 4. 关键词匹配
            var keywordScore = CalculateKeywordScore(tool.Keywords, queryLower, intentKeywords);
            var normalizedKeywordScore = Math.Min(keywordScore, MaxKeywordScore) * _config.KeywordWeight;
            score += normalizedKeywordScore;
            if (keywordScore > 0)
            {
                reasons.Add($"Keyword match: {normalizedKeywordScore:F1}");
            }

            // 5. 工具优先级加成（线性加分，不是乘法）
            var priorityBonus = tool.Priority * PriorityMultiplier * _config.PriorityWeight;
            score += priorityBonus;
            if (priorityBonus > 0)
            {
                reasons.Add($"Priority bonus: {priorityBonus:F1}");
            }

            // 6. 服务器健康度调整（只有低于阈值才惩罚）
            if (server.SuccessRate < MinHealthThreshold)
            {
                var healthPenalty = (MinHealthThreshold - server.SuccessRate) * 50 * _config.HealthWeight;
                score -= healthPenalty;
                reasons.Add($"Health penalty: -{healthPenalty:F1}");
            }
            else if (server.SuccessRate > 0.9) // 高健康度奖励
            {
                var healthBonus = (server.SuccessRate - 0.9) * 20 * _config.HealthWeight;
                score += healthBonus;
                reasons.Add($"Health bonus: +{healthBonus:F1}");
            }

            // 7. 响应时间调整（改为线性惩罚）
            if (server.ResponseTime.HasValue)
            {
                var responseMs = server.ResponseTime.Value.TotalMilliseconds;
                if (responseMs > ResponseTimeThreshold)
                {
                    var penalty = Math.Min((responseMs - ResponseTimeThreshold) / ResponseTimeThreshold * 20, 30) * _config.PerformanceWeight;
                    score -= penalty;
                    reasons.Add($"Response time penalty: -{penalty:F1}");
                }
                else if (responseMs < 200) // 快速响应奖励
                {
                    var bonus = (200 - responseMs) / 200 * 10 * _config.PerformanceWeight;
                    score += bonus;
                    reasons.Add($"Fast response bonus: +{bonus:F1}");
                }
            }

            // 应用最终分数限制和验证
            score = Math.Max(0, Math.Min(score, _config.MaxScore));

            var reasoning = reasons.Any() ? string.Join("; ", reasons) : "No specific match found";

            // 缓存结果
            _scoreCache.TryAdd(cacheKey, score);

            return (score, reasoning);
        }
        catch (Exception ex)
        {
            return (0, $"Calculation error: {ex.Message}");
        }
    }

    /// <summary>
    /// 简单的匹配分数计算（降级方案）
    /// </summary>
    /// <returns></returns>
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

            // 完全匹配意图类型
            if (serverTypes.Contains(intent))
            {
                score += MaxServerTypeScore * 0.8; // 32分
            }

            // 部分匹配关键词
            var matchCount = intentKeywords.Count(kw =>
                serverTypes.Any(st => kw.Contains(st, StringComparison.OrdinalIgnoreCase)));
            score += Math.Min(matchCount * 8, MaxServerTypeScore * 0.6); // 最多24分

            // 服务器专业度加分
            var specialtyBonus = serverTypes.Length > 3 ? 4 : serverTypes.Length * 2;
            score += specialtyBonus;
        }
        else
        {
            // 未知服务器类型，给予基础分数
            score += 5;
        }

        return Math.Min(score, MaxServerTypeScore);
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
    /// <returns></returns>
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

            // 提取数字参数
            ExtractNumericParameters(query, parameters);

            // 提取路径参数
            ExtractPathParameters(query, parameters);

            // 提取文件扩展名
            ExtractFileExtensions(query, parameters);

            // 提取布尔参数
            ExtractBooleanParameters(queryLower, parameters);

            // 提取时间参数
            ExtractTimeParameters(query, parameters);

            // 根据意图提取特定参数
            ExtractIntentSpecificParameters(queryLower, intent, parameters);

        }
        catch (Exception ex)
        {
            // Silently handle parameter extraction errors
        }

        return parameters;
    }

    /// <summary>
    /// 提取数字参数
    /// </summary>
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

            // 尝试解析为整数或双精度
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

    /// <summary>
    /// 提取路径参数
    /// </summary>
    private void ExtractPathParameters(string query, Dictionary<string, object> parameters)
    {
        var pathMatch = PathRegex.Match(query);
        if (pathMatch.Success)
        {
            parameters["path"] = pathMatch.Value;
        }

        // Unix 路径
        var unixPathPattern = @"/[\w\-_./]+";
        var unixPathMatch = Regex.Match(query, unixPathPattern);
        if (unixPathMatch.Success && unixPathMatch.Value.Length > 2)
        {
            parameters["unixPath"] = unixPathMatch.Value;
        }
    }

    /// <summary>
    /// 提取文件扩展名
    /// </summary>
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

    /// <summary>
    /// 提取布尔参数
    /// </summary>
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

    /// <summary>
    /// 提取时间参数
    /// </summary>
    private void ExtractTimeParameters(string query, Dictionary<string, object> parameters)
    {
        // 匹配时间格式如 "12:30", "2:45 PM"
        var timePattern = @"\b(\d{1,2}):(\d{2})(?:\s*(AM|PM))?\b";
        var timeMatch = Regex.Match(query, timePattern, RegexOptions.IgnoreCase);

        if (timeMatch.Success)
        {
            parameters["time"] = timeMatch.Value;
        }

        // 匹配持续时间如 "5 minutes", "2 hours"
        var durationPattern = @"\b(\d+)\s+(second|minute|hour|day|week|month|year)s?\b";
        var durationMatch = Regex.Match(query, durationPattern, RegexOptions.IgnoreCase);

        if (durationMatch.Success)
        {
            parameters["duration"] = durationMatch.Value;
        }
    }

    /// <summary>
    /// 根据意图提取特定参数
    /// </summary>
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

    /// <summary>
    /// 批量计算多个工具的匹配分数
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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

    /// <summary>
    /// 清理缓存
    /// </summary>
    public void ClearCache()
    {
        _intentCache.Clear();
        _scoreCache.Clear();
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns></returns>
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
    /// <summary>
    /// Gets or sets 服务器类型匹配权重（默认1.2，因为服务器选择是基础）
    /// </summary>
    public double ServerTypeWeight { get; set; } = 1.2;

    /// <summary>
    /// Gets or sets 工具名称匹配权重（默认1.5，工具名称通常很明确）
    /// </summary>
    public double ToolNameWeight { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets 描述匹配权重（默认1.0，提升描述的重要性）
    /// </summary>
    public double DescriptionWeight { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets 关键词匹配权重（默认1.3，关键词很重要）
    /// </summary>
    public double KeywordWeight { get; set; } = 1.3;

    /// <summary>
    /// Gets or sets 优先级权重（默认0.8，避免过度依赖优先级）
    /// </summary>
    public double PriorityWeight { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets 健康度权重（默认1.0）
    /// </summary>
    public double HealthWeight { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets 性能权重（默认0.6，性能不应该是决定因素）
    /// </summary>
    public double PerformanceWeight { get; set; } = 0.6;

    /// <summary>
    /// Gets or sets 最大分数限制（默认200，更合理的范围）
    /// </summary>
    public double MaxScore { get; set; } = 200.0;

    /// <summary>
    /// Gets or sets 缓存大小限制
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets 缓存过期时间
    /// </summary>
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets a value indicating whether 是否启用调试模式（详细日志）
    /// </summary>
    public bool DebugMode { get; set; } = false;

    /// <summary>
    /// Gets or sets 最低可接受分数（低于此分数的匹配将被过滤）
    /// </summary>
    public double MinAcceptableScore { get; set; } = 10.0;
}