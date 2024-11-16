Read this doc on how to add a sample to the app.

**Note**: The app is very much in early stages of development and the sample architecture changes frequently - I will try to keep this file updated, but it's likely that it could be out of date.

## Samples

All samples go in the `Samples` folder. The samples are loaded dynamically, so all you need to do is to annotate your sample class with the [GallerySample] attribute to get them to show up in the app. You also need to know the structure of the Samples folder.

A sample is defined by a `.xaml` and a `.xaml.cs` file, where the class in your `.xaml.cs` needs to be annotated with the [GallerySample] attribute. The [GallerySample] attribute contains metadata about the sample, and the `.xaml.cs`/`.xaml` file is the entry point to the sample. You can add your files anywhere within the Samples folder. Try to group them properly.

If your sample uses a Model that hasn't been used before in the gallery app, you might also need to include a new model file (`.model.json`)inside the `Samples\ModelsDefinitions\` directory. The `.model.json` file is used to define a collection of samples that use a specific model family, and the `apis.json` file is used to define a collection of samples that use a specific API. You can also add a `.modelgroup.json` file to organize the model family within a model group. The `.model.json`, `.modelgroup.json`, and `apis.json` files are used to group samples together in the app and they show up as a single page where all samples are rendered at the same time.

A sample folder that is not inside a model or api folder renders as a single sample in the app. These are good for samples that are higher level concepts (such as guides), or samples that combine multiple models or APIs.

### `Sample.xaml` and `Sample.xaml.cs`
This is the entry point to the sample - whatever you put here will show up when the sample is loaded. 

If you are creating a sample for a model, and your sample is part of a model collection, the model downloading is handled for you. To get the path of the model on disk, override the OnNavigatedTo method in your Sample.xaml.cs file:

```csharp
protected override void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    if (e.Parameter is SampleNavigationParameters params)
    {
        // path to the model on disk is params.CachedModel.Path
        // if params.CachedModel.IsFile is true, the Path points to a file
        // otherwise, it points to a folder
        // this is determined by the model url in the model.json
        // params.CachedModel.Details contains the model details as defined in model.json
        // such as the url, description, and hardware requirements
        // 
        // params.RequestWaitForCompletion() is an optional method
        // to indicate to the SampleContainer that you are waiting for the model to load
        // this will show a loading spinner in the UI
        // once the model is loaded, call params.NotifyCompletion()
    }
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
    ModelType = ModelType.ResNet,
    Scenario = ScenarioType.ImageClassifyImage,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.LabelMap,
        SharedCodeEnum.BitmapFunctions
    ],
    Name = "Image Classification",
    Id = "09d73ba7-b877-45f9-9df7-41898ab4d339",
    Icon = "\uE8B9")]
```

The `Scenario` is used to group samples together by scenario. If you have multiple samples that are demonstrating the same scenario (for example: summarizing text), you can give them the same `Scenario` and they will show up together in the app. The scenario must be first defined in `scenarios.json` in the root of the `Samples` folder.


### `.model.json`
This is a metadata file that contains the model details. The model details are used to group samples together by model that might have different hardware requirements. 

```json
{
  "LanguageModels": {
    "Id": "615e2f1c-ea95-4c34-9d22-b1e8574fe476",
    "Name": "Language Models",
    "Icon": "\uE8BD",
    "Models": {
      "Phi3": {
        "Id": "phi3",
        "Name": "Phi 3",
        "Icon": "\uE8BD",
        "Description": "Phi 3",
        "DocsUrl": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx",
        "PromptTemplate": "Phi3",
        "Models": {
          "Phi3MiniDirectML": {
            "Id": "202d6eaa-7a65-4d40-b2c3-53f1ef12a4eb",
            "Name": "Phi 3 Mini DirectML",
            "Url": "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/tree/main/directml/directml-int4-awq-block-128",
            "Description": "Phi 3 Mini DirectML",
            "HardwareAccelerator": "DML",
            "Size": 2135763374,
            "ParameterSize": "3.8B"
          },
          "Phi3MiniCPU": {
            ...
          }
        }
      }
    }
  }
}
```

The Models dictionary contains the different variations of the model that are available. The Url of the model is used to download the model from HuggingFace and it could point to the root of the HF repo, directly to a subfolder, or a single file.


## Adding a sample

To add a sample, first identify the model or API that the sample is using. If there is no `.model.json`/`.modelgroup.json` for the model or API, create a new `.model.json`/`.modelgroup.json` under the `ModelsDefinitions` folder. 

Add a new WinUI Blank Page in the desired folder called `[YourSampleName].xaml`, and add the proper `[GallerySample]` attribute with the appropriate metadata as defined above. If adding a sample for a model, override the OnNavigatedTo method to get the path of the model on disk, as described above.

Run the app - the sample should show up as part of the model or API collection, or as a standalone page if it's not part of a collection.