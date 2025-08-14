# LanguageModel 功能示例

这是一个展示 `Microsoft.Windows.AI.Text.LanguageModel` 各项功能的示例项目，**专为 Visual Studio 优化**。

## 🚀 快速开始

### 在 Visual Studio 中运行（推荐）
1. 用 **Visual Studio 2022** 打开 `LanguageModelExample.csproj`
2. 还原 NuGet 包
3. 按 `F5` 开始调试，或 `Ctrl+F5` 运行

### 命令行运行
```bash
cd LanguageModelExample
dotnet restore
dotnet build --configuration Release --platform x64
dotnet run
```

## ✨ 功能特性

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

## 🎯 Visual Studio 优化特性

- **完整的调试支持**: 断点、单步执行、变量监视
- **智能交互**: 示例数据快速加载、一键清空
- **实时状态反馈**: 进度显示、错误提示、调试信息
- **日志记录**: 集成 Microsoft.Extensions.Logging
- **项目模板**: 包含 .vstemplate 文件
- **预配置**: launchSettings.json 和发布配置

## 🛠️ 系统要求

- **操作系统**: Windows 10 版本 17763 或更高版本
- **.NET版本**: .NET 8.0 或更高版本
- **开发环境**: Visual Studio 2022 17.0+ (推荐)
- **硬件要求**: 支持 Windows AI 功能的设备（如 Copilot+ PC）
- **模型要求**: Phi-Silica 模型可用

## 📚 详细文档

- **[Visual Studio 使用指南](VISUAL_STUDIO_GUIDE.md)** - 完整的VS使用说明
- **[项目总结](PROJECT_SUMMARY.md)** - 详细的技术实现说明
- **[构建脚本](build.bat)** - Windows批处理构建脚本
- **[PowerShell脚本](build.ps1)** - PowerShell构建脚本

## 🎮 使用方法

1. **启动应用**: 在Visual Studio中按F5，或运行生成的exe文件
2. **等待初始化**: 应用会自动初始化AI模型，状态栏会显示进度
3. **快速测试**: 使用"快速开始"区域加载示例数据
4. **功能测试**: 在各个模块中输入文本并点击相应按钮
5. **查看结果**: 结果会显示在对应的输出框中
6. **调试信息**: 展开"调试信息"面板查看详细信息

## 🔧 技术实现

- 使用 WinUI 3 构建用户界面
- 直接调用 `Microsoft.Windows.AI.Text` 命名空间下的API
- 异步处理所有AI操作
- 完整的错误处理和状态反馈
- 集成日志记录系统
- 支持多种构建和发布配置

## 📱 交互体验

- **示例数据管理**: 循环切换多个预设示例
- **一键操作**: 加载所有示例、清空所有内容
- **实时反馈**: 进度环、状态信息、错误提示
- **调试友好**: 详细的调试信息面板

## 🚨 注意事项

- 首次运行可能需要下载和初始化AI模型
- 某些功能可能受设备硬件能力限制
- 建议在支持NPU的设备上运行以获得最佳性能
- 需要确保Phi-Silica模型在系统中可用

## 🎉 开始使用

现在您可以在 **Visual Studio** 中享受完整的开发体验了！

1. 打开项目文件
2. 设置断点进行调试
3. 使用交互式UI测试功能
4. 查看详细的调试信息
5. 根据需要修改和扩展功能

---

**专为 Visual Studio 优化，提供最佳的开发体验！** 🚀 