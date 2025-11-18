// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 结果处理器 - 负责处理工具调用结果并生成用户友好的回复
/// </summary>
public class McpResultProcessor
{
    private readonly McpAIDecisionEngine? _aiDecisionEngine;
    private readonly ILogger<McpResultProcessor>? _logger;

    public McpResultProcessor(IChatClient? chatClient = null, ILogger<McpResultProcessor>? logger = null)
    {
        _aiDecisionEngine = chatClient != null ? new McpAIDecisionEngine(chatClient, logger) : null;
        _logger = logger;
    }

    /// <summary>
    /// 处理工具调用结果，使用 LLM 生成用户友好的回复
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    public async Task<McpResponse> ProcessInvocationResultAsync(
        string originalQuery,
        McpInvocationResult result,
        IChatClient? chatClient,
        Action<string>? thinkAreaCallback,
        CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            return new McpResponse
            {
                Answer = $"Tool invocation failed: {result.Error}",
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }

        // 如果没有 LLM，返回原始数据
        if (chatClient == null)
        {
            thinkAreaCallback?.Invoke("⚠️ No AI model available, returning raw JSON data");
            return new McpResponse
            {
                Answer = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions { WriteIndented = true }),
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }

        try
        {
            thinkAreaCallback?.Invoke("🧠 Requesting AI model to analyze and process results...");

            var extractedAnswer = await _aiDecisionEngine!.AnalyzeResultAsync(originalQuery, result, "结果提取", cancellationToken)
                ?? "Unable to extract answer from tool result.";

            thinkAreaCallback?.Invoke("✅ AI processing complete, formatting final answer...");

            // 获取原始 JSON 数据并组合回答
            var rawJson = SerializeResultData(result);
            var combinedAnswer = $"{extractedAnswer}\n\n---------\n{rawJson}";

            return new McpResponse
            {
                Answer = combinedAnswer,
                Source = $"{result.RoutingInfo?.SelectedServer.Name}.{result.RoutingInfo?.SelectedTool.Name}",
                RawResult = result
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing result with LLM: {ex.Message}");
            thinkAreaCallback?.Invoke($"⚠️ AI processing error, trying simple text extraction: {ex.Message}");

            // 降级到简单的文本提取
            var fallbackAnswer = await ExtractSimpleAnswerAsync(originalQuery, result, chatClient, thinkAreaCallback, cancellationToken);
            return new McpResponse
            {
                Answer = fallbackAnswer,
                Source = result.RoutingInfo?.SelectedServer.Name ?? "Unknown",
                RawResult = result
            };
        }
    }

    /// <summary>
    /// 从 MCP 调用结果中提取并序列化数据
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private string SerializeResultData(McpInvocationResult result)
    {
        var structuredContent = result.Data?.GetType().GetProperty("StructuredContent")?.GetValue(result.Data, null);
        var dataToSerialize = structuredContent ?? result.Data;
        return JsonSerializer.Serialize(dataToSerialize, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// 智能答案提取（使用 AI 分析工具返回的数据）
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private async Task<string> ExtractSimpleAnswerAsync(
        string originalQuery,
        McpInvocationResult result,
        IChatClient? chatClient,
        Action<string>? thinkAreaCallback,
        CancellationToken cancellationToken)
    {
        if (result.Data == null)
        {
            return "The tool did not return any data.";
        }

        // 如果有 AI 决策引擎，尝试使用 AI 分析数据
        if (_aiDecisionEngine != null)
        {
            try
            {
                thinkAreaCallback?.Invoke("🔄 Attempting simple AI analysis...");

                var aiAnswer = await _aiDecisionEngine.AnalyzeResultAsync(originalQuery, result, "简单结果提取", cancellationToken);

                if (!string.IsNullOrEmpty(aiAnswer))
                {
                    _logger?.LogDebug("Successfully extracted answer using AI analysis");
                    thinkAreaCallback?.Invoke("✅ Simple AI analysis completed successfully");

                    var rawJson = SerializeResultData(result);
                    return $"{aiAnswer}\n\n--- API ---\n{rawJson}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"AI analysis failed, falling back to basic extraction: {ex.Message}");
                thinkAreaCallback?.Invoke($"⚠️ Simple AI analysis failed, falling back to basic extraction: {ex.Message}");
            }
        }

        // 降级到基本的数据提取
        thinkAreaCallback?.Invoke("📄 Using basic data extraction...");
        try
        {
            var json = SerializeResultData(result);
            thinkAreaCallback?.Invoke("✅ Basic data extraction completed");
            return $"Retrieved the following information:\n{json}";
        }
        catch
        {
            thinkAreaCallback?.Invoke("❌ Data extraction failed, returning raw data");
            return result.Data?.ToString() ?? "Unable to parse the returned data.";
        }
    }
}