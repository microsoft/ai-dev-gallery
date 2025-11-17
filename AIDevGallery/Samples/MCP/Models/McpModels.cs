// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace AIDevGallery.Samples.MCP.Models;

/// <summary>
/// MCP Server 信息
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
}

/// <summary>
/// MCP Tool 信息
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

/// <summary>
/// 路由决策结果
/// </summary>
public class RoutingDecision
{
    public McpServerInfo SelectedServer { get; set; } = null!;
    public McpToolInfo SelectedTool { get; set; } = null!;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public bool RequiresClarification { get; set; }
    public string ClarificationQuestion { get; set; } = string.Empty;
    public object? InvocationPlan { get; set; }
}

/// <summary>
/// MCP 调用结果
/// </summary>
public class McpInvocationResult
{
    public bool IsSuccess { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public RoutingDecision? RoutingInfo { get; set; }
}

/// <summary>
/// 最终的用户回复
/// </summary>
public class McpResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public McpInvocationResult? RawResult { get; set; }
}