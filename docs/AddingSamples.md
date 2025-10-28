Read this doc on how to add a sample to the app.

**Note**: The app is very much in early stages of development and the sample architecture changes frequently - I will try to keep this file updated, but it's likely that it could be out of date.

## Samples

All samples go in the `Samples` folder. The samples are loaded dynamically, so all you need to do is to annotate your sample class with the [GallerySample] attribute to get them to show up in the app. You also need to know the structure of the Samples folder.

A sample is defined by a `.xaml` and a `.xaml.cs` file, where the class in your `.xaml.cs` needs to be annotated with the [GallerySample] attribute. The [GallerySample] attribute contains metadata about the sample, and the `.xaml.cs`/`.xaml` file is the entry point to the sample. You can add your files anywhere within the Samples folder. Try to group them properly.

If your sample uses a Model that hasn't been used before in the gallery app, you might also need to include a new model definition file inside the `Samples\Definitions\Models\` directory. Two formats are supported:
- `.model.json` - A simpler format for defining a single model family
- `.modelgroup.json` - Used to define a group of related model families with additional grouping metadata

If your sample uses a specific API, add it to an `apis.json` file anywhere under the `Samples\Definitions\` directory. The `.model.json`, `.modelgroup.json`, and `apis.json` files are used to group samples together in the app and they show up as a single page where all samples are rendered at the same time.

A sample folder that is not inside a model or api folder renders as a single sample in the app. These are good for samples that are higher level concepts (such as guides), or samples that combine multiple models or APIs.

### `Sample.xaml` and `Sample.xaml.cs`
This is the entry point to the sample - whatever you put here will show up when the sample is loaded. 

If you are creating a sample for a model, your sample should inherit from `BaseSamplePage` and override the `LoadModelAsync` method. The model downloading is handled for you. To load the model, override the LoadModelAsync method in your Sample.xaml.cs file:

```csharp
protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
{
    try
    {
        // For language models, get an IChatClient instance
        chatClient = await sampleParams.GetIChatClientAsync();
        
        // sampleParams.NotifyCompletion() should be called when the model is ready
        // This will hide the loading spinner in the UI
    }
    catch (Exception ex)
    {
        ShowException(ex);
    }

    sampleParams.NotifyCompletion();
}
```

If your sample requires two models (for example, a language model plus an embeddings model), override the multi-model overload instead:

```csharp
protected override async Task LoadModelAsync(MultiModelSampleNavigationParameters sampleParams)
{
    try
    {
        // Example: initialize second model using sampleParams.ModelPaths[1]
        // ... your model-2 init code ...

        // For language models, still use the chat client helper
        chatClient = await sampleParams.GetIChatClientAsync();
    }
    catch (Exception ex)
    {
        ShowException(ex);
    }

    sampleParams.NotifyCompletion();
}
```

It is also important to ensure that the model and any resources are disposed when the page is unloaded. To do this, register a handler to the `Unloaded` event in the constructor of the Sample.xaml.cs file:

```csharp
public Sample()
{
    this.Unloaded += (s, e) =>
    {
        // clean up code goes here
        // eg: model.Dispose();
    };

    this.InitializeComponent();
}
```

### `[GallerySample]`
This is an attribute that contains the sample icon, links and more that show up across the app - the properties changes as new metadata is introduced, so look at how other samples are done for what to put here.

```csharp
[GallerySample(
    Name = "Paraphrase",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextParaphraseText,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Id = "9e006e82-8e3f-4401-8a83-d4c4c59cc20c",
    Icon = "\uE8D4")]
