---
applyTo: "AIDevGallery/Samples/WCRAPIs/**/*.cs"
---

# Windows AI APIs (WCRAPIs) Sample Instructions

When reviewing or modifying samples in `AIDevGallery/Samples/WCRAPIs/`:

## Overview

Windows AI APIs samples use Windows Copilot Runtime APIs (Phi Silica, Text Recognition, etc.) that are built into Windows on Copilot+ PCs.

## Limited Access Features (LAF)

### Important Security Note
- **NEVER commit production LAF tokens to the repository**
- Demo tokens in code are for development only
- Use `LimitedAccessFeaturesHelper` for token management

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
