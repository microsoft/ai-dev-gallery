# MCP Client 演示说明

这个 MCP Client 示例展示了如何创建一个智能的 Model Context Protocol 客户端，能够：

## 功能特性

1. **智能路由**: 根据用户查询自动选择最合适的 MCP server 和工具
2. **LLM 集成**: 使用语言模型处理和美化 MCP 工具的返回结果
3. **实时状态**: 显示 MCP 服务器连接状态和工具可用性
4. **安全控制**: 对敏感操作进行用户确认
5. **错误处理**: 完善的错误处理和降级机制

## 架构组件

### 服务层
- **McpDiscoveryService**: 发现和连接本地 MCP 服务器
- **McpRoutingService**: 智能路由决策，选择最佳工具
- **McpInvocationService**: 执行工具调用并处理结果
- **McpManager**: 统一管理器，协调所有服务

### 路由算法
系统使用多层路由算法来选择最佳工具：

1. **意图分析**: 解析用户查询的主要意图（内存、存储、CPU等）
2. **服务器匹配**: 基于类别和标签匹配相关服务器
3. **工具匹配**: 在选定服务器中找到最合适的工具
4. **评分系统**: 综合考虑匹配度、成功率、响应时间等因素

### 示例查询处理流程

**用户输入**: "这台电脑的RAM有多大？"

1. **意图分析**: 识别为 "memory" 意图
2. **服务器选择**: 选择 "System Information Server"（匹配 "system", "hardware" 类别）
3. **工具选择**: 选择 "get_ram_size" 工具（匹配 "ram", "memory" 关键词）
4. **参数提取**: 从查询中提取相关参数
5. **工具调用**: 执行 MCP 工具调用
6. **结果处理**: 使用 LLM 将技术数据转换为用户友好的回答

**最终回答**: "这台电脑的总内存为 16GB，当前已使用 7.5GB（46.9%），剩余可用内存 8.5GB。"

## 模拟数据说明

由于这是演示版本，系统使用模拟的 MCP 服务器数据：

### 预定义服务器
1. **System Information Server**: 系统硬件信息
   - 工具: get_system_info, get_ram_size
   - 类别: system, hardware

2. **File System Server**: 文件系统操作
   - 工具: list_files, get_file_info
   - 类别: filesystem, files

3. **Process Information Server**: 进程和性能信息
   - 工具: list_processes, get_cpu_usage
   - 类别: system, processes

## 扩展说明

要连接真实的 MCP 服务器，需要：

1. 安装相应的 MCP 服务器程序
2. 配置服务器的可执行路径和参数
3. 更新 `GetPredefinedServers()` 方法中的服务器定义
4. 实现真实的 stdio 通信协议

## 安全考虑

- 敏感操作需要用户确认
- 最小权限原则
- 错误隔离和恢复
- 连接超时和重试机制

## 使用建议

尝试以下查询来体验不同的路由决策：

- "这台电脑的内存有多大？" → 路由到内存信息工具
- "C盘还有多少空间？" → 路由到磁盘信息工具  
- "当前CPU使用率如何？" → 路由到性能监控工具
- "显示正在运行的进程" → 路由到进程列表工具