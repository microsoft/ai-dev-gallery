---
applyTo: "AIDevGallery/Samples/**/*.cs"
---

# Sample Implementation Instructions

When reviewing or creating sample code in `AIDevGallery/Samples/`:

## Required Class Structure

Every sample MUST:
1. Inherit from `BaseSamplePage`
2. Have the `[GallerySample]` attribute with required properties
3. Override `LoadModelAsync(SampleNavigationParameters)` or `LoadModelAsync(MultiModelSampleNavigationParameters)`
4. Register cleanup in constructor via `this.Unloaded += (s, e) => CleanUp();`
5. Call `sampleParams.NotifyCompletion()` when model is ready

## [GallerySample] Attribute

Required properties:
- `Name`: Display name
- `Model1Types`: Array of `ModelType` values
- `Id`: Unique GUID string
- `Icon`: Segoe MDL2 glyph code

Optional properties:
- `Scenario`: Reference a `ScenarioType` from `scenarios.json`
- `Model2Types`: For dual-model samples
- `NugetPackageReferences`: Required packages
- `SharedCode`: Array of `SharedCodeEnum` values
- `AssetFilenames`: Asset files needed by the sample

---

## Language Model Samples (IChatClient)

For samples using language models (LLMs):

```csharp
protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
{
    try
    {
        chatClient = await sampleParams.GetIChatClientAsync();
    }
    catch (Exception ex)
    {
        ShowException(ex);
    }

    sampleParams.NotifyCompletion();
}
```

---

## Windows ML

For samples using ONNX models with Windows ML / ONNX Runtime:

### Initialization Flow

1. **Ensure certified EPs** - Install/register hardware acceleration packages (DML, QNN, etc.)
2. **Create SessionOptions** - Call `RegisterOrtExtensions()` for additional operators
3. **Honor WinMlSampleOptions** - Use `Policy` for auto-selection or `EpName`/`DeviceType` for specific EP
4. **Create InferenceSession** - Optionally use compiled model for faster subsequent runs
5. **Call NotifyCompletion** - Hide loading spinner when ready

### LoadModelAsync Pattern

```csharp
protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
{
    try
    {
        await InitializeModelAsync(sampleParams.ModelPath, sampleParams.WinMlSampleOptions);
    }
    catch (Exception ex)
    {
        ShowException(ex, "Failed to load model.");
    }

    sampleParams.NotifyCompletion();
}
```

### WinML Initialization (in InitializeModelAsync or SharedCode)

```csharp
// 1. Ensure certified EPs are available
var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();
await catalog.EnsureAndRegisterCertifiedAsync();

// 2. Create session options
SessionOptions sessionOptions = new();
sessionOptions.RegisterOrtExtensions();

// 3. Configure EP based on WinMlSampleOptions
if (options.Policy != null)
{
    sessionOptions.SetEpSelectionPolicy(options.Policy.Value);
}
else if (options.EpName != null)
{
    sessionOptions.AppendExecutionProviderFromEpName(options.EpName, options.DeviceType);
    if (options.CompileModel)
    {
        modelPath = sessionOptions.GetCompiledModel(modelPath, options.EpName) ?? modelPath;
    }
}

// 4. Create inference session
_inferenceSession = new InferenceSession(modelPath, sessionOptions);
```

### WinML Code Review Checks

- `EnsureAndRegisterCertifiedAsync()` called for EP registration
- `RegisterOrtExtensions()` called on SessionOptions
- `WinMlSampleOptions` honored (Policy or EpName/DeviceType)
- `CompileModel` option handled when applicable
- InferenceSession disposed in cleanup

---

## Cleanup Pattern

```csharp
public MySample()
{
    this.Unloaded += (s, e) => CleanUp();
    this.InitializeComponent();
}

private void CleanUp()
{
    _cts?.Cancel();
    _cts?.Dispose();
    _inferenceSession?.Dispose();
    chatClient?.Dispose();
}
```

## Code Review Checks

- `NotifyCompletion()` called when model is ready
- Resources disposed in `Unloaded` handler
- Long operations run on background thread (`Task.Run`)
- CancellationToken used for cancellable operations
- GUID in `Id` is unique across all samples
- `Scenario` references an existing ScenarioType
- All referenced `SharedCode` files exist
- NuGet packages match what SharedCode files need

## Other Patterns

### Telemetry
```csharp
SendSampleInteractedEvent("ClassifyImage");
```

### Accessibility
```csharp
NarratorHelper.Announce(InputTextBox, "Processing complete.", "ProcessingCompleteAnnouncementId");
```

### File Copyright Headers
```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
```