```

Key properties:
- `Name`: Display name of the sample
- `Model1Types`: Array of model types this sample supports (required)
- `Model2Types`: Optional second model type array if the sample uses multiple models
- `Scenario`: The scenario type defined in `scenarios.json`
- `SharedCode`: Optional array of shared code items for sample export; values come from `SharedCodeEnum` (auto-generated from files under `Samples\SharedCode` and `AIDevGallery\Utils\DeviceUtils`). Most basic language model samples can omit this.
- `NugetPackageReferences`: Array of NuGet package names needed
- `AssetFilenames`: Array of asset files needed by this sample
- `Id`: Unique identifier (GUID)
- `Icon`: Icon glyph for display

The `Scenario` is used to group samples together by scenario. If you have multiple samples that are demonstrating the same scenario (for example: summarizing text), you can give them the same `Scenario` and they will show up together in the app. The scenario must be first defined in `Samples\scenarios.json`. This file is embedded at build time by a source generator — edit it and rebuild to take effect (it is not loaded dynamically at runtime).

Prompt templates for language models are defined in `Samples\promptTemplates.json` and are also embedded by a source generator.


### `.model.json` and `.modelgroup.json`

Both `.model.json` and `.modelgroup.json` formats are supported for defining models. The main difference is:
- `.model.json` - Used for a single model family
- `.modelgroup.json` - Used for grouping multiple related model families with additional metadata like display order

#### `.model.json` format
This format defines a single model family with its variations:

```json
{
  "Phi3Mini": {
    "Id": "phi3mini",
    "Name": "Phi 3 Mini",
    "Description": "Phi-3 Mini is a 3.8B parameter, lightweight, state-of-the-art open language model",
    "DocsUrl": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx",
    "Models": {
      "Phi3MiniDirectML": {
        "Id": "202d6eaa-7a65-4d40-b2c3-53f1ef12a4eb",
        "Name": "Phi 3 Mini DirectML",
        "Url": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/tree/main/directml/directml-int4-awq-block-128",
        "Description": "Phi 3 Mini DirectML will run on your GPU",
        "HardwareAccelerator": "GPU",
        "Size": 2135763374,
        "Icon": "Microsoft.svg",
        "ParameterSize": "3.8B",
        "PromptTemplate": "Phi3",
        "License": "mit"
      },
      "Phi3MiniCPU": {
        ...
      }
    },
    "ReadmeUrl": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/blob/main/README.md"
  }
}
```

#### `.modelgroup.json` format
This format groups multiple related model families together with additional metadata like display order:

```json
{
  "LanguageModels": {
    "Id": "615e2f1c-ea95-4c34-9d22-b1e8574fe476",
    "Name": "Language",
    "Icon": "\uE8BD",
    "Order": 1,
    "Models": {
      "Phi3Mini": {
        "Id": "phi3mini",
        "Name": "Phi 3 Mini",
        "Description": "Phi-3 Mini is a 3.8B parameter, lightweight, state-of-the-art open language model",
        "DocsUrl": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx",
        "Models": {
          "Phi3MiniDirectML": {
            "Id": "202d6eaa-7a65-4d40-b2c3-53f1ef12a4eb",
            "Name": "Phi 3 Mini DirectML",
            "Url": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/tree/main/directml/directml-int4-awq-block-128",
            "Description": "Phi 3 Mini DirectML will run on your GPU",
            "HardwareAccelerator": "GPU",
            "Size": 2135763374,
            "Icon": "Microsoft.svg",
            "ParameterSize": "3.8B",
            "PromptTemplate": "Phi3",
            "License": "mit",
            "AIToolkitActions": ["Playground", "PromptBuilder"],
            "AIToolkitId": "Phi-3-mini-4k-directml-int4-awq-block-128-onnx"
          },
          "Phi3MiniCPU": {
            ...
          }
        },
        "ReadmeUrl": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/blob/main/README.md"
      }
    }
  }
}
```

#### Key fields for both formats:

**Model Family fields** (applies to both `.model.json` and models within `.modelgroup.json`):
- `Id`: Unique identifier for the model family
- `Name`: Display name
- `Description`: Description of the model family
- `DocsUrl`: External link to the model family's documentation homepage/repository overview, used for external link and source display.
- `ReadmeUrl`: Direct link to the model family's README markdown that the app fetches and renders in-app
- `Models`: Dictionary of model variations with different hardware requirements
  - `Url`: HuggingFace URL - can point to repo root, a subfolder, or a single file
  - `HardwareAccelerator`: One or more of: "CPU", "GPU", "DML", "QNN", "NPU", "WCRAPI", "OLLAMA", "OPENAI", "VitisAI", "OpenVINO" (or an array like ["CPU", "GPU"])
  - `Size`: Model size in bytes
  - `License`: License type (e.g., "mit", "apache-2.0")
  - `PromptTemplate`: Prompt template to use (for language models)
  - `SupportedOnQualcomm`: Whether the model is supported on Qualcomm devices (optional)
  - `Icon`: Icon filename shown for the model (optional)
  - `FileFilter`: A string or array of strings to filter selectable files (optional)
  - `AIToolkitActions`: Actions available in AI Toolkit (optional)
  - `AIToolkitId`: AI Toolkit identifier (optional)
  - `AIToolkitFinetuningId`: AI Toolkit finetuning identifier (optional)
  - `InputDimensions` / `OutputDimensions`: Model input/output dimensions (optional)

**Additional fields for `.modelgroup.json` only:**
- `Order`: Display order in the app (optional) - this field is only available in `.modelgroup.json`
- `Models`: Dictionary of model families within this group (in `.modelgroup.json`, this wraps multiple model families)


## Adding a sample

To add a sample, follow these steps:

1. **Identify the model or API**: Determine which model or Windows AI API your sample will use.

2. **Create or update model definitions** (if needed):
   If your sample uses a Model that hasn't been used before in the gallery app, you might also need to include a new model file (`.model.json`)inside the `Samples\Definitions\` directory. 
   - The `.model.json` file is used to define a collection of samples that use a specific model family
   - You can also add a `.modelgroup.json` file to organize the model family within a model group
   - The `apis.json` file is used to define a collection of samples that use a specific API

3. **Create the sample files**: Add a new WinUI Blank Page in the appropriate folder: `Samples\[CategoryName]\[SampleName].xaml`

4. **Implement the sample**:
   - Make your sample class inherit from `BaseSamplePage`
   - Add the `[GallerySample]` attribute with appropriate metadata (use `Model1Types` array, `Scenario`, etc.)
   - Override `LoadModelAsync(SampleNavigationParameters sampleParams)` for single-model samples, or `LoadModelAsync(MultiModelSampleNavigationParameters sampleParams)` for dual-model samples
   - Use `await sampleParams.GetIChatClientAsync()` for language models or appropriate methods for other model types (See [Common entry points cheat sheet](#common-entry-points))
   - For WinML-based models, use `sampleParams.WinMlSampleOptions` for EP policy, device type, and compile options
   - Call `sampleParams.NotifyCompletion()` when initialization is complete
   - Register cleanup code in the `Unloaded` event handler

5. **Test**: Run the app - the sample should show up as part of the model or API collection, or as a standalone page if it's not part of a collection.

### Common entry points

- Language models
  - Use from navigation params: `await sampleParams.GetIChatClientAsync()`
  - Factory (direct use): `await OnnxRuntimeGenAIChatClientFactory.CreateAsync(modelDir, LlmPromptTemplate?)`
  - Phi Silica: `await PhiSilicaClient.CreateAsync()`

- Embeddings
  - Create: `var embeddings = await EmbeddingGenerator.CreateAsync(modelPath, sampleParams.WinMlSampleOptions)`
  - Use: `await embeddings.GenerateAsync(values)` or `await foreach (var v in embeddings.GenerateStreamingAsync(values)) { ... }`

- Speech (Whisper)
  - Create: `var whisper = await WhisperWrapper.CreateAsync(modelPath, sampleParams.WinMlSampleOptions)`
  - Use: `await whisper.TranscribeAsync(pcmBytes, language, WhisperWrapper.TaskType.Transcribe)`

- Image generation (Stable Diffusion)
  - Init: `var sd = new StableDiffusion(modelFolder); await sd.InitializeAsync(sampleParams.WinMlSampleOptions);`
  - Inference: `var image = sd.Inference(prompt, cancellationToken);`