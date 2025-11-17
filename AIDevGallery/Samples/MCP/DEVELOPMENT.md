# MCP Client 开发文档

## 快速开始

### 项目结构

```
AIDevGallery/Samples/MCP/
├── MCPClient.xaml              # 主UI页面
├── MCPClient.xaml.cs           # UI代码隐藏
├── Models/
│   └── McpModels.cs           # 数据模型定义
├── Services/
│   ├── McpManager.cs          # 主协调器
│   ├── McpDiscoveryService.cs # 服务发现
│   ├── McpRoutingService.cs   # 智能路由
│   ├── McpInvocationService.cs# 工具调用
│   └── McpClientInterface.cs  # MCP客户端封装
└── README.md                  # 项目说明
```

### 核心依赖

基于实际代码分析，MCP Client功能的核心依赖包括：

#### 必需的NuGet包依赖
```xml
<PackageReference Include="ModelContextProtocol" Version="0.4.0-preview.1" />
<PackageReference Include="Microsoft.Extensions.AI" Version="9.9.1" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
<PackageReference Include="CommunityToolkit.WinUI.Converters" Version="8.2.250402" />
<PackageReference Include="Microsoft.WindowsAppSDK" Version="2.0.0-experimental3" />
```

#### 在示例属性中声明的依赖
MCP Client示例在`GallerySample`属性中声明了这些运行时依赖：
```csharp
NugetPackageReferences = [
    "CommunityToolkit.Mvvm",           // MVVM框架
    "CommunityToolkit.WinUI.Converters", // UI转换器
    "Microsoft.Extensions.AI"          // AI抽象层
    "ModelContextProtocol"              // MCP协议支持
]
```

#### 系统依赖
- **Windows 10 版本 1903+ 或 Windows 11**
- **odr.exe**：Windows MCP 代理程序，用于与系统MCP服务器通信
- **已注册的MCP服务器**：
  - `systeminfo-mcp-server`：系统信息服务器
  - `file-mcp-server`：文件操作服务器  
  - `settings-mcp-server`：设置管理服务器

## 服务详细说明

### McpManager - 主协调器

**核心职责**：
- 统筹整个MCP交互流程
- 协调各子服务的调用
- 处理LLM集成和结果提取 

**关键方法**：

```csharp
// 处理用户查询的主要方法
public async Task<McpResponse> ProcessQueryAsync(string userQuery, IChatClient? chatClient, CancellationToken cancellationToken = default)
{
    // 1. 路由决策 - 选择最佳的 server 和 tool
    var routingDecision = await _routingService.RouteQueryAsync(userQuery);
    
    // 2. 执行工具调用
    var invocationResult = await _invocationService.InvokeToolAsync(routingDecision, cancellationToken);
    
    // 3. 使用 LLM 处理结果
    return await ProcessInvocationResultAsync(userQuery, invocationResult, chatClient, cancellationToken);
}

// 获取系统状态
public async Task<Dictionary<string, object>> GetSystemStatusAsync(CancellationToken cancellationToken = default)

// 获取可用工具目录
public string GetToolCatalog()
```

### McpDiscoveryService - 服务发现

**核心职责**：
- 发现和枚举Windows系统中的MCP服务器
- 建立和维护服务器连接
- 获取服务器工具列表

**关键方法**：

```csharp
// 发现所有可用的MCP服务器
public async Task<List<McpServerInfo>> DiscoverServersAsync(CancellationToken cancellationToken = default)

// 获取指定服务器的工具列表
public List<McpToolInfo> GetServerTools(string serverId)

// 获取所有已连接的服务器
public List<McpServerInfo> GetConnectedServers()

// 获取服务器客户端连接
public McpClientWrapper? GetServerClient(string serverId)
```

**预定义服务器配置**：

```csharp
private List<McpServerInfo> GetPredefinedServers()
{
    return new List<McpServerInfo>
    {
        new McpServerInfo
        {
            Id = "system-info",
            Name = "System Information Server",
            ExecutablePath = "odr.exe",
            Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server"],
            Categories = ["system", "hardware"],
            Tags = ["ram", "cpu", "disk", "memory", "hardware", "system", "info"]
        },
        // 更多服务器配置...
    };
}
```

### McpRoutingService - 智能路由

**核心职责**：
- 使用AI进行多步骤路由决策
- 实现降级到关键词匹配的备用方案
- 评分和排序候选工具

**AI驱动的路由流程**：

```csharp
public async Task<RoutingDecision?> RouteQueryAsync(string userQuery)
{
    // 步骤1: 意图识别
    var intent = await ClassifyIntentAsync(userQuery);
    
    // 步骤2: 服务器选择
    var serverSelection = await SelectServerAsync(userQuery, servers, intent);
    
    // 步骤3: 工具选择
    var toolSelection = await SelectToolAsync(userQuery, selectedServer, availableTools, intent);
    
    // 步骤4: 参数提取
    var argumentExtraction = await ExtractArgumentsAsync(userQuery, selectedTool, intent);
    
    // 步骤5: 生成工具调用计划
    var invocationPlan = await CreateInvocationPlanAsync(userQuery, selectedServer, selectedTool, arguments);
    
    return new RoutingDecision { /* 组装结果 */ };
}
```

