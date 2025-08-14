# LanguageModel 功能示例

这是一个展示 `Microsoft.Windows.AI.Text.LanguageModel` 各项功能的简单示例项目。

## 功能特性

### 1. 基础文本生成
- 使用 `LanguageModel.GenerateResponseAsync()` 方法
- 支持自定义提示词输入
- 实时显示生成结果

### 2. 文本摘要
- 使用 `TextSummarizer.SummarizeParagraphAsync()` 方法
- 将长文本转换为简洁摘要
- 适用于文档总结、内容概括等场景

### 3. 文本重写
- 使用 `TextRewriter.RewriteAsync()` 方法
- 保持原意的情况下重新表达文本
- 适用于内容改写、风格调整等

### 4. 文本转表格
- 使用 `TextToTableConverter.ConvertAsync()` 方法
- 将结构化文本转换为表格格式
- 适用于数据整理、报告生成等

## 系统要求

- Windows 10 版本 17763 或更高版本
- .NET 8.0 或更高版本
- 支持 Windows AI 功能的设备（如 Copilot+ PC）
- Phi-Silica 模型可用

## 使用方法

1. 启动应用程序
2. 等待AI模型初始化完成
3. 在各个功能模块中输入相应的文本
4. 点击对应的功能按钮
5. 查看生成结果

## 技术实现

- 使用 WinUI 3 构建用户界面
- 直接调用 `Microsoft.Windows.AI.Text` 命名空间下的API
- 异步处理所有AI操作
- 完整的错误处理和状态反馈

## 注意事项

- 首次运行可能需要下载和初始化AI模型
- 某些功能可能受设备硬件能力限制
- 建议在支持NPU的设备上运行以获得最佳性能

## 依赖项

- Microsoft.Windows.AI.MachineLearning (0.3.131-beta)
- WinUI 3
- .NET 8.0 