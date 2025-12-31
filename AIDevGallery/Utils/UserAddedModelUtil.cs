// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Utils;

internal static class UserAddedModelUtil
{
    public static async Task OpenAddLanguageModelFlow(XamlRoot root)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();

        if (folder != null)
        {
            var config = Directory.GetFiles(folder.Path)
                .FirstOrDefault(r => Path.GetFileName(r) == "genai_config.json");

            if (string.IsNullOrEmpty(config) || App.ModelCache.Models.Any(m => m.Path == folder.Path))
            {
                var message = string.IsNullOrEmpty(config) ?
                    "The folder does not contain a model you can add. Ensure \"genai_config.json\" is present in the selected directory" :
                    "This model is already added";

                ContentDialog confirmFolderDialog = new()
                {
                    Title = "Can't add model",
                    Content = message,
                    XamlRoot = root,
                    CloseButtonText = "OK"
                };

                await confirmFolderDialog.ShowAsync();
                return;
            }

            HardwareAccelerator accelerator = HardwareAccelerator.CPU;
            string configContents = string.Empty;

            try
            {
                configContents = await File.ReadAllTextAsync(config);
                accelerator = GetHardwareAcceleratorFromConfig(configContents);
            }
            catch (Exception ex)
            {
                ContentDialog confirmFolderDialog = new()
                {
                    Title = "Can't read genai_config.json",
                    Content = ex.Message,
                    XamlRoot = root,
                    CloseButtonText = "OK"
                };

                await confirmFolderDialog.ShowAsync();
                return;
            }

            var (isValid, unavailableProviders) = ValidateExecutionProviders(configContents);
            if (!isValid)
            {
                var warningMessage = "This model requires execution providers that are not available on your device:\n\n" +
                    string.Join(", ", unavailableProviders) +
                    "\n\nThe model may fail to load or run. Do you want to add it anyway?";

                ContentDialog warningDialog = new()
                {
                    Title = "Incompatible Execution Providers",
                    Content = new TextBlock()
                    {
                        Text = warningMessage,
                        TextWrapping = TextWrapping.Wrap
                    },
                    XamlRoot = root,
                    CloseButtonText = "Cancel",
                    PrimaryButtonText = "Add Anyway",
                    DefaultButton = ContentDialogButton.Close,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                var warningResult = await warningDialog.ShowAsync();
                if (warningResult != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var nameTextBox = new TextBox()
            {
                Text = Path.GetFileName(folder.Path),
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
                Header = "Model name"
            };

            ContentDialog nameModelDialog = new()
            {
                Title = "Add model",
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock()
                        {
                            Text = $"Adding ONNX model from \n \"{folder.Path}\"",
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        nameTextBox
                    }
                },
                XamlRoot = root,
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Add",
                DefaultButton = ContentDialogButton.Primary,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            string modelName = nameTextBox.Text;

            nameTextBox.TextChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(nameTextBox.Text))
                {
                    nameModelDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    modelName = nameTextBox.Text;
                    nameModelDialog.IsPrimaryButtonEnabled = true;
                }
            };

            var result = await nameModelDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            await AddLanguageModelFromLocalFilepath(folder.Path, modelName, accelerator);
        }
    }

    public static async Task<bool> OpenAddModelFlow(XamlRoot targetRoot, List<ModelType> modelTypes)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".onnx");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            if (App.ModelCache.IsModelCached($"local-file:///{file.Path}"))
            {
                ContentDialog modelAlreadyAddedDialog = new()
                {
                    Title = "Model already added",
                    Content = $"A model at path \"{file.Path}\" has already been added. Please try a different model.",
                    XamlRoot = targetRoot,
                    CloseButtonText = "Close",
                    DefaultButton = ContentDialogButton.Close,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                await modelAlreadyAddedDialog.ShowAsync();
                return false;
            }

            var nameTextBox = new TextBox()
            {
                Text = Path.GetFileName(file.Path),
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
                Header = "Model name"
            };

            ContentDialog nameModelDialog = new()
            {
                Title = "Add model",
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 8,
                    Children =
                {
                    new TextBlock()
                    {
                        Text = $"Adding ONNX model from \n \"{file.Path}\"",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    nameTextBox
                }
                },
                XamlRoot = targetRoot,
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Add",
                DefaultButton = ContentDialogButton.Primary,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            string modelName = nameTextBox.Text;

            nameTextBox.TextChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(nameTextBox.Text))
                {
                    nameModelDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    modelName = nameTextBox.Text;
                    nameModelDialog.IsPrimaryButtonEnabled = true;
                }
            };

            var result = await nameModelDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return false;
            }

            await AddModelFromLocalFilePath(file.Path, modelName, modelTypes);
            return true;
        }

        return false;
    }

    private static List<ModelType> GetValidatedModelTypesForUploadedOnnxModel(string modelFilepath, List<ModelType> modelTypes)
    {
        return ModelDetailsHelper.GetModelDetailsForModelTypes(modelTypes)
            .Where(pair => ValidateUserAddedModelDimensionsForModelTypeModelDetails(pair.Value, modelFilepath))
            .Select(pair => pair.Key)
            .ToList();
    }

    private static bool ValidateUserAddedModelDimensionsForModelTypeModelDetails(List<ModelDetails> modelDetailsList, string modelFilepath)
    {
        using SessionOptions sessionOptions = new();
        sessionOptions.RegisterOrtExtensions();

        using InferenceSession inferenceSession = new(modelFilepath, sessionOptions);
        var inputDimensions = inferenceSession.InputMetadata.Select(kvp => kvp.Value.Dimensions).ToList();
        var outputDimensions = inferenceSession.OutputMetadata.Select(kvp => kvp.Value.Dimensions).ToList();

        return modelDetailsList.Any(modelDetails =>
            ValidateUserAddedModelAgainstModelDimensions(inputDimensions, outputDimensions, modelDetails));
    }

    private static bool ValidateUserAddedModelAgainstModelDimensions(List<int[]> inputDimensions, List<int[]> outputDimensions, ModelDetails modelDetails)
    {
        if (modelDetails.InputDimensions is null || modelDetails.OutputDimensions is null)
        {
            return false;
        }

        if (modelDetails.InputDimensions.Count != inputDimensions.Count || modelDetails.OutputDimensions.Count != outputDimensions.Count)
        {
            return false;
        }

        for (int i = 0; i < inputDimensions.Count; i++)
        {
            if (!(CompareDimension(modelDetails.OutputDimensions[i], outputDimensions[i]) && CompareDimension(modelDetails.InputDimensions[i], inputDimensions[i])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CompareDimension(int[] dimensionA, int[] dimensionB)
    {
        if (dimensionA.Length != dimensionB.Length)
        {
            return false;
        }

        for (int i = 0; i < dimensionA.Length; i++)
        {
            if ((dimensionA[i] is -1 or 1) && (dimensionB[i] is -1 or 1))
            {
                continue;
            }

            if (dimensionA[i] != dimensionB[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsModelsDetailsListUploadCompatible(this IEnumerable<ModelDetails> modelDetailsList)
    {
        return modelDetailsList.Any(m => m.InputDimensions != null && m.OutputDimensions != null);
    }

    public static HardwareAccelerator GetHardwareAcceleratorFromConfig(string configContents)
    {
        if (configContents.Contains(""""backend_path": "QnnHtp.dll"""", StringComparison.OrdinalIgnoreCase))
        {
            return HardwareAccelerator.QNN;
        }

        var config = JsonSerializer.Deserialize(configContents, SourceGenerationContext.Default.GenAIConfig);
        if (config == null)
        {
            throw new InvalidDataException("genai_config.json is not valid");
        }

        // Return based on priority: QNN > DML > NPU > GPU > CPU
        bool hasGpu = false;
        bool hasNpu = false;
        bool hasCpu = false;

        // Check all provider options from decoder-level and pipeline-level
        var allProviderOptions = GetAllProviderOptions(config);
        foreach (var provider in allProviderOptions)
        {
            var accelerator = CheckProviderForAccelerator(provider, ref hasGpu, ref hasNpu, ref hasCpu);
            if (accelerator.HasValue)
            {
                return accelerator.Value;
            }
        }

        if (hasNpu)
        {
            return HardwareAccelerator.NPU;
        }

        if (hasGpu)
        {
            return HardwareAccelerator.GPU;
        }

        return HardwareAccelerator.CPU;
    }

    private static IEnumerable<ProviderOptions> GetAllProviderOptions(GenAIConfig config)
    {
        foreach (var provider in config.Model.Decoder.SessionOptions.ProviderOptions)
        {
            yield return provider;
        }

        if (config.Model.Decoder.Pipeline == null)
        {
            yield break;
        }

        foreach (var pipelineItem in config.Model.Decoder.Pipeline)
        {
            if (pipelineItem.Stages == null)
            {
                continue;
            }

            foreach (var stageEntry in pipelineItem.Stages)
            {
                PipelineStage? stage = null;
                try
                {
                    stage = JsonSerializer.Deserialize(stageEntry.Value.GetRawText(), SourceGenerationContext.Default.PipelineStage);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (stage?.SessionOptions?.ProviderOptions != null)
                {
                    foreach (var provider in stage.SessionOptions.ProviderOptions)
                    {
                        yield return provider;
                    }
                }
            }
        }
    }

    private static HardwareAccelerator? CheckProviderForAccelerator(ProviderOptions provider, ref bool hasGpu, ref bool hasNpu, ref bool hasCpu)
    {
        if (provider.HasProvider("qnn"))
        {
            return HardwareAccelerator.QNN;
        }

        if (provider.HasProvider("dml"))
        {
            return HardwareAccelerator.DML;
        }

        var openvinoOptions = provider.GetProviderOptions("OpenVINO");
        if (openvinoOptions != null && openvinoOptions.TryGetValue("device_type", out var deviceType))
        {
            var devType = deviceType.ToLowerInvariant();
            if (devType == "npu")
            {
                hasNpu = true;
            }
            else if (devType == "gpu")
            {
                hasGpu = true;
            }
            else if (devType == "cpu")
            {
                hasCpu = true;
            }
        }

        if (provider.HasProvider("vitisai"))
        {
            hasNpu = true;
        }

        if (provider.HasProvider("cpu"))
        {
            hasCpu = true;
        }

        return null;
    }

    /// <summary>
    /// Validates that the execution providers specified in the genai_config.json are available on this device.
    /// </summary>
    /// <returns>A tuple with (isValid, unavailableProviders)</returns>
    private static (bool IsValid, List<string> UnavailableProviders) ValidateExecutionProviders(string configContents)
    {
        var config = JsonSerializer.Deserialize(configContents, SourceGenerationContext.Default.GenAIConfig);
        if (config == null)
        {
            return (false, new List<string> { "Invalid genai_config.json" });
        }

        var availableEPs = DeviceUtils.GetEpDevices()
            .Select(device => device.EpName)
            .Distinct()
            .ToList();
        var unavailableProviders = new List<string>();

        var providerMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "qnn", ExecutionProviderNames.QNN },
            { "dml", ExecutionProviderNames.DML },
            { "openvino", ExecutionProviderNames.OpenVINO },
            { "vitisai", ExecutionProviderNames.VitisAI },
            { "cuda", ExecutionProviderNames.CUDA },
            { "tensorrt", ExecutionProviderNames.TensorRT },
            { "cpu", ExecutionProviderNames.CPU }
        };

        var allProviderOptions = GetAllProviderOptions(config);
        foreach (var provider in allProviderOptions)
        {
            if (provider.ExtensionData == null)
            {
                continue;
            }

            foreach (var providerKey in provider.ExtensionData.Keys)
            {
                // Skip CPU as it's always available
                if (providerKey.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (providerMapping.TryGetValue(providerKey, out var expectedEP))
                {
                    if (!availableEPs.Any(ep => ep.Equals(expectedEP, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!unavailableProviders.Contains(providerKey, StringComparer.OrdinalIgnoreCase))
                        {
                            unavailableProviders.Add(providerKey);
                        }
                    }
                }
            }
        }

        return (unavailableProviders.Count == 0, unavailableProviders);
    }

    public static async Task<ModelDetails?> AddModelFromLocalFilePath(string filepath, string name, List<ModelType> modelTypes)
    {
        List<ModelType> validatedModelTypes = GetValidatedModelTypesForUploadedOnnxModel(filepath, modelTypes);
        if (validatedModelTypes.Count > 0)
        {
            string id = "useradded-local-model-" + Guid.NewGuid().ToString();

            foreach (ModelType modelType in validatedModelTypes)
            {
                App.AppData.AddModelTypeToUserAddedModelsMappingEntry(modelType, id);
            }

            await App.AppData.SaveAsync();

            var details = new ModelDetails()
            {
                Id = id,
                Name = name,
                Url = $"local-file:///{filepath}",
                Description = "Localy added ONNX model",
                HardwareAccelerators = [HardwareAccelerator.CPU, HardwareAccelerator.GPU, HardwareAccelerator.NPU],
                IsUserAdded = true,
                Size = new FileInfo(filepath).Length,
                ReadmeUrl = null,
                License = "unknown"
            };

            await App.ModelCache.AddLocalModelToCache(details, filepath);

            return details;
        }
        else
        {
            StringBuilder validDimensions = new();
            validDimensions.AppendLine("The model is not compatible with any of the supported model types. Dimmensions required:");
            foreach (var (type, models) in ModelDetailsHelper.GetModelDetailsForModelTypes(modelTypes))
            {
                foreach (var model in models)
                {
                    if (model.InputDimensions != null && model.OutputDimensions != null)
                    {
                        validDimensions.AppendLine();
                        validDimensions.AppendLine(CultureInfo.InvariantCulture, $"Model Type: {type}");
                        validDimensions.AppendLine(CultureInfo.InvariantCulture, $"Input: {FlattenWithBrackets(model.InputDimensions)}");
                        validDimensions.AppendLine(CultureInfo.InvariantCulture, $"Output: {FlattenWithBrackets(model.OutputDimensions)}");
                    }
                }
            }

            throw new InvalidDataException(validDimensions.ToString());

            string FlattenWithBrackets(List<int[]> list)
            {
                // Wrap every inner array in brackets, then join all those pieces
                return string.Join(" ", list.Select(arr => $"[{string.Join(", ", arr)}]"));
            }
        }
    }

    public static async Task<ModelDetails?> AddLanguageModelFromLocalFilepath(string filepath, string name, HardwareAccelerator accelerator)
    {
        DirectoryInfo dirInfo = new DirectoryInfo(filepath);
        long dirSize = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));

        var details = new ModelDetails()
        {
            Id = "useradded-local-languagemodel-" + Guid.NewGuid().ToString(),
            Name = name,
            Url = $"local-file:///{filepath}",
            Description = "Localy added GenAI Model",
            HardwareAccelerators = [accelerator],
            IsUserAdded = true,
            PromptTemplate = ModelDetailsHelper.GetTemplateFromName(filepath),
            Size = dirSize,
            ReadmeUrl = null,
            License = "unknown"
        };

        await App.ModelCache.AddLocalModelToCache(details, filepath);

        return details;
    }
}