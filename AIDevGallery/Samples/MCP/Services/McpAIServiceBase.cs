// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
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

    protected McpAIServiceBase(IChatClient? chatClient)
    {
        _chatClient = chatClient;
    }

    public bool HasAIClient => _chatClient != null;

    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    protected async Task<T?> CallAIWithJsonResponseAsync<T>(string systemPrompt, string userPrompt, string stepName, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_chatClient == null)
        {
            return null;
        }

        try
        {
            var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[当前步骤: {stepName}]\n{systemPrompt}";

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, combinedSystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            // 使用结构化输出
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
                    return result;
                }
            }
            catch
            {
                // 降级到文本解析
                var chatOptions = new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Json,
                    Temperature = 0.1f
                };

                var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                var aiResponse = response.Text ?? string.Empty;

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
                        return result;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    protected async Task<string?> CallAIWithTextResponseAsync(string systemPrompt, string userPrompt, string stepName, CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            return null;
        }

        try
        {
            var combinedSystemPrompt = $"{McpPromptTemplateManager.GLOBAL_SYSTEM_PROMPT}\n\n[当前步骤: {stepName}]\n{systemPrompt}";

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, combinedSystemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages, null, cancellationToken);
            return response?.Text?.Trim();
        }
        catch
        {
            return null;
        }
    }

    protected string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var cleaned = response.Trim();

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

        var jsonStart = cleaned.IndexOf('{');
        var jsonEnd = cleaned.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd >= jsonStart)
        {
            var jsonPart = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);

            try
            {
                using var doc = JsonDocument.Parse(jsonPart);
                return jsonPart;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        return string.Empty;
    }
}