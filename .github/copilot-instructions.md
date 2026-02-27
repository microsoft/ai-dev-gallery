# AI Dev Gallery - GitHub Copilot Custom Instructions

## Repository Overview

AI Dev Gallery is a WinUI 3 desktop application showcasing local AI capabilities for Windows developers. It features interactive samples powered by local AI models, and enables exporting standalone Visual Studio projects.

**Tech Stack**: C#, WinUI 3, Windows App SDK, ONNX Runtime, WinML, .NET 9.0

## PR Review Focus Areas

### Security
- Check for hardcoded secrets, API keys, or credentials
- Verify Limited Access Features (LAF) tokens are not committed
- Ensure model URLs point to trusted sources (HuggingFace, GitHub)
- Verify telemetry events don't log sensitive data (user paths, usernames)

### Performance
- Ensure model inference runs on background threads (`Task.Run`)
- Verify proper disposal of `InferenceSession` and other resources
- Check for memory leaks in image processing code

### Sample Implementation Standards
When reviewing PRs that add or modify samples:

1. **Class Structure**
   - Must inherit from `BaseSamplePage`
   - Must have `[GallerySample]` attribute with all required properties
   - Must override `LoadModelAsync(SampleNavigationParameters)` or `LoadModelAsync(MultiModelSampleNavigationParameters)`
   - Must register `Unloaded` event handler for cleanup

2. **[GallerySample] Attribute Requirements**
   - `Name`: Display name (required)
   - `Model1Types`: Array of supported model types (required)
   - `Scenario`: Must reference a ScenarioType from scenarios.json
   - `Id`: Unique GUID (required)
   - `Icon`: Segoe MDL2 glyph (required)
   - `NugetPackageReferences`: Array of needed packages
   - `SharedCode`: Array of SharedCodeEnum values if using shared utilities

3. **Model Loading Pattern**
   - Call `sampleParams.NotifyCompletion()` when initialization finishes
   - Use `await sampleParams.GetIChatClientAsync()` for language models
   - Use `sampleParams.WinMlSampleOptions` for WinML EP configuration
   - Handle exceptions with `ShowException(ex, message)`

4. **Resource Cleanup**
   ```csharp
   public MySample()
   {
       this.Unloaded += (s, e) => CleanUp();
       this.InitializeComponent();
   }
   
   private void CleanUp()
   {
       _session?.Dispose();
       _cts?.Cancel();
       _cts?.Dispose();
   }
   ```

### WinML/ONNX Patterns
When reviewing WinML samples:

1. **EP Registration**
   ```csharp
   var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();
   await catalog.EnsureAndRegisterCertifiedAsync();
   ```

2. **Session Options**
   - Always call `sessionOptions.RegisterOrtExtensions()`
   - Honor `WinMlSampleOptions.Policy` or `EpName`/`DeviceType`
   - Use `sessionOptions.GetCompiledModel()` when `CompileModel` is true

3. **Tensor Handling**
   - Verify input dimensions match model expectations
   - Check batch size is set correctly
   - Ensure proper preprocessing (resize, normalize)

### Model Definition Files
When reviewing `.model.json` or `.modelgroup.json`:

1. **Required Fields**
   - Unique `Id` for model family and individual models
   - Valid HuggingFace/GitHub `Url`
   - Correct `Size` in bytes
   - Valid `License` identifier
   - `HardwareAccelerator`: CPU, GPU, NPU, etc.

2. **For Language Models**
   - Must have `PromptTemplate` referencing promptTemplates.json

### Scenario Updates
When `scenarios.json` is modified:
- Verify the scenario has unique `Id`
- Check `Name`, `Description`, and `Instructions` are meaningful
- Remember: rebuild required for changes to take effect

## Code Style

### C# Conventions
- Use file-scoped namespaces
- Copyright header: `// Copyright (c) Microsoft Corporation. All rights reserved.`
- License header: `// Licensed under the MIT License.`
- Enable nullable reference types
- Use pattern matching and modern C# features
- Use `async`/`await` for asynchronous operations

### Naming Conventions
- Sample classes: Descriptive name matching the feature (e.g., `Chat`, `ImageClassification`)
- Private fields: `_camelCase` with underscore prefix
- Public properties: `PascalCase`

### XAML Conventions
- Use WinUI 3 controls
- Follow accessibility guidelines (narrator support, keyboard navigation)

## Testing Requirements

### Unit Tests
- Location: `AIDevGallery.Tests/UnitTests/`
- Framework: MSTest
- Run with filter: `--filter "FullyQualifiedName~UnitTests"`

### When Adding Samples
- Verify sample appears in correct category
- Test model loading and inference
- Verify project export generates valid VS solution
- Test on both x64 and ARM64 if possible

## Common Issues to Flag

1. **Missing cleanup code** - `Unloaded` handler not disposing resources
2. **Blocking UI thread** - Model inference not on background thread
3. **Missing NotifyCompletion** - Loading spinner never dismissed
4. **Invalid GUIDs** - Duplicate or malformed sample IDs
5. **Missing NuGet references** - SharedCode files require corresponding packages
6. **Incorrect HardwareAccelerator** - Model won't run on intended hardware
7. **scenarios.json not rebuilt** - New scenarios don't appear in app

## Documentation References

- [Adding Samples Guide](../docs/AddingSamples.md)
- [WinML Beginner Guide](../docs/WinML-Beginner-Guide-Create-Sample.md)
- [Telemetry Guide](../docs/Telemetry-Guide.md)