**降级策略**：

```csharp
// 当AI路由失败时的降级方案
private async Task<RoutingDecision?> RouteWithKeywordsAsync(string userQuery, List<McpServerInfo> servers)
{
    var query = userQuery.ToLowerInvariant();
    var candidates = new List<(McpServerInfo server, McpToolInfo tool, double score)>();
    
    foreach (var server in servers)
    {
        var tools = _discoveryService.GetServerTools(server.Id);
        foreach (var tool in tools)
        {
            var score = CalculateSimpleMatchScore(query, server, tool);
            if (score > 0)
            {
                candidates.Add((server, tool, score));
            }
        }
    }
    
    var best = candidates.OrderByDescending(c => c.score).First();
    return new RoutingDecision { /* 返回最佳匹配 */ };
}
```

### McpInvocationService - 工具调用

**核心职责**：
- 安全执行MCP工具调用
- 处理超时和重试机制
- 维护服务器统计信息

**关键方法**：

```csharp
// 执行工具调用
public async Task<McpInvocationResult> InvokeToolAsync(RoutingDecision decision, CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var client = _discoveryService.GetServerClient(decision.SelectedServer.Id);
        var result = await CallToolWithTimeoutAsync(client, toolCallRequest, TimeSpan.FromSeconds(30), cancellationToken);
        
        UpdateServerStats(decision.SelectedServer, true, stopwatch.Elapsed);
        
        return new McpInvocationResult
        {
            IsSuccess = true,
            Data = result,
            ExecutionTime = stopwatch.Elapsed,
            RoutingInfo = decision
        };
    }
    catch (Exception ex)
    {
        UpdateServerStats(decision.SelectedServer, false, stopwatch.Elapsed);
        return new McpInvocationResult { IsSuccess = false, Error = ex.Message };
    }
}

// 健康检查
public async Task<bool> HealthCheckAsync(string serverId, CancellationToken cancellationToken = default)

// 批量调用
public async Task<List<McpInvocationResult>> InvokeToolsAsync(List<RoutingDecision> decisions, CancellationToken cancellationToken = default)
```

## AI Prompt 工程指南

### 系统级约束提示

```csharp
private const string GLOBAL_SYSTEM_PROMPT = @"
你是一个 MCP-aware 助手，只能通过 MCP 协议调用已注册的工具来完成用户请求。
不得绕过 MCP 执行命令，也不得在未调用工具时编造答案。

核心约束：
1. 始终执行完整决策流程：意图识别→服务器选择→工具选择→参数生成→安全检查→工具调用→结果解释
2. 中间步骤输出严格JSON格式，禁止解释性文字
3. 参数不足时返回单条中文澄清问题
4. 绝不泄露敏感信息，只调用最小权限工具
5. 最终回答附简短来源标注(server.tool)
";
```

### 分步骤提示模板

#### 意图识别提示

```csharp
private async Task<IntentClassificationResponse?> ClassifyIntentAsync(string userQuery)
{
    var systemPrompt = """
        你是一个专门的JSON响应生成器。分析用户的MCP工具请求并返回结构化分析结果。
        
        必须返回且仅返回这个JSON结构：
        {
          "need_tool": boolean,
          "topic": "systeminfo" | "filesystem" | "settings" | "hardware" | "network" | "other",
          "keywords": ["string1", "string2", ...],
          "confidence": number_between_0_and_1
        }
        """;

    var userPrompt = $"用户问题：{userQuery}";
    
    return await CallAIWithJsonResponse<IntentClassificationResponse>(systemPrompt, userPrompt, "意图识别");
}
```

#### 结果提取提示

```csharp
private string CreateExtractionSystemPrompt(McpInvocationResult result)
{
    return @"你是一个 MCP-aware 助手，专门负责从 MCP 工具调用的结果中提取关键信息并生成用户友好的回答。

核心规则：
1. **严格基于MCP数据**: 你必须且只能基于 MCP 工具返回的实际数据回答，绝不允许编造、推测或添加任何数据中不存在的信息
2. **避免自由回答**: 不允许绕过 MCP 执行命令或提供未经工具验证的信息
3. **明确空值处理**: 如果数据不完整、缺失或为空，必须明确说明'数据不可用'或'工具未返回此信息'
4. **结构化响应**: 用自然、简洁的语言表达技术信息，但保持事实准确性
5. **错误透明**: 如果返回的是错误或空数据，诚实告知用户并提供可操作建议

输出格式：直接回答用户的问题，基于MCP数据提供准确信息。如需说明数据来源限制，请简洁说明。";
}
```

### JSON响应处理

```csharp
private async Task<T?> CallAIWithJsonResponse<T>(string systemPrompt, string userPrompt, string stepName) where T : class
{
    try
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        };

        // 方法1: 使用结构化输出 (推荐)
        var structuredResponse = await _chatClient!.GetResponseAsync<T>(
            messages, 
            options: new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()
            });
        
        if (structuredResponse != null && structuredResponse.TryGetResult(out T? result) && result != null)
        {
            return result;
        }
        
        // 方法2: 降级到文本解析
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Json,
            Temperature = 0.1f
        };

        var response = await _chatClient!.GetResponseAsync(messages, chatOptions);
        var cleanedJson = CleanJsonResponse(response.Text ?? string.Empty);
        
        return JsonSerializer.Deserialize<T>(cleanedJson, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, $"❌ Error during {stepName}");
        return null;
    }
}

```

