// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP AI调用基础服务 - 提供通用的AI调用和响应处理功能
/// </summary>
public abstract class McpAIServiceBase
{
    protected readonly IChatClient? _chatClient;
    protected readonly ILogger? _logger;

    protected McpAIServiceBase(IChatClient? chatClient, ILogger? logger = null)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether 是否有可用的AI客户端
    /// </summary>
    public bool HasAIClient => _chatClient != null;

    /// <summary>
    /// 通用AI调用方法，使用结构化输出获取JSON响应
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    protected async Task<T?> CallAIWithJsonResponseAsync<T>(string systemPrompt, string userPrompt, string stepName, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_chatClient == null)
        {
            _logger?.LogWarning($"No AI client available for {stepName}");
            return null;
        }

        try
        {
            // 合并全局系统提示和步骤特定提示
            var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[当前步骤: {stepName}]\n{systemPrompt}";

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, combinedSystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // 方法1: 使用结构化输出
            try
            {
                var structuredResponse = await _chatClient.GetResponseAsync<T>(
                    messages,
                    options: new ChatOptions
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()
                    },
                    cancellationToken: cancellationToken);

                if (structuredResponse != null && structuredResponse.TryGetResult(out T? result) && result != null)
                {
                    _logger?.LogDebug($"✅ {stepName} structured output parsed successfully");
                    return result;
                }
            }
            catch (Exception structuredEx)
            {
                _logger?.LogWarning(structuredEx, $"⚠️ Structured output failed for {stepName}, falling back to text parsing");

                // 方法2: 降级到文本解析
                var chatOptions = new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Json,
                    Temperature = 0.1f
                };

                var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                var aiResponse = response.Text ?? string.Empty;

                _logger?.LogDebug($"🤖 {stepName} AI Response: {aiResponse}");

                var cleanedJson = CleanJsonResponse(aiResponse);
                if (!string.IsNullOrEmpty(cleanedJson))
                {
                    var result = JsonSerializer.Deserialize<T>(cleanedJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });

                    if (result != null)
                    {
                        _logger?.LogDebug($"✅ {stepName} fallback parsing successful");
                        return result;
                    }
                }
            }

            _logger?.LogWarning($"⚠️ Could not parse {stepName} response with any method");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"❌ Error during {stepName}");
            return null;
        }
    }

    /// <summary>
    /// 通用AI文本响应调用方法
    /// </summary>
    protected async Task<string?> CallAIWithTextResponseAsync(string systemPrompt, string userPrompt, string stepName, CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            _logger?.LogWarning($"No AI client available for {stepName}");
            return null;
        }

        try
        {
            // 合并全局系统提示和步骤特定提示
            var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[当前步骤: {stepName}]\n{systemPrompt}";

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, combinedSystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages, null, cancellationToken);
            var aiResponse = response?.Text?.Trim();

            if (!string.IsNullOrEmpty(aiResponse))
            {
                _logger?.LogDebug($"✅ {stepName} text response received successfully");
                return aiResponse;
            }

            _logger?.LogWarning($"⚠️ Empty response received for {stepName}");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"❌ Error during {stepName}");
            return null;
        }
    }

    /// <summary>
    /// 清理和验证JSON响应
    /// </summary>
    protected string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var cleaned = response.Trim();

        // 移除markdown代码块标记
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7);
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3);
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }

        cleaned = cleaned.Trim();

        // 找到第一个 { 和最后一个 }
        var jsonStart = cleaned.IndexOf('{');
        var jsonEnd = cleaned.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd >= jsonStart)
        {
            var jsonPart = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);

            // 验证JSON格式
            try
            {
                using var doc = JsonDocument.Parse(jsonPart);
                return jsonPart;
            }
            catch (JsonException)
            {
                _logger?.LogWarning($"Invalid JSON detected: {jsonPart.Substring(0, Math.Min(100, jsonPart.Length))}...");
                return string.Empty;
            }
        }

        return string.Empty;
    }
}