// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Pages;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var files = Directory.GetFiles(folder.Path);
            var config = files.Where(r => Path.GetFileName(r) == "genai_config.json").FirstOrDefault();

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

            try
            {
                string configContents = string.Empty;
                configContents = await File.ReadAllTextAsync(config);
                accelerator = UserAddedModelUtil.GetHardwareAcceleratorFromConfig(configContents);
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
            if(App.ModelCache.IsModelCached($"local-file:///{file.Path}"))
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
        List<ModelType> validatedModelTypes = new();

        foreach (var (type, models) in ModelDetailsHelper.GetModelDetailsForModelTypes(modelTypes))
        {
            if (ValidateUserAddedModelDimensionsForModelTypeModelDetails(models, modelFilepath))
            {
                validatedModelTypes.Add(type);
            }
        }

        return validatedModelTypes;
    }

    private static bool ValidateUserAddedModelDimensionsForModelTypeModelDetails(List<ModelDetails> modelDetailsList, string modelFilepath)
    {
        using InferenceSession inferenceSession = new(modelFilepath);
        List<int[]> inputDimensions = new();
        List<int[]> outputDimensions = new();

        inputDimensions.AddRange(inferenceSession.InputMetadata.Select(kvp => kvp.Value.Dimensions));
        outputDimensions.AddRange(inferenceSession.OutputMetadata.Select(kvp => kvp.Value.Dimensions));

        foreach (ModelDetails modelDetails in modelDetailsList)
        {
            if (ValidateUserAddedModelAgainstModelDimensions(inputDimensions, outputDimensions, modelDetails))
            {
                return true;
            }
        }

        return false;
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
        foreach(ModelDetails modelDetails in modelDetailsList)
        {
            if(modelDetails.InputDimensions != null && modelDetails.OutputDimensions != null)
            {
                return true;
            }
        }

        return false;
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
            throw new FileLoadException("genai_config.json is not valid");
        }

        if (config.Model.Decoder.SessionOptions.ProviderOptions.Any(p => p.Dml != null))
        {
            return HardwareAccelerator.DML;
        }

        return HardwareAccelerator.CPU;
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
                HardwareAccelerators = [HardwareAccelerator.CPU],
                IsUserAdded = true,
                Size = new FileInfo(filepath).Length,
                ReadmeUrl = null,
                License = "unknown"
            };

            await App.ModelCache.AddLocalModelToCache(details, filepath);

            return details;
        }

        return null;
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