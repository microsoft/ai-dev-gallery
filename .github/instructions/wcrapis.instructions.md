---
applyTo: "AIDevGallery/Samples/WCRAPIs/**/*.cs"
---

# Windows AI APIs (WCRAPIs) Sample Instructions

When reviewing or modifying samples in `AIDevGallery/Samples/WCRAPIs/`:

## Overview

WCRAPI samples use Windows Copilot Runtime APIs (Phi Silica, Text Recognition, etc.) that are:
- Built into Windows on Copilot+ PCs
- Accessed via `Microsoft.Windows.AI.*` namespaces
- Subject to Limited Access Features (LAF) requirements

## Limited Access Features (LAF)

### Important Security Note
- **NEVER commit production LAF tokens to the repository**
- Demo tokens in code are for development only
- Use `LimitedAccessFeaturesHelper` for token management

### Pattern
```csharp
var demoToken = LimitedAccessFeaturesHelper.GetAiLanguageModelToken();
var demoPublisherId = LimitedAccessFeaturesHelper.GetAiLanguageModelPublisherId();
```

## Feature Availability Checks

Always check if the API is available before use:

```csharp
var readyState = LanguageModel.GetReadyState();
if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
{
    if (readyState == AIFeatureReadyState.NotReady)
    {
        var operation = await LanguageModel.EnsureReadyAsync();
        if (operation.Status != AIFeatureReadyResultState.Success)
        {
            ShowException(null, "Feature not available");
            return;
        }
    }
    // Use the API
}
else
{
    var msg = readyState == AIFeatureReadyState.DisabledByUser
        ? "Disabled by user."
        : "Not supported on this system.";
    ShowException(null, $"Feature not available: {msg}");
}
```

## Common WCRAPI Patterns

### Phi Silica Text Generation
```csharp
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;

_languageModel = await LanguageModel.CreateAsync();
var result = await _languageModel.GenerateResponseAsync(prompt);
```

### Text Recognition (OCR)
```csharp
using Microsoft.Windows.AI.Vision;

var textRecognizer = await TextRecognizer.CreateAsync();
var result = await textRecognizer.RecognizeTextAsync(softwareBitmap);
```

### Image Description
```csharp
using Microsoft.Windows.AI.Vision;

var imageDescriber = await ImageDescriber.CreateAsync();
var description = await imageDescriber.DescribeImageAsync(softwareBitmap);
```

## Model Types for WCRAPIs

Use `ModelType.PhiSilica` or specific WCRAPI model types:
```csharp
[GallerySample(
    Model1Types = [ModelType.PhiSilica],
    ...
)]
```

## Code Review Checks

- LAF tokens use `LimitedAccessFeaturesHelper`, not hardcoded values
- Feature availability checked before use
- Proper error handling for unsupported devices
- User-friendly messages for availability failures
- Graceful fallback when API is not ready
- CancellationToken support for long operations
- Proper disposal of AI resources

## ARM64 Requirements

Most WCRAPIs require ARM64 Copilot+ PCs:
- Test on ARM64 hardware or emulator
- Document ARM64 requirement clearly
- Handle unavailability on x64 gracefully

## API Categories

Each WCRAPI sample should reference the correct category in `apis.json`:
- `"Phi Silica"` - Text generation, summarization, rewriting
- `"Vision"` - OCR, image description, background removal
- `"Imaging"` - Super resolution, image enhancement

## NuGet References

WCRAPI samples typically need:
```csharp
NugetPackageReferences = [
    "Microsoft.Extensions.AI"
]
```