## 错误处理最佳实践

### 错误分类和处理

```csharp
public enum McpErrorType
{
    Connection,    // 连接相关错误
    Permission,    // 权限错误
    InvalidArgs,   // 参数错误
    Timeout,       // 超时错误
    ServerError,   // 服务器内部错误
    Unknown        // 未知错误
}

public class McpErrorHandler
{
    public static McpInvocationResult HandleError(Exception ex, RoutingDecision decision)
    {
        var errorType = ClassifyError(ex);
        var userMessage = GetUserFriendlyMessage(errorType, ex);
        var errorCode = GetErrorCode(errorType);
        
        return new McpInvocationResult
        {
            IsSuccess = false,
            Error = userMessage,
            ErrorCode = errorCode,
            RoutingInfo = decision
        };
    }
    
    private static McpErrorType ClassifyError(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException => McpErrorType.Timeout,
            TimeoutException => McpErrorType.Timeout,
            UnauthorizedAccessException => McpErrorType.Permission,
            ArgumentException => McpErrorType.InvalidArgs,
            _ => McpErrorType.Unknown
        };
    }
    
    private static string GetUserFriendlyMessage(McpErrorType errorType, Exception ex)
    {
        return errorType switch
        {
            McpErrorType.Connection => "无法连接到MCP服务器，请检查服务器状态。",
            McpErrorType.Permission => "权限不足，无法执行此操作。",
            McpErrorType.InvalidArgs => "提供的参数不正确，请检查输入。",
            McpErrorType.Timeout => "操作超时，请稍后重试。",
            McpErrorType.ServerError => "服务器内部错误，请联系管理员。",
            _ => $"发生未知错误：{ex.Message}"
        };
    }
}
```

## 部署和配置

### 应用程序清单配置

确保应用程序清单包含必要的能力声明：

```xml
<Package.appxmanifest>
  <Applications>
    <Application>
      <uap:VisualElements>
        <!-- UI配置 -->
      </uap:VisualElements>
      
      <Extensions>
        <!-- MCP相关扩展 -->
        <uap:Extension Category="windows.protocol">
          <uap:Protocol Name="mcp" />
        </uap:Extension>
      </Extensions>
    </Application>
  </Applications>
  
  <Capabilities>
    <Capability Name="internetClient" />
    <Capability Name="privateNetworkClientServer" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package.appxmanifest>
```

## 故障排除

### 常见问题和解决方案

#### 1. MCP服务器连接失败

**症状**：应用程序显示"No MCP servers available"

**解决步骤**：
1. 检查Windows系统是否已安装MCP服务器
2. 验证`odr.exe`是否在系统PATH中
3. 检查应用程序权限设置
4. 查看日志文件中的详细错误信息

```csharp
// 诊断代码
private async Task DiagnoseMcpConnections()
{
    var servers = _discoveryService.GetConnectedServers();
    foreach (var server in servers)
    {
        var isHealthy = await _invocationService.HealthCheckAsync(server.Id);
        _logger?.LogInformation($"Server {server.Name}: {(isHealthy ? "Healthy" : "Unhealthy")}");
        
        if (!isHealthy)
        {
            // 尝试重新连接
            await _discoveryService.ReconnectServerAsync(server.Id);
        }
    }
}
```

#### 2. AI路由决策失败

**症状**：查询返回"No routing decision found"

**解决步骤**：
1. 检查LLM客户端连接状态
2. 验证AI模型配置
3. 查看路由决策日志
4. 检查是否存在合适的工具

```csharp
// 诊断AI路由
private async Task DiagnoseAiRouting(string userQuery)
{
    try
    {
        var candidates = await _routingService.GetRoutingCandidatesAsync(userQuery);
        _logger?.LogInformation($"Found {candidates.Count} routing candidates for '{userQuery}':");
        
        foreach (var candidate in candidates.Take(5))
        {
            _logger?.LogInformation($"  {candidate.server.Name}.{candidate.tool.Name}: {candidate.score:F2}");
        }
        
        if (!candidates.Any())
        {
            _logger?.LogWarning("No routing candidates found - check tool keywords and server availability");
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error during routing diagnosis");
    }
}
```

#### 3. 工具调用超时

**症状**：工具调用返回"MCP_TIMEOUT"错误

**解决步骤**：
1. 增加超时时间配置
2. 检查服务器负载
3. 验证网络连接
4. 考虑使用更轻量级的替代工具

```csharp
// 配置更长的超时时间
var invocationResult = await CallToolWithTimeoutAsync(
    client, 
    toolCallRequest, 
    TimeSpan.FromMinutes(2), // 增加到2分钟
    cancellationToken
);
```

**文档版本**：v1.1  
**最后更新**：2024年11月17日  
**维护者**：AI Dev Gallery Team