// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.Samples.MCP.Models;

/// <summary>
/// 意图识别响应模型
/// </summary>
public class IntentClassificationResponse
{
    [JsonPropertyName("need_tool")]
    public bool NeedTool { get; set; }

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
/// 服务器排名模型
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

/// <summary>
/// 工具调用计划响应模型
/// </summary>
public class ToolInvocationPlanResponse
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "call_tool";

    [JsonPropertyName("server_id")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();

    [JsonPropertyName("timeout_ms")]
    public int TimeoutMs { get; set; } = 120000;

    [JsonPropertyName("retries")]
    public int Retries { get; set; } = 1;
}

/// <summary>
/// 路由步骤结果
/// </summary>
public class RoutingStepResult<T>
    where T : class
{
    public T? Result { get; set; }
    public bool Success => Result != null;
    public string? ErrorMessage { get; set; }
    public double Confidence { get; set; }
}