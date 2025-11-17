// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 工具调用服务
/// </summary>
public class McpInvocationService
{
    private readonly McpDiscoveryService _discoveryService;
    private readonly ILogger<McpInvocationService>? _logger;

    public McpInvocationService(McpDiscoveryService discoveryService, ILogger<McpInvocationService>? logger = null)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// 执行路由决策，调用相应的 MCP 工具
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [RequiresDynamicCode()]
    public async Task<McpInvocationResult> InvokeToolAsync(RoutingDecision decision, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogInformation($"Invoking tool {decision.SelectedTool.Name} on server {decision.SelectedServer.Name}");

            // 获取服务器客户端
            var client = _discoveryService.GetServerClient(decision.SelectedServer.Id);
            if (client == null)
            {
                return new McpInvocationResult
                {
                    IsSuccess = false,
                    Error = $"No active connection to server {decision.SelectedServer.Name}",
                    ErrorCode = "MCP_NO_CONNECTION",
                    ExecutionTime = stopwatch.Elapsed,
                    RoutingInfo = decision
                };
            }

            // 准备工具调用参数
            var toolCallRequest = new
            {
                name = decision.SelectedTool.Name,
                arguments = decision.Parameters ?? new Dictionary<string, object>()
            };

            _logger?.LogDebug($"Calling tool with parameters: {JsonSerializer.Serialize(toolCallRequest)}");

            // 执行工具调用
            var result = await CallToolWithTimeoutAsync(client, toolCallRequest, TimeSpan.FromSeconds(30), cancellationToken);

            stopwatch.Stop();

            // 更新服务器统计信息
            UpdateServerStats(decision.SelectedServer, true, stopwatch.Elapsed);

            return new McpInvocationResult
            {
                IsSuccess = true,
                Data = result,
                ExecutionTime = stopwatch.Elapsed,
                RoutingInfo = decision
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            UpdateServerStats(decision.SelectedServer, false, stopwatch.Elapsed);

            return new McpInvocationResult
            {
                IsSuccess = false,
                Error = "Tool invocation was cancelled",
                ErrorCode = "MCP_CANCELLED",
                ExecutionTime = stopwatch.Elapsed,
                RoutingInfo = decision
            };
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            UpdateServerStats(decision.SelectedServer, false, stopwatch.Elapsed);

            return new McpInvocationResult
            {
                IsSuccess = false,
                Error = "Tool invocation timed out",
                ErrorCode = "MCP_TIMEOUT",
                ExecutionTime = stopwatch.Elapsed,
                RoutingInfo = decision
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            UpdateServerStats(decision.SelectedServer, false, stopwatch.Elapsed);

            _logger?.LogError($"Error invoking tool {decision.SelectedTool.Name}: {ex.Message}");

            return new McpInvocationResult
            {
                IsSuccess = false,
                Error = ex.Message,
                ErrorCode = "MCP_INVOCATION_ERROR",
                ExecutionTime = stopwatch.Elapsed,
                RoutingInfo = decision
            };
        }
    }

    /// <summary>
    /// 带超时的工具调用
    /// </summary>
    private async Task<object?> CallToolWithTimeoutAsync(McpClientWrapper client, object toolCallRequest, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // 使用正确的 MCP SDK API
            var toolName = GetToolName(toolCallRequest);
            var parameters = GetToolParameters(toolCallRequest);

            var response = await client.CallToolAsync(toolName, parameters, combinedCts.Token);
            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Tool call timed out after {timeout.TotalSeconds} seconds");
        }
    }

    /// <summary>
    /// 更新服务器统计信息
    /// </summary>
    private void UpdateServerStats(McpServerInfo server, bool success, TimeSpan executionTime)
    {
        try
        {
            // 更新响应时间
            server.ResponseTime = executionTime;
            server.LastUsed = DateTime.Now;

            // 更新成功率 (简单的移动平均)
            const double alpha = 0.1; // 学习率
            if (success)
            {
                server.SuccessRate = server.SuccessRate * (1 - alpha) + alpha;
            }
            else
            {
                server.SuccessRate = server.SuccessRate * (1 - alpha);
            }

            _logger?.LogDebug($"Updated stats for {server.Name}: SuccessRate={server.SuccessRate:F2}, ResponseTime={executionTime.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error updating server stats: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量调用多个工具（用于复杂查询）
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<McpInvocationResult>> InvokeToolsAsync(List<RoutingDecision> decisions, CancellationToken cancellationToken = default)
    {
        var tasks = decisions.Select(decision => InvokeToolAsync(decision, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return new List<McpInvocationResult>(results);
    }

    /// <summary>
    /// 健康检查 - 测试与服务器的连接
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<bool> HealthCheckAsync(string serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _discoveryService.GetServerClient(serverId);
            if (client == null)
            {
                return false;
            }

            // 尝试列出工具作为健康检查
            var tools = await client.ListToolsAsync(cancellationToken);
            return tools != null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Health check failed for server {serverId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取服务器状态摘要
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Dictionary<string, object>> GetServerStatusAsync(string serverId, CancellationToken cancellationToken = default)
    {
        var status = new Dictionary<string, object>();

        try
        {
            var servers = _discoveryService.GetConnectedServers();
            var server = servers.FirstOrDefault(s => s.Id == serverId);

            if (server == null)
            {
                status["connected"] = false;
                status["error"] = "Server not found";
                return status;
            }

            var isHealthy = await HealthCheckAsync(serverId, cancellationToken);
            var tools = _discoveryService.GetServerTools(serverId);

            status["connected"] = isHealthy;
            status["server_name"] = server.Name;
            status["tool_count"] = tools.Count;
            status["success_rate"] = server.SuccessRate;
            status["last_used"] = server.LastUsed;
            status["response_time_ms"] = server.ResponseTime?.TotalMilliseconds ?? 0;
            status["enabled"] = server.IsEnabled;
        }
        catch (Exception ex)
        {
            status["connected"] = false;
            status["error"] = ex.Message;
        }

        return status;
    }

    /// <summary>
    /// 从工具调用请求中提取工具名称
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private string GetToolName(object toolCallRequest)
    {
        try
        {
            var json = JsonSerializer.Serialize(toolCallRequest);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // 提取失败时返回空字符串
        }

        return string.Empty;
    }

    /// <summary>
    /// 从工具调用请求中提取参数
    /// </summary>
    private Dictionary<string, object> GetToolParameters(object toolCallRequest)
    {
        try
        {
            var json = JsonSerializer.Serialize(toolCallRequest);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("arguments", out var argsElement))
            {
                var parameters = new Dictionary<string, object>();

                foreach (var property in argsElement.EnumerateObject())
                {
                    parameters[property.Name] = property.Value.ToString() ?? string.Empty;
                }

                return parameters;
            }
        }
        catch
        {
            // 提取失败时返回空字典
        }

        return new Dictionary<string, object>();
    }
}