// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.Samples.MCP.Models;

#region Core MCP Models

/// <summary>
/// MCP 服务器信息
/// </summary>
public class McpServerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
    public string[] Categories { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public DateTime LastUsed { get; set; }
    public TimeSpan? ResponseTime { get; set; }
    public double SuccessRate { get; set; } = 1.0;
    public double HealthScore { get; set; } = 1.0;
    public TimeSpan AverageResponseTime { get; set; } = TimeSpan.FromMilliseconds(100);
}

/// <summary>
/// MCP 工具信息
/// </summary>
public class McpToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> InputSchema { get; set; } = new();
    public string ServerId { get; set; } = string.Empty;
    public string[] Keywords { get; set; } = [];
    public int Priority { get; set; } = 0;
}

#endregion

#region Execution Results

/// <summary>
/// MCP 工具调用结果
/// </summary>
public class McpInvocationResult
{
    public bool IsSuccess { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public McpRoutingResult? RoutingInfo { get; set; }
}

/// <summary>
/// 用户响应
/// </summary>
public class McpResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public McpInvocationResult? RawResult { get; set; }
}

#endregion

#region Routing Models

/// <summary>
/// 路由决策结果
/// </summary>
public class McpRoutingResult
{
    public McpServerInfo SelectedServer { get; set; } = null!;
    public McpToolInfo SelectedTool { get; set; } = null!;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public bool RequiresClarification { get; set; }
    public string ClarificationQuestion { get; set; } = string.Empty;
    public McpInvocationPlan? InvocationPlan { get; set; }
}

/// <summary>
/// 工具调用计划
/// </summary>
public class McpInvocationPlan
{
    public string Action { get; set; } = "call_tool";
    public string ServerId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
    public int TimeoutMs { get; set; } = 120000;
    public int Retries { get; set; } = 1;
}

#endregion

#region AI Routing Models

/// <summary>
/// 意图识别响应模型
/// </summary>
public class IntentClassificationResponse
{
    [JsonPropertyName("need_tool")]
    public bool NeedTool { get; set; }

    [JsonPropertyName("need_tool_reason")]
    public string NeedToolReason { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public string[] Keywords { get; set; } = [];

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// 服务器选择响应模型
/// </summary>
public class ServerSelectionResponse
{
    [JsonPropertyName("chosen_server_id")]
    public string ChosenServerId { get; set; } = string.Empty;

    [JsonPropertyName("ranking")]
    public ServerRanking[] Ranking { get; set; } = [];

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// 服务器评分排名
/// </summary>
public class ServerRanking
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("reasons")]
    public string[] Reasons { get; set; } = [];
}

/// <summary>
/// 工具选择响应模型
/// </summary>
public class ToolSelectionResponse
{
    [JsonPropertyName("chosen_tool_name")]
    public string ChosenToolName { get; set; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public string[] Alternatives { get; set; } = [];

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// 参数提取响应模型
/// </summary>
public class ArgumentExtractionResponse
{
    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();

    [JsonPropertyName("missing")]
    public string[] Missing { get; set; } = [];

    [JsonPropertyName("clarify_question")]
    public string ClarifyQuestion { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

#endregion

#region Utilities

/// <summary>
/// 路由步骤结果包装器
/// </summary>
public class RoutingStepResult<T>
    where T : class
{
    public T? Result { get; set; }
    
    public bool Success => Result != null && string.IsNullOrEmpty(ErrorMessage);
    
    public string? ErrorMessage { get; set; }
    
    public double Confidence { get; set; }
    
    public TimeSpan ExecutionTime { get; set; }
}

#endregion