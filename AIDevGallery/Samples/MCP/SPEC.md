# AI Dev Gallery MCP Client 规范文档

## 项目概述

本项目实现了一个基于Windows平台的智能MCP（Model Context Protocol）Client，能够自动发现本地MCP服务器、智能路由用户查询到最合适的工具，并使用LLM对结果进行智能提取和自然语言展示。

### 核心能力

- **智能服务器发现**：自动枚举和连接Windows系统中已注册的MCP服务器
- **AI驱动的路由**：使用多步骤AI决策流程自动选择最佳的服务器和工具
- **自然语言交互**：用户可以用自然语言提问，系统自动转换为MCP工具调用
- **智能结果提取**：LLM从工具返回的原始数据中提取关键信息，生成用户友好的回答
- **健康监控**：实时监控MCP服务器连接状态和工具调用成功率

### 技术栈

- **平台**：Windows, WinUI 3, .NET
- **MCP协议**：使用Microsoft Model Context Protocol Client SDK
- **AI集成**：Microsoft.Extensions.AI框架
- **UI框架**：WinUI 3 with XAML
- **架构模式**：服务分层架构 + MVVM

## 架构设计

### 系统架构图

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   UI Layer      │    │  Business Logic │    │  MCP Protocol   │
│   (XAML/C#)     │◄──►│    Services     │◄──►│    Clients      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │                       │
         v                       v                       v
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│    Messages     │    │  AI Chat Client │    │  MCP Servers    │
│   Collection    │    │ (LLM Integration)│    │  (Windows)      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### 核心服务组件

#### 1. McpManager - 主协调器
**职责**：统筹整个MCP交互流程
- 初始化各子服务
- 处理用户查询的端到端流程
- 协调路由、调用、结果处理

#### 2. McpDiscoveryService - 服务发现
**职责**：发现和管理MCP服务器连接
- 枚举Windows已注册的MCP服务器
- 建立并维护服务器连接
- 获取服务器工具列表
- 连接健康监控

#### 3. McpRoutingService - 智能路由
**职责**：基于AI的路由决策
- 多步骤AI决策流程（意图识别→服务器选择→工具选择→参数提取）
- 关键词匹配降级方案
- 候选项评分和排序

#### 4. McpInvocationService - 工具调用
**职责**：执行MCP工具调用
- 安全的工具调用执行
- 超时和重试机制
- 服务器统计信息维护

### 数据模型

#### 核心数据结构

```csharp
// 服务器信息
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
}

// 工具信息
public class McpToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> InputSchema { get; set; } = new();
    public string ServerId { get; set; } = string.Empty;
    public string[] Keywords { get; set; } = [];
    public int Priority { get; set; } = 0;
}

// 路由决策
public class RoutingDecision
{
    public McpServerInfo SelectedServer { get; set; } = null!;
    public McpToolInfo SelectedTool { get; set; } = null!;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public bool RequiresClarification { get; set; }
    public string ClarificationQuestion { get; set; } = string.Empty;
}

// 调用结果
public class McpInvocationResult
{
    public bool IsSuccess { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public RoutingDecision? RoutingInfo { get; set; }
}

// 最终用户响应
public class McpResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public McpInvocationResult? RawResult { get; set; }
}
```

## AI驱动的多步骤路由算法

### 完整流程

1. **意图识别** - 分析用户查询确定所需工具类型
2. **服务器选择** - 根据服务器能力和健康度选择最佳服务器
3. **工具选择** - 从选定服务器的工具列表中选择最匹配的工具
4. **参数提取** - 从用户查询中提取工具所需参数
5. **安全检查** - 评估操作风险和权限要求
6. **调用计划** - 生成可执行的工具调用配置
7. **结果解释** - 使用LLM从原始结果中提取用户友好的答案

### 每步骤的AI Prompt规范

#### 步骤1：意图识别
```json
{
  "need_tool": true|false,
  "topic": "systeminfo|filesystem|settings|hardware|network|other",
  "keywords": ["string1", "string2", ...],
  "confidence": 0.0-1.0
}
```

#### 步骤2：服务器选择
```json
{
  "chosen_server_id": "服务器ID",
  "ranking": [
    {"server_id": "ID", "score": 0.85, "reasons": ["原因1", "原因2"]},
    ...
  ],
  "confidence": 0.0-1.0
}
```

#### 步骤3：工具选择
```json
{
  "chosen_tool_name": "工具名称",
  "alternatives": ["备选工具1", "备选工具2"],
  "confidence": 0.0-1.0
}
```

#### 步骤4：参数提取
```json
{
  "arguments": {"参数名": "参数值", ...},
  "missing": ["缺失参数1", "缺失参数2"],
  "clarify_question": "澄清问题（仅当missing非空时）",
  "confidence": 0.0-1.0
}
```

#### 步骤5：工具调用计划
```json
{
  "action": "call_tool",
  "server_id": "服务器ID",
  "tool_name": "工具名称",
  "arguments": {"参数": "值"},
  "timeout_ms": 120000,
  "retries": 1
}
```

### 降级策略

当AI路由失败时，系统自动降级到基于关键词的简单匹配算法：

1. **关键词映射**：预定义意图到关键词的映射表
2. **评分计算**：基于服务器类型、工具名称、描述的匹配度计算分数
3. **健康度调整**：根据服务器成功率和响应时间调整最终分数

## 安全与权限控制

### 最小权限原则

系统实现了分层的权限控制：

1. **工具分类**：
   - 只读工具（get, list, info）- 自动执行
   - 写入工具（set, create, delete）- 需要确认
   - 系统工具（system, config）- 严格限制

2. **用户确认机制**：
   ```csharp
   private bool RequiresUserConfirmation(RoutingDecision decision)
   {
       var sensitiveCategories = new[] { "system", "file", "network", "settings" };
       var sensitiveToolPatterns = new[] { "delete", "modify", "write", "set" };
       var readOnlyToolPatterns = new[] { "get", "list", "show", "read", "info" };
       
       // 实现基于工具类型和操作类型的确认逻辑
   }
   ```

3. **错误处理**：
   - 连接错误（MCP_NO_CONNECTION）
   - 权限错误（MCP_PERMISSION_DENIED）
   - 参数错误（MCP_INVALID_ARGS）
   - 超时错误（MCP_TIMEOUT）

## LLM集成与提示工程

### 全局系统提示

```
你是一个 MCP-aware 助手，只能通过 MCP 协议调用已注册的工具来完成用户请求。
不得绕过 MCP 执行命令，也不得在未调用工具时编造答案。

约束条件：
1. 始终执行：意图识别→服务器选择→工具选择→参数生成→安全检查→工具调用→结果解释
2. 中间步骤输出严格JSON格式，不得包含解释性文字
3. 参数不足时返回单条中文澄清问题
4. 绝不泄露敏感信息，只调用最小权限工具
5. 最终回答使用中文，附简短来源标注
```

### 结果提取系统提示

```
你是一个 MCP-aware 助手，专门从 MCP 工具调用结果中提取关键信息。

核心规则：
1. 严格基于MCP数据回答，绝不编造信息
2. 避免自由回答，不绕过MCP执行命令
3. 数据缺失时明确说明'数据不可用'
4. 结构化响应，自然简洁表达
5. 错误透明，提供可操作建议
6. 突出重要信息，转换技术细节为用户友好表述

禁止行为：
- 补充MCP工具未提供的数据
- 基于常识推测答案
- 忽略或隐瞒工具返回的错误
```

## 预定义MCP服务器配置

### Windows内置MCP服务器

系统默认支持以下Windows MCP服务器：

#### 1. 系统信息服务器
```csharp
{
    Id = "system-info",
    Name = "System Information Server",
    Description = "Provides system hardware and software information",
    ExecutablePath = "odr.exe",
    Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server"],
    Categories = ["system", "hardware"],
    Tags = ["ram", "cpu", "disk", "memory", "hardware", "system", "info"]
}
```

#### 2. 文件系统服务器
```csharp
{
    Id = "file-system",
    Name = "File Operations Server", 
    Description = "File system operations and information",
    ExecutablePath = "odr.exe",
    Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"],
    Categories = ["filesystem", "files"],
    Tags = ["file", "directory", "path", "storage", "read", "write", "list"]
}
```

#### 3. 设置服务器
```csharp
{
    Id = "settings",
    Name = "Settings Server",
    Description = "Windows settings and configuration management",
    ExecutablePath = "odr.exe", 
    Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server"],
    Categories = ["settings", "configuration"],
    Tags = ["settings", "config", "windows", "preferences", "system"]
}
```

## 用户交互体验

### 典型使用场景

#### 场景1：系统信息查询
**用户输入**：`"这台电脑的RAM有多大？"`

**处理流程**：
1. 意图识别：`topic="systeminfo", keywords=["ram", "memory"]`
2. 服务器选择：选择`system-info`服务器
3. 工具选择：选择`get_memory_info`工具
4. 参数提取：无需参数
5. 工具调用：执行内存查询
6. 结果提取：`"这台电脑的RAM为16GB（来源：system-info.get_memory_info）"`

#### 场景2：文件操作
**用户输入**：`"显示Documents文件夹的内容"`

**处理流程**：
1. 意图识别：`topic="filesystem", keywords=["documents", "folder", "list"]`
2. 服务器选择：选择`file-system`服务器  
3. 工具选择：选择`list_directory`工具
4. 参数提取：`{"path": "Documents"}`
5. 工具调用：列出文件夹内容
6. 结果提取：格式化的文件列表

### UI/UX设计原则

1. **对话式界面**：模仿聊天应用的交互模式
2. **实时状态显示**：显示MCP连接状态和可用工具数量
3. **透明度**：在调试区域显示路由决策和执行详情
4. **错误友好**：提供清晰的错误信息和可操作建议
5. **确认机制**：高风险操作需要用户明确确认

### 可访问性

- **屏幕阅读器支持**：使用NarratorHelper提供语音反馈
- **键盘导航**：支持Tab导航和快捷键
- **高对比度模式**：适配系统主题设置

## 性能优化

### 连接池管理

- 复用MCP服务器连接，避免频繁建立/断开
- 实现连接健康检查和故障恢复
- 并发调用支持和连接限制

### 缓存策略

- 服务器工具列表缓存
- 路由决策结果缓存（相似查询）
- 服务器统计信息本地存储

### 响应时间优化

- 并行服务器发现和连接
- 异步工具调用执行
- 渐进式UI更新（显示处理状态）

## 错误处理与恢复

### 错误分类

1. **连接层错误**
   - MCP_NO_CONNECTION：服务器不可达
   - MCP_CONN_TIMEOUT：连接超时
   - MCP_CONN_REFUSED：连接被拒绝

2. **协议层错误**
   - MCP_INVALID_REQUEST：请求格式错误
   - MCP_METHOD_NOT_FOUND：工具不存在
   - MCP_INVALID_PARAMS：参数错误

3. **业务层错误**
   - MCP_PERMISSION_DENIED：权限不足
   - MCP_RESOURCE_NOT_FOUND：资源不存在
   - MCP_OPERATION_FAILED：操作失败

### 恢复策略

1. **自动重试**：临时性错误（超时、网络中断）
2. **降级服务**：切换到备用服务器或工具
3. **用户交互**：需要用户输入或确认的错误
4. **错误上报**：记录详细错误信息用于诊断

## 扩展性设计

### 插件架构

系统支持动态添加新的MCP服务器：

```csharp
public interface IMcpServerProvider
{
    Task<List<McpServerInfo>> DiscoverServersAsync();
    Task<McpClientWrapper> ConnectAsync(McpServerInfo serverInfo);
}
```

### 自定义路由策略

支持注册自定义路由算法：

```csharp
public interface IRoutingStrategy
{
    Task<RoutingDecision?> RouteAsync(string query, List<McpServerInfo> servers);
    int Priority { get; }
}
```

### 工具扩展

支持工具能力的动态扩展和注册：

```csharp
public interface IToolCapabilityProvider
{
    string[] GetSupportedIntents();
    double CalculateMatchScore(string intent, McpToolInfo tool);
}
```

## 监控与诊断

### 遥测数据

- 路由决策成功率和置信度分布
- 工具调用延迟和成功率统计
- 服务器健康状态监控
- 用户查询意图分析

### 调试支持

- 详细的路由决策日志
- 工具调用请求/响应跟踪  
- AI提示和响应记录
- 性能分析数据

### 故障排除

- 连接诊断工具
- 服务器状态检查
- 工具可用性验证
- 配置验证和修复建议

## 最佳实践

### 开发指南

1. **异步优先**：所有I/O操作使用async/await
2. **取消支持**：支持CancellationToken取消长时间操作
3. **资源管理**：正确实现IDisposable释放连接资源
4. **异常处理**：分层异常处理，提供用户友好的错误信息
5. **日志记录**：使用结构化日志记录关键操作和决策

### 安全考虑

1. **最小权限**：只授予必要的工具访问权限
2. **输入验证**：验证用户输入和工具参数
3. **输出净化**：过滤敏感信息的输出
4. **审计日志**：记录敏感操作的执行日志
5. **超时控制**：设置合理的操作超时时间

### 性能建议

1. **连接复用**：维护长期的MCP服务器连接
2. **批量操作**：支持批量工具调用减少往返
3. **缓存策略**：缓存静态数据如工具列表和服务器信息
4. **并发控制**：限制并发连接数避免资源耗尽
5. **渐进加载**：优先加载用户最可能使用的服务器和工具

## 版本演进计划

### v1.0 - 基础功能
- [x] MCP服务器发现和连接
- [x] 基本的关键词匹配路由
- [x] 工具调用和结果显示
- [x] 简单的错误处理

### v1.1 - AI智能路由（当前版本）
- [x] 多步骤AI决策流程
- [x] 智能参数提取
- [x] 结果智能解释
- [x] 改进的错误处理和恢复

### v1.2 - 高级功能（规划中）
- [ ] 多工具组合调用
- [ ] 上下文感知的对话
- [ ] 个性化路由学习
- [ ] 高级安全策略

### v2.0 - 企业级功能（未来）
- [ ] 多租户支持
- [ ] 分布式MCP服务器支持
- [ ] 高级监控和分析
- [ ] 插件生态系统

---

**文档版本**：v1.1  
**最后更新**：2024年11月17日  
**维护者**：AI Dev Gallery Team