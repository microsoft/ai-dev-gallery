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

## [GallerySample] Attribute Checklist

Required properties:
- `Name`: Display name for the sample
- `Model1Types`: Array of `ModelType` values (e.g., `[ModelType.LanguageModels, ModelType.PhiSilica]`)
- `Scenario`: Must reference a `ScenarioType` from `scenarios.json`
- `Id`: Unique GUID string (generate a new one for each sample)
- `Icon`: Segoe MDL2 glyph code (e.g., `"\uE8D4"`)

Common optional properties:
- `Model2Types`: For dual-model samples
- `NugetPackageReferences`: Required packages (e.g., `["Microsoft.Extensions.AI"]`)
- `SharedCode`: Array of `SharedCodeEnum` values for exportable utilities

## Model Loading Patterns

### For Language Models (IChatClient)
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

### For WinML/ONNX Models
```csharp
protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
{
    try
    {
        await InitModel(
            sampleParams.ModelPath,
            sampleParams.WinMlSampleOptions.Policy,
            sampleParams.WinMlSampleOptions.EpName,
            sampleParams.WinMlSampleOptions.CompileModel,
            sampleParams.WinMlSampleOptions.DeviceType);
    }
    catch (Exception ex)
    {
        ShowException(ex, "Failed to load model.");
    }
    sampleParams.NotifyCompletion();
}
```

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

- `NotifyCompletion()` is called (otherwise loading spinner never hides)
- Resources disposed in `Unloaded` handler
- Long operations run on background thread (`Task.Run`)
- CancellationToken used for cancellable operations
- GUID in `Id` is unique across all samples
- `Scenario` references an existing ScenarioType
- All referenced `SharedCode` files exist
- NuGet packages match what SharedCode files need

## Telemetry

Use `SendSampleInteractedEvent("action")` to track user interactions:
```csharp
SendSampleInteractedEvent("ClassifyImage");
```

## Accessibility

Use `NarratorHelper` for screen reader announcements:
```csharp
NarratorHelper.Announce(InputTextBox, "Processing complete.", "ProcessingCompleteAnnouncementId");
```

## File Copyright Headers

Every C# file must start with:
```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
```
