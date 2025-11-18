// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP AI决策引擎 - 负责使用AI模型进行路由决策的各个步骤
/// </summary>
public class McpAIDecisionEngine : McpAIServiceBase
{
    public McpAIDecisionEngine(IChatClient? chatClient, ILogger? logger = null)
        : base(chatClient, logger)
    {
    }

    /// <summary>
    /// 步骤1: 使用AI进行意图识别
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [RequiresDynamicCode("Uses JSON serialization for AI response parsing which may require dynamic code generation")]
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [RequiresDynamicCode("Uses JSON serialization for AI response parsing which may require dynamic code generation")]
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [RequiresDynamicCode("Uses JSON serialization for AI response parsing which may require dynamic code generation")]
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [RequiresDynamicCode("Uses JSON serialization for AI response parsing which may require dynamic code generation")]
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    /// 步骤6: 使用AI分析和提取工具调用结果
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<string?> AnalyzeResultAsync(
        string originalQuery, 
        McpInvocationResult result, 
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = McpPromptTemplateManager.GetResultExtractionSystemPrompt();
        var userPrompt = McpPromptTemplateManager.FormatResultExtractionUserPrompt(originalQuery, result);

        return await CallAIWithTextResponseAsync(systemPrompt, userPrompt, stepName, cancellationToken);
    }
}