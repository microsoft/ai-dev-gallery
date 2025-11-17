// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP AI调用服务 - 负责与AI模型的交互和响应解析
/// </summary>
public class McpAIService
{
    private readonly IChatClient? _chatClient;
    private readonly ILogger<McpAIService>? _logger;

    public McpAIService(IChatClient? chatClient, ILogger<McpAIService>? logger = null)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// 是否有可用的AI客户端
    /// </summary>
    public bool HasAIClient => _chatClient != null;

    /// <summary>
    /// 步骤1: 使用AI进行意图识别
    /// </summary>
    public async Task<RoutingStepResult<IntentClassificationResponse>> ClassifyIntentAsync(string userQuery)
    {
        var systemPrompt = McpPromptTemplateManager.GetIntentClassificationPrompt();
        var userPrompt = McpPromptTemplateManager.FormatUserQuery(userQuery);

        var result = await CallAIWithJsonResponseAsync<IntentClassificationResponse>(systemPrompt, userPrompt, "意图识别");
        return new RoutingStepResult<IntentClassificationResponse>
        {
            Result = result,
            Confidence = result?.Confidence ?? 0
        };
    }

    /// <summary>
    /// 步骤2: 使用AI选择最佳服务器
    /// </summary>
    public async Task<RoutingStepResult<ServerSelectionResponse>> SelectServerAsync(
        string userQuery, 
        List<McpServerInfo> servers, 
        IntentClassificationResponse intent)
    {
        var systemPrompt = McpPromptTemplateManager.GetServerSelectionPrompt();
        var userPrompt = McpPromptTemplateManager.FormatServerSelectionUserPrompt(userQuery, servers, intent);

        var result = await CallAIWithJsonResponseAsync<ServerSelectionResponse>(systemPrompt, userPrompt, "服务器选择");
        return new RoutingStepResult<ServerSelectionResponse>
        {
            Result = result,
            Confidence = result?.Confidence ?? 0
        };
    }

    /// <summary>
    /// 步骤3: 使用AI选择最佳工具
    /// </summary>
    public async Task<RoutingStepResult<ToolSelectionResponse>> SelectToolAsync(
        string userQuery, 
        McpServerInfo server, 
        List<McpToolInfo> tools, 
        IntentClassificationResponse intent)
    {
        var systemPrompt = McpPromptTemplateManager.GetToolSelectionPrompt();
        var userPrompt = McpPromptTemplateManager.FormatToolSelectionUserPrompt(userQuery, server.Id, tools, intent);

        var result = await CallAIWithJsonResponseAsync<ToolSelectionResponse>(systemPrompt, userPrompt, "工具选择");
        return new RoutingStepResult<ToolSelectionResponse>
        {
            Result = result,
            Confidence = result?.Confidence ?? 0
        };
    }

    /// <summary>
    /// 步骤4: 使用AI提取工具参数
    /// </summary>
    public async Task<RoutingStepResult<ArgumentExtractionResponse>> ExtractArgumentsAsync(
        string userQuery, 
        McpToolInfo tool, 
        IntentClassificationResponse intent)
    {
        var systemPrompt = McpPromptTemplateManager.GetArgumentExtractionPrompt();
        var userPrompt = McpPromptTemplateManager.FormatArgumentExtractionUserPrompt(userQuery, tool.Name, tool.InputSchema, intent);

        var result = await CallAIWithJsonResponseAsync<ArgumentExtractionResponse>(systemPrompt, userPrompt, "参数提取");
        return new RoutingStepResult<ArgumentExtractionResponse>
        {
            Result = result,
            Confidence = result?.Confidence ?? 0
        };
    }

    /// <summary>
    /// 步骤5: 使用AI生成工具调用计划
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    public async Task<RoutingStepResult<ToolInvocationPlanResponse>> CreateInvocationPlanAsync(
        string userQuery, 
        McpServerInfo server, 
        McpToolInfo tool, 
        Dictionary<string, object> arguments)
    {
        var systemPrompt = McpPromptTemplateManager.GetInvocationPlanPrompt();

        var userPrompt = $"""
            用户问题：{userQuery}
            已选 server/tool/args：
            - server: {server.Id}
            - tool: {tool.Name}
            - args: {JsonSerializer.Serialize(arguments)}
            """;

        var result = await CallAIWithJsonResponseAsync<ToolInvocationPlanResponse>(systemPrompt, userPrompt, "调用计划");
        return new RoutingStepResult<ToolInvocationPlanResponse>
        {
            Result = result,
            Confidence = result != null ? 1.0 : 0.0 // 调用计划成功就是100%置信度
        };
    }

    /// <summary>
    /// 通用AI调用方法，使用结构化输出获取JSON响应
    /// </summary>
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    private async Task<T?> CallAIWithJsonResponseAsync<T>(string systemPrompt, string userPrompt, string stepName)
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
                    });

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

                var response = await _chatClient.GetResponseAsync(messages, chatOptions);
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
    /// 清理和验证JSON响应
    /// </summary>
    private string CleanJsonResponse(string response)
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