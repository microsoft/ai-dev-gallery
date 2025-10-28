### Beginner Guide to Create a WinML Sample in AI Dev Gallery

## What you’ll build

- Learn how samples are organized and displayed in AI Dev Gallery app
- Understand WinML, ONNX, and Execution Providers (EPs)
- Add `[GallerySample]` metadata and link your page to a scenario (`scenarios.json`)
- Initialize a WinML model the “project-approved” way in `LoadModelAsync`
- Dispose resources when the page unloads
- Use the Minimal Sample skeleton (at the end) to get started quickly

---

## Core concepts

- **What is ONNX?**
  - ONNX is a common file format for AI models. Export your model (from PyTorch, etc.) to `.onnx` and you can run it on Windows using WinML/ONNX Runtime.

- **What is WinML?**
  - Windows Machine Learning (WinML) is the Windows-native way to run ONNX models. It offers high-performance inference, hardware acceleration (e.g., GPU), and tight integration with Windows app packaging and devices.

- **ONNX Runtime (ORT) and Execution Providers (EPs)**
  - ORT is the inference engine. EPs are backend plugins that execute model ops on different hardware.
  - Typical EPs:
    - CPU: pure CPU inference
    - DirectML (DML): GPU acceleration on Windows via DirectML
    - QNN / NPU: accelerators on specific hardware
  - In this project, EP choice and options come from `WinMlSampleOptions` (policy, EP name, device type, compile option).

- **Tensors (inputs/outputs)**
  - Models take tensors (multi-dimensional arrays), e.g., images as `[N, C, H, W]` or `[N, H, W, C]`.
  - You must preprocess raw data (resize, normalize, layout conversion) into the model’s expected input tensor, and postprocess outputs (softmax, top-K, parsing) for human-readable results.

---

## How samples appear in the app (See [AddingSamples.md](AddingSamples.md) for the original quick guide)

- All samples live under `AIDevGallery/Samples`. Organization is flexible but grouping by model or feature is recommended.
- Each sample is a WinUI page defined by a pair: `Sample.xaml` + `Sample.xaml.cs`.
- The page class in `Sample.xaml.cs` must carry the `[GallerySample]` attribute.
  - This attribute provides display metadata (name, icon, scenario, NuGet packages, etc.). The app dynamically loads it and shows your page.
  - For a reference, see [ImageClassification.xaml.cs](../AIDevGallery/Samples/Open Source Models/Image Models/ImageNet/ImageClassification.xaml.cs).

---

## Link to a Scenario (ScenarioType)

- [scenarios.json](../AIDevGallery/Samples/scenarios.json) defines scenario groups and UI copy (e.g., “Classify Image”, “Detect Objects”).
- In `[GallerySample]`, set `Scenario = ScenarioType.YourScenario` to categorize your sample.
- If you need a new scenario, add it to [scenarios.json](../AIDevGallery/Samples/scenarios.json) and rebuild (it’s embedded via a source generator at compile time, not loaded at runtime).

---

## Link to model definitions (optional)

- If your sample introduces a new model family not previously used in the gallery, add under `Samples/Definitions/Models/`:
  - `.model.json`: a single model family definition
  - `.modelgroup.json`: multiple related families with display order, etc.
- If your sample demonstrates a particular API, add an `apis.json` under `Samples/Definitions/`.
- These definitions group samples on a single collection page. For a one-off page, you can skip this.

---

## What files to create

1) Create a new page at a suitable location, e.g.:
   - `AIDevGallery/Samples/MyModel/MySample.xaml`
   - `AIDevGallery/Samples/MyModel/MySample.xaml.cs`

2) In `MySample.xaml.cs`:
   - Inherit from `BaseSamplePage`
   - Add the `[GallerySample]` attribute with minimal metadata
   - Override `LoadModelAsync(SampleNavigationParameters sampleParams)` to initialize the model; call `sampleParams.NotifyCompletion()` when ready
   - Register `Unloaded` to dispose `InferenceSession` and other resources

---

## WinML initialization flow (project convention)

1) Ensure and register certified EPs:
   - Purpose: install/register certified hardware acceleration packages (e.g., DML) when available.
   - If it fails, gracefully fall back to CPU.

2) Create `SessionOptions` and register ORT extensions:
   - `sessionOptions.RegisterOrtExtensions()` enables additional operators many models need.

3) Honor `sampleParams.WinMlSampleOptions` for EP selection:
   - `Policy`: let the system pick the best EP
   - Or `EpName` + `DeviceType`: explicitly request a specific EP and device
   - `CompileModel`: pre-compile for faster subsequent runs (first-time cost, later speedup)

4) Create the `InferenceSession`:
   - `new InferenceSession(modelPath, sessionOptions)`
   - If compilation is enabled, the path may be replaced by a compiled artifact.

5) Call `sampleParams.NotifyCompletion()` when initialization finishes:
   - UI hides the loading spinner; the page is ready for interaction.

6) Dispose resources on page unload:
   - `this.Unloaded += (s, e) => _inferenceSession?.Dispose();`

---

## I/O, preprocessing, and postprocessing (image classification example)

- Inspect input name and shape (e.g., `NCHW`) and set `N=1`.
- Resize the image to the expected resolution (e.g., `224x224`), normalize per the model’s spec, and fill a `Tensor<float>`.
- Run `_inferenceSession.Run(inputs)`, then postprocess (softmax/top-K) and bind the results to your UI.

For concrete code, refer to `ImageClassification.xaml.cs` (`InitModel` / `ClassifyImage`).

---

## `[GallerySample]` minimal fields

- `Name`: display name
- `Model1Types`: required array of model types your sample supports
- `Scenario`: from `ScenarioType` generated by `scenarios.json`
- `NugetPackageReferences`: additional packages (e.g., `Microsoft.ML.OnnxRuntime.Extensions`)
- `Id`: unique GUID
- `Icon`: optional Segoe MDL2 glyph

Other optional fields (e.g., `SharedCode`) can be added as needed—see existing samples for patterns.

---

## Dev tips

- Use `ShowException(ex, "...message...")` to display a helpful dialog with copyable details.
- `SendSampleInteractedEvent(...)` can record user interactions for telemetry.
- First-time init/inference can be slow—`CompileModel` significantly speeds up subsequent runs.
- Input layout and preprocessing vary by model—check the model’s README or similar samples.

---

## Minimal Sample skeleton (copy, rename, and adapt)

> Replace namespace/class name/model type/scenario with yours. Comments map to the steps above.

```csharp
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Samples.Attributes;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MySamples
{
    [GallerySample(
        Name = "My Minimal WinML Sample",
        Model1Types = [ModelType.SqueezeNet], // pick a model type that matches your sample
        Scenario = ScenarioType.ImageClassifyImage, // pick a scenario defined in scenarios.json
        NugetPackageReferences = [
            "Microsoft.ML.OnnxRuntime.Extensions"
        ],
        Id = "00000000-0000-0000-0000-000000000000", // sample GUID
        Icon = "\uE8B9"
    )]
    internal sealed partial class MyMinimalSample : BaseSamplePage
    {
        private InferenceSession? _session;

        public MyMinimalSample()
        {
            // Step 6: ensure resources are released when page unloads
            this.Unloaded += (s, e) => _session?.Dispose();

            this.InitializeComponent();
        }

        protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
        {
            try
            {
                // Step 1/2/3: initialize session options and EP based on WinMlSampleOptions
                await InitializeSessionAsync(sampleParams.ModelPath, sampleParams.WinMlSampleOptions);

                // Step 5: notify the UI that model is ready (hide loading spinner)
                sampleParams.NotifyCompletion();

                // (Optional) Run a first inference or prepare UI state
            }
            catch (Exception ex)
            {
                ShowException(ex, "Failed to initialize model.");
            }
        }

        private Task InitializeSessionAsync(string modelPath, WinMlSampleOptions options)
        {
            return Task.Run(async () =>
            {
                if (_session != null)
                {
                    return; // already initialized
                }

                // Step 1: ensure certified EPs are present (e.g., DirectML)
                var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();
                try
                {
                    var _ = await catalog.EnsureAndRegisterCertifiedAsync();
                }
                catch (Exception installEx)
                {
                    Debug.WriteLine($"WARNING: Failed to install packages: {installEx.Message}");
                }

                // Step 2: create session options and register ORT extensions
                SessionOptions so = new();
                so.RegisterOrtExtensions();

                // Step 3: choose EP policy or a specific EP
                if (options.Policy != null)
                {
                    so.SetEpSelectionPolicy(options.Policy.Value);
                }
                else if (options.EpName != null)
                {
                    so.AppendExecutionProviderFromEpName(options.EpName, options.DeviceType);

                    // Step 3 (optional): pre-compile the model for faster subsequent runs
                    if (options.CompileModel)
                    {
                        modelPath = so.GetCompiledModel(modelPath, options.EpName) ?? modelPath;
                    }
                }

                // Step 4: create inference session
                _session = new InferenceSession(modelPath, so);
            });
        }

        // (Optional) Add methods for preprocessing, inference, and postprocessing
    }
}
```

---

## FAQ

- Is first inference slow?
  - Yes. Enabling `CompileModel` makes subsequent runs fast.

- How do I know the input size and preprocessing?
  - Check the model’s README and look at similar samples in this repo.

- Can I run without a dedicated GPU?
  - Yes. DirectML supports many integrated GPUs. If unavailable, CPU fallback still works.

- Why doesn’t my modified `scenarios.json` show up immediately?
  - It’s embedded via a source generator. Rebuild the app.

---

## Next steps

- Copy the minimal sample, change the namespace/class/model type/scenario, and run the app to see your page appear.
- If you need grouped presentation or a new model family, add `.model.json` / `.modelgroup.json` or `apis.json` under `Samples/Definitions`.

## Related links

- [Use ONNX APIs in Windows ML](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/use-onnx-apis)
- [Get started with Windows ML](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/get-started)