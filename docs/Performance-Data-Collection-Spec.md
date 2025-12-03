# 性能数据收集方案设计文档

## 1. 概述

本文档详细描述了 AI Dev Gallery 项目的性能数据收集方案。该方案旨在通过自动化测试流程，收集关键的性能指标（如启动时间、模型加载时间、推理延迟等），并将数据结构化存储，以便后续在 Power BI 中进行趋势分析和回归检测。

本方案的核心设计理念是 **“轻量级、上下文感知、多平台支持”**。

## 2. 架构设计

### 2.1 数据流向

```mermaid
graph LR
    A[测试执行 (MSTest/FlaUI)] -->|调用| B[PerformanceCollector]
    B -->|序列化| C[JSON 性能报告]
    C -->|上传| D[Azure Blob Storage]
    D -->|连接| E[Power BI Dashboard]
```

### 2.2 核心组件

1.  **PerformanceCollector (C#)**: 一个轻量级的静态辅助类，负责在测试运行时收集数据、捕获环境上下文，并生成 JSON 文件。
2.  **JSON Report**: 结构化的数据文件，包含元数据、环境信息和测量指标。
3.  **CI/CD Pipeline**: 集成在 GitHub Actions 和 Azure Pipelines 中，负责触发测试并上传产物。

## 3. 数据结构设计与思考 (JSON Schema)

为了满足多维度分析的需求，我们设计了包含 `Meta` (元数据)、`Environment` (环境) 和 `Measurements` (测量值) 的三层结构。这种设计并非随意为之，而是基于以下核心思考：

### 3.1 设计思考

1.  **可追溯性 (Traceability) - `Meta`**
    *   **问题**: 当发现性能下降时，第一反应是“这是哪个版本引入的？”。
    *   **设计**: `Meta` 节点必须包含 `CommitHash` 和 `RunId`。这使得我们可以在 Power BI 中点击一个数据点，直接跳转到对应的 GitHub Commit 或 Pipeline Build，快速定位“罪魁祸首”。

2.  **上下文感知 (Context Awareness) - `Environment`**
    *   **问题**: AI 应用的性能高度依赖硬件。同样的模型在 NPU 上跑可能比 CPU 快 10 倍。如果混合了不同机器的数据而没有区分，平均值将毫无意义。
    *   **设计**: `Environment` 节点独立存在，详细记录 OS、CPU、RAM 和 GPU/NPU 信息。这允许我们在分析时进行“同类比较”（Apple-to-Apple comparison），例如“只看所有配备 RTX 4090 的机器的性能趋势”。

3.  **灵活性与扩展性 (Flexibility) - `Measurements` & `Tags`**
    *   **问题**: AI 领域的指标变化很快（从单纯的耗时，到 Tokens/s，再到 TTFT）。如果我们为每个指标设计固定的数据库列，架构将非常脆弱。
    *   **设计**: 采用“宽表”模式。`Measurements` 是一个列表，每个测量项包含 `Name`, `Value`, `Unit` 和一个灵活的 `Tags` 字典。
    *   **价值**: `Tags` 是设计的精髓。它允许我们记录任意维度的业务上下文（如 `Model=Phi-3`, `Quantization=Int4`, `Accelerator=DirectML`）。在 Power BI 中，这些 Tags 可以被动态展开为筛选器，无需修改数据结构即可支持新的分析维度。

4.  **轻量化 (Lightweight)**
    *   **问题**: 引入复杂的 APM (Application Performance Monitoring) 系统会增加运维成本和 CI 环境的复杂性。
    *   **设计**: 选择 JSON 文件作为载体。它人类可读、易于调试、无需数据库服务器，且 Azure Blob Storage + Power BI 对 JSON 的支持极佳，实现了最低成本的闭环。

### 3.2 示例 JSON

```json
{
  "Meta": {
    "SchemaVersion": "1.0",
    "RunId": "20251126.1",
    "CommitHash": "a1b2c3d...",
    "Branch": "main",
    "Timestamp": "2025-11-26T10:30:00Z",
    "Trigger": "Push"
  },
  "Environment": {
    "OS": "Microsoft Windows 11 Enterprise",
    "Platform": "X64",
    "Configuration": "Release",
    "Hardware": {
      "Cpu": "Intel(R) Core(TM) i9-13900K",
      "Ram": "32 GB",
      "Gpu": "NVIDIA GeForce RTX 4090"
    }
  },
  "Measurements": [
    {
      "Category": "AppLifecycle",
      "Name": "AppStartupTime",
      "Value": 1250.5,
      "Unit": "ms",
      "Tags": {
        "Scenario": "ColdStart"
      }
    },
    {
      "Category": "AI_Inference",
      "Name": "TimeToFirstToken",
      "Value": 200.0,
      "Unit": "ms",
      "Tags": {
        "Model": "Phi-3-mini",
        "Accelerator": "NPU",
        "Quantization": "Int4"
      }
    }
  ]
}
```

### 3.3 字段详细说明

*   **Meta**: 用于追踪数据来源。
    *   `RunId`: CI 流水线的唯一 ID，用于关联构建日志。
    *   `Trigger`: 触发原因（Push, PR, Schedule），用于区分日常开发和夜间回归测试。
*   **Environment**: 记录测试运行时的硬件和系统环境。
    *   `Hardware`: 包含 CPU/RAM/GPU 信息，用于在异构硬件环境中标准化性能基准。
*   **Measurements**: 具体的性能指标列表。
    *   `Category`: 指标分类（如 `AppLifecycle`, `AI_Inference`, `Memory`），用于在 Dashboard 上分组展示。
    *   `Tags`: **关键设计**。灵活的键值对，用于存储业务上下文。例如，通过 Tag 区分同一模型在不同量化精度下的性能差异。

## 4. 实现细节

### 4.1 PerformanceCollector 类

位于 `AIDevGallery.UnitTests\Helpers\PerformanceCollector.cs`。

*   **线程安全**: 使用 `lock` 机制，支持并行测试执行。
*   **自动环境探测**:
    *   自动识别 CI 平台（GitHub Actions vs Azure Pipelines）。
    *   自动获取 CPU 型号和 RAM 大小。
*   **灵活输出**: 支持通过环境变量 `PERFORMANCE_OUTPUT_PATH` 自定义输出目录。

### 4.2 使用示例

在测试代码中（Unit Test 或 UI Test）：

```csharp
[TestMethod]
public void Measure_ModelLoad_Time()
{
    PerformanceCollector.Clear();
    var stopwatch = Stopwatch.StartNew();

    // ... 执行模型加载操作 ...

    stopwatch.Stop();

    // 记录指标
    PerformanceCollector.Track("ModelLoadTime", stopwatch.ElapsedMilliseconds, "ms", new Dictionary<string, string>
    {
        { "Model", "Phi-3" },
        { "Accelerator", "NPU" }
    }, category: "AI_Model");

    // 保存结果
    PerformanceCollector.Save();
}
```

## 5. CI/CD 集成指南

### 5.1 Azure Pipelines

在 `azure-pipelines.yml` 中配置：

```yaml
variables:
  PERFORMANCE_OUTPUT_PATH: $(Build.ArtifactStagingDirectory)\PerfResults

steps:
- task: DotNetCoreCLI@2
  displayName: 'Run Performance Tests'
  inputs:
    command: 'test'
    projects: '**/*UnitTests.csproj'
    arguments: '--filter "TestCategory=Performance"'

- task: PublishBuildArtifacts@1
  displayName: 'Publish Performance Data'
  inputs:
    PathtoPublish: '$(Build.ArtifactStagingDirectory)\PerfResults'
    ArtifactName: 'PerformanceData'
```

### 5.2 GitHub Actions

在 `.github/workflows/perf.yml` 中配置：

```yaml
env:
  PERFORMANCE_OUTPUT_PATH: ${{ github.workspace }}/PerfResults

steps:
- name: Run Performance Tests
  run: dotnet test --filter "TestCategory=Performance"

- name: Upload Performance Artifacts
  uses: actions/upload-artifact@v4
  with:
    name: PerformanceData
    path: ${{ github.workspace }}/PerfResults
```

## 6. Power BI 集成

1.  **数据源**: 选择 "Azure Blob Storage"。
2.  **连接**: 指向存储 JSON 文件的容器。
3.  **数据转换 (Power Query)**:
    *   解析 JSON 内容。
    *   展开 `Measurements` 列表。
    *   展开 `Tags` 列（根据需要）。
4.  **可视化**:
    *   使用 `Meta.Timestamp` 作为 X 轴。
    *   使用 `Measurements.Value` 作为 Y 轴。
    *   使用 `Environment.Hardware.Cpu` 或 `Tags.Model` 作为切片器/图例。

## 7. 优势总结

1.  **零侵入性**: 不需要修改应用源代码，仅在测试层收集。
2.  **高信噪比**: 只记录关键业务指标，避免了通用 APM 工具的海量噪声。
3.  **上下文丰富**: 每一条数据都绑定了代码版本、硬件环境和模型参数，使得归因分析变得简单。
4.  **成本低廉**: 基于 JSON 文件存储，无需维护昂贵的时序数据库。
