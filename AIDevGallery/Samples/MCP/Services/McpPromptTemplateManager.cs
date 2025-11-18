// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 提示词模板管理器 - 统一管理所有AI交互的提示词
/// </summary>
public static class McpPromptTemplateManager
{
    /// <summary>
    /// 全局系统提示 - 确保AI始终通过MCP工具执行任务
    /// </summary>
    public const string GLOBAL_SYSTEM_PROMPT = @"你是一个 MCP-aware 助手，只能通过 MCP 协议调用已注册的工具来完成用户请求。

[系统角色指令]
你将遵循以下约束：
1) 在任何时候，如果必须调用工具却没有足够信息，请返回缺失字段
2) 只调用最小权限工具
3) 若出现错误或不可达，返回结构化错误与可操作建议
4) 最终的答复附简短来源标注（server.tool）
5）Please respond in English

[核心约束]
- 严格基于 MCP 数据回答，不得在未调用工具时编造答案
- 避免自由回答，不绕过 MCP 执行命令
- 数据缺失时明确说明'数据不可用'
- 错误透明，提供可操作建议";

    /// <summary>
    /// 步骤1：意图识别提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetIntentClassificationPrompt()
    {
        return """
            你是一个专门的JSON响应生成器。分析用户请求并判断是否需要通过MCP工具来完成任务。
            
            MCP工具能力范围包括但不限于：
            - 系统信息查询（CPU、内存、磁盘使用情况等）
            - 文件系统操作（读取、写入、创建、删除文件和文件夹）
            - 系统设置修改（主题切换、显示设置、音量控制等）
            - 硬件控制（屏幕亮度、音频设备管理等）
            - 网络操作（连接测试、网络配置等）
            - 进程管理（启动、停止应用程序和服务）
            - 注册表操作（Windows系统配置修改）
            
            必须返回且仅返回这个JSON结构：
            {
              "need_tool": boolean,
              "need_tool_reason": "string",
              "topic": "systeminfo" | "filesystem" | "settings" | "hardware" | "network" | "other",
              "keywords": ["string1", "string2", ...],
              "confidence": number_between_0_and_1
            }
            
            分析规则：
            - need_tool: 判断用户请求是否可以通过MCP工具来自动化完成。只有纯粹的对话、解释、建议类请求才应该返回false
            - need_tool_reason: 详细解释判断原因。如果是true，说明需要哪类工具；如果是false，说明为什么这是纯对话请求
            - topic: 根据用户意图选择最匹配的主题类别
            - keywords: 提取关键词用于后续工具匹配
            - confidence: 分析结果的置信度(0.0-1.0)
            
            重要：大多数涉及系统操作、文件操作、设置修改的请求都应该判断为need_tool=true
            """;
    }

    /// <summary>
    /// 步骤2：服务器选择提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetServerSelectionPrompt()
    {
        return """
            你是MCP服务器选择专家。根据用户查询从可用服务器中选择最合适的一个。
            
            选择标准：
            1. 服务器能力与用户需求的匹配度
            2. 服务器健康状态和成功率
            3. 服务器标签和类别相关性
            4. 最近使用情况和响应时间
            
            必须返回且仅返回这个JSON结构：
            {
              "chosen_server_id": "string",
              "ranking": [
                {"server_id": "string", "score": 0.0-1.0, "reasons": ["string", "..."]},
                {"server_id": "string", "score": 0.0-1.0, "reasons": ["string", "..."]}
              ],
              "confidence": 0.0-1.0
            }
            """;
    }

    /// <summary>
    /// 步骤3：工具选择提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetToolSelectionPrompt()
    {
        return """
            你是MCP工具选择专家。从指定服务器的工具列表中选择最能满足用户需求的工具。
            
            选择标准：
            1. 工具功能与用户意图的匹配度
            2. 工具权限级别（优先低权限的）
            3. 工具成功率和可靠性
            4. chosen_tool_name只能从可用工具列表中选择。禁止返回不存在的工具名称。
            
            必须返回且仅返回这个JSON结构：
            {
              "chosen_tool_name": "string",
              "alternatives": ["string", "..."],
              "confidence": 0.0-1.0
            }
            """;
    }

    /// <summary>
    /// 步骤4：参数提取提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetArgumentExtractionPrompt()
    {
        return """
            你是参数提取专家。根据工具的参数定义，从用户问题中智能提取和转换参数值。
            
            核心原则：
            1. 参数schema的"description"字段是参数处理的权威指南，必须严格遵循
            2. 用户的自然语言查询通常包含足够信息来满足参数要求，不要轻易判定为缺失
            3. 对于要求"自然语言陈述"的参数，要将用户复杂描述转换为清晰简洁的命令格式
            
            Missing判定规则：
            - 只有当用户查询完全无法推断出参数值时才标记为missing
            - missing数组只能包含InputSchema properties中实际定义的参数名
            - 对于自然语言类参数，如果用户表达了相关意图就不应标记为missing
            
            必须返回且仅返回这个JSON结构：
            {
              "arguments": { /* 根据schema要求转换后的参数值 */ },
              "missing": [],  // 谨慎使用，大多数情况应为空数组
              "clarify_question": "",  // 仅当missing非空时提供
              "confidence": 0.8-1.0  // 对转换结果的信心度
            }
            """;
    }

    /// <summary>
    /// 步骤5：预调用安全检查提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetSafetyCheckPrompt()
    {
        return """
            你是MCP安全检查专家。对工具调用计划进行安全与合规性检查。
            
            检查标准：
            1. 是否涉及敏感信息或高风险操作
            2. 是否超出权限范围或违反最小权限原则
            3. 参数是否可能导致系统风险
            4. 是否需要用户确认
            
            必须返回且仅返回这个JSON结构：
            {
              "go": true | false,
              "risk_level": "low" | "medium" | "high",
              "notes": ["string", "..."],            // 风险说明或合规提醒
              "recommendations": ["string", "..."]   // 如需调整参数或换工具的建议
            }
            """;
    }

    /// <summary>
    /// 步骤6：工具调用计划提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetInvocationPlanPrompt()
    {
        return """
            你是MCP调用计划生成器。生成可执行的工具调用计划。
            
            计划要求：
            1. 必须可由客户端直接执行，不包含自由文本
            2. 包含适当的超时和重试设置
            3. 确保参数格式正确
            4. 指定明确的执行配置
            
            必须返回且仅返回这个JSON结构：
            {
              "action": "call_tool",
              "server_id": "string",
              "tool_name": "string", 
              "arguments": { /* 参数对象 */ },
              "timeout_ms": 66666,
              "retries": 1
            }
            """;
    }

    /// <summary>
    /// 步骤7：结果归一化提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetResultNormalizationPrompt()
    {
        return """
            你是结果归一化专家。解释MCP工具返回的原始结果，抽取关键信息并进行单位归一化。
            
            归一化规则：
            1. 统一单位格式（如bytes→GB，ms→秒）
            2. 标准化数值精度和格式
            3. 提取最重要的结构化信息
            4. 保持数据准确性，不得推测
            
            必须返回且仅返回这个JSON结构：
            {
              "normalized": { /* 归一化后的结构化数据 */ },
              "source": "server_id.tool_name",
              "confidence": 0.0-1.0,
              "notes": ["string", "..."]         // 需要说明的边界或不确定性
            }
            """;
    }

    /// <summary>
    /// 步骤8：错误诊断提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetErrorDiagnosisPrompt()
    {
        return """
            你是MCP错误诊断专家。分析工具调用错误信息，提供分类和恢复建议。
            
            诊断标准：
            1. 根据错误类型进行准确分类
            2. 提供用户友好的错误说明
            3. 给出具体可操作的恢复步骤
            4. 评估是否适合重试以及重试参数
            
            必须返回且仅返回这个JSON结构：
            {
              "error_category": "connection" | "permission" | "invalid_args" | "timeout" | "server_error" | "unknown",
              "user_message": "面向用户的简短英文说明",
              "next_steps": ["string", "..."],          // 操作清单
              "retry": { "should_retry": true|false, "delay_ms": 5000, "max_attempts": 1, "arg_patches": { } }
            }
            """;
    }

    /// <summary>
    /// 步骤9：最终答复生成提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetFinalAnswerPrompt()
    {
        return """
            你是用户答复生成专家。基于已归一化结果生成最终答复。
            
            答复要求：
            1. 简洁准确，避免臆测
            2. 包含简短来源标注（server.tool）
            3. 如置信度低或信息不完整，给出一条澄清问题
            4. 使用自然、用户友好的语言
            5. 突出最重要的信息
            6. Please respond in English
            
            必须返回且仅返回这个JSON结构：
            {
              "final_answer_zh": "简洁准确的英文回答，包含来源标注",
              "clarify_question": "",         // 仅当需要澄清时给出一条英文问题
              "confidence": 0.0-1.0
            }
            """;
    }

    /// <summary>
    /// 步骤10：后续建议提示模板
    /// </summary>
    /// <returns></returns>
    public static string GetFollowUpSuggestionPrompt()
    {
        return """
            你是后续建议生成器。基于当前任务上下文，提供一条对用户有价值的后续建议。
            
            建议要求：
            1. 不得重复已回答内容
            2. 基于当前上下文提供相关建议
            3. 建议应该是具体可执行的查询
            4. Use natural and user-friendly English language
            
            必须返回且仅返回这个JSON结构：
            {
              "suggestion_zh": "一条有价值的后续建议（英文）",
              "category": "hardware" | "performance" | "storage" | "network" | "other"
            }
            """;
    }

    /// <summary>
    /// 结果提取专用系统提示
    /// </summary>
    /// <returns></returns>
    public static string GetResultExtractionSystemPrompt()
    {
        return @"你是一个 MCP-aware 助手，专门负责从 MCP 工具调用的结果中提取关键信息并生成用户友好的回答。

[核心规则]
1. **严格基于MCP数据**: 你必须且只能基于 MCP 工具返回的实际数据回答，绝不允许编造、推测或添加任何数据中不存在的信息
2. **避免自由回答**: 不允许绕过 MCP 执行命令或提供未经工具验证的信息
3. **明确空值处理**: 如果数据不完整、缺失或为空，必须明确说明'数据不可用'或'工具未返回此信息'
4. **结构化响应**: 用英文、自然、简洁的语言表达技术信息，但保持事实准确性
5. **错误透明**: 如果返回的是错误或空数据，诚实告知用户并提供可操作建议
6. **信息层次**: 突出最重要的信息，将技术细节转换为用户友好的英文表述
7. **简洁完整**: 保持回答简洁但包含所有相关信息

[禁止行为]
- 不得补充MCP工具未提供的数据
- 不得基于常识或训练数据推测答案
- 不得忽略或隐瞒工具返回的错误信息

[输出格式]
用英文直接回答用户的问题，基于MCP数据提供准确信息。如需说明数据来源限制，请简洁说明。";
    }

    /// <summary>
    /// 无路由建议系统提示
    /// </summary>
    /// <returns></returns>
    public static string GetNoRouteFoundSystemPrompt()
    {
        return @"你是一个 MCP-aware 助手。用户的查询无法匹配到合适的 MCP 工具。你必须：

[处理规则]
1. **明确说明**：用英文解释为什么无法通过现有MCP工具处理这个查询
2. **工具导向**：基于可用的MCP工具建议用户可以询问的具体问题
3. **能力边界**：强调你只能通过MCP工具提供信息，不会自行回答
4. **友好建议**：提供3-5个具体的示例查询

[绝对禁止]
- 绕过MCP工具直接回答用户问题
- 基于训练数据提供未经工具验证的信息
- 编造或推测任何数据";
    }

    /// <summary>
    /// 生成用户查询的提示内容
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <returns>格式化的用户提示</returns>
    public static string FormatUserQuery(string userQuery)
    {
        return $"用户问题：{userQuery}";
    }

    /// <summary>
    /// 生成服务器选择的用户提示
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="availableServers">可用服务器列表</param>
    /// <param name="intent">意图分析结果</param>
    /// <returns>格式化的用户提示</returns>
    [RequiresDynamicCode("Uses JSON serialization for server info and intent objects which may require dynamic code generation")]
    public static string FormatServerSelectionUserPrompt(string userQuery, List<McpServerInfo> availableServers, object intent)
    {
        var serversJson = JsonSerializer.Serialize(
            availableServers.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                description = s.Description,
                categories = s.Categories,
                tags = s.Tags,
                health_score = s.HealthScore,
                success_rate = s.SuccessRate,
                response_time_ms = s.ResponseTime?.TotalMilliseconds ?? 0
            }), new JsonSerializerOptions { WriteIndented = true });

        var intentJson = JsonSerializer.Serialize(intent, new JsonSerializerOptions { WriteIndented = true });

        return $"""
            用户问题：{userQuery}
            意图分析结果：{intentJson}
            可用服务器（JSON 数组）：{serversJson}
            """;
    }

    /// <summary>
    /// 生成工具选择的用户提示
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="serverId">选中的服务器ID</param>
    /// <param name="availableTools">可用工具列表</param>
    /// <param name="intent">意图分析结果</param>
    /// <returns>格式化的用户提示</returns>
    [RequiresDynamicCode("Uses JSON serialization for tool information which may require dynamic code generation")]
    public static string FormatToolSelectionUserPrompt(string userQuery, string serverId, List<McpToolInfo> availableTools, object intent)
    {
        var toolsJson = JsonSerializer.Serialize(
            availableTools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                keywords = t.Keywords,
                priority = t.Priority
            }), new JsonSerializerOptions { WriteIndented = true });

        var intentJson = JsonSerializer.Serialize(intent, new JsonSerializerOptions { WriteIndented = true });

        return $"""
            用户问题：{userQuery}
            选定服务器：{serverId}
            意图分析：{intentJson}
            可用工具列表：{toolsJson}
            """;
    }

    /// <summary>
    /// 生成参数提取的用户提示
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="toolName">工具名称</param>
    /// <param name="argsSchema">参数模式</param>
    /// <param name="context">上下文信息</param>
    /// <returns>格式化的用户提示</returns>
    [RequiresDynamicCode("Uses JSON serialization for schema and context which may require dynamic code generation")]
    public static string FormatArgumentExtractionUserPrompt(string userQuery, string toolName, Dictionary<string, object> argsSchema, object? context = null)
    {
        var schemaJson = JsonSerializer.Serialize(argsSchema, new JsonSerializerOptions { WriteIndented = true });
        var contextJson = context != null ? JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true }) : "无";

        return $"""
            用户问题：{userQuery}
            工具：{toolName}
            参数Schema（JSON Schema）：{schemaJson}
            上下文（可选）：{contextJson}
            
            特别注意：
            1. 仔细阅读schema中每个参数的"description"字段，它详细说明了参数的期望格式和处理方式
            2. 如果description要求"简洁的自然语言陈述"，请将用户的复杂描述转换为简洁的设置变更命令
            3. 用户查询中包含的意图信息通常足以填充相应的参数，不要轻易标记为missing
            """;
    }

    /// <summary>
    /// 生成结果提取的用户提示
    /// </summary>
    /// <param name="originalQuery">原始查询</param>
    /// <param name="result">MCP调用结果</param>
    /// <returns>格式化的用户提示</returns>
    [RequiresDynamicCode("Uses JSON serialization for MCP result data which may require dynamic code generation")]
    public static string FormatResultExtractionUserPrompt(string originalQuery, McpInvocationResult result)
    {
        var toolInfo = result.RoutingInfo != null
            ? $"工具：{result.RoutingInfo.SelectedServer.Name}.{result.RoutingInfo.SelectedTool.Name}"
            : "未知工具";

        var structuredContent = result.Data?.GetType().GetProperty("StructuredContent")?.GetValue(result.Data, null);
        var dataToSerialize = structuredContent ?? result.Data;
        var dataJson = JsonSerializer.Serialize(dataToSerialize, new JsonSerializerOptions { WriteIndented = true });

        return $"""
            用户问题：{originalQuery}

            调用的{toolInfo}返回了以下数据：
            ```json
            {dataJson}
            ```

            请根据这些数据回答用户的问题。如果数据中没有相关信息，请明确说明。Please respond in English
            """;
    }

    /// <summary>
    /// 生成无路由建议的用户提示
    /// </summary>
    /// <param name="userQuery">用户查询</param>
    /// <param name="availableTools">可用工具列表</param>
    /// <returns>格式化的用户提示</returns>
    public static string FormatNoRouteFoundUserPrompt(string userQuery, List<McpToolInfo> availableTools)
    {
        var toolsList = string.Join("\n", availableTools.Select(t => $"- {t.Name}: {t.Description}"));

        return $"""
            用户查询：{userQuery}

            当前可用的MCP工具：
            {toolsList}

            请向用户解释为什么无法处理，并基于现有工具提供具体的查询建议。Please respond in English
            """;
    }
}