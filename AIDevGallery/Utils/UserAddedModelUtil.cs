// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Utils;

internal static class UserAddedModelUtil
{
    public static async Task<bool> OpenAddModelFlow(XamlRoot targetRoot, List<Sample>? samples)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".onnx");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            HardwareAccelerator accelerator = HardwareAccelerator.CPU;

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

            List<ModelType> validatedModelTypes = GetValidatedModelTypesForUploadedOnnxModel(file.Path, samples);
            if (validatedModelTypes.Count > 0)
            {
                string id = "useradded-local-model-" + Guid.NewGuid().ToString();

                foreach (ModelType modelType in validatedModelTypes)
                {
                    foreach (ModelType leaf in GetLeafModelTypesFromParent(modelType))
                    {
                        App.AppData.AddModelTypeToUserAddedModelsMappingEntry(leaf, id);
                    }
                }

                await App.AppData.SaveAsync();

                var details = new ModelDetails()
                {
                    Id = id,
                    Name = modelName,
                    Url = $"local-file:///{file.Path}",
                    Description = "Localy added GenAI Model",
                    HardwareAccelerators = [accelerator],
                    IsUserAdded = true,
                    Size = new FileInfo(file.Path).Length,
                    ReadmeUrl = null,
                    License = "unknown"
                };

                await App.ModelCache.AddLocalModelToCache(details, file.Path);
                return true;
            }
        }

        return false;
    }

    private static List<ModelType> GetValidatedModelTypesForUploadedOnnxModel(string modelFilepath, List<Sample>? samples)
    {
        if (samples is null)
        {
            return [];
        }

        List<ModelType> validatedModelTypes = new();

        foreach (var s in samples)
        {
            var models = ModelDetailsHelper.GetModelDetails(s);
            foreach (var modelTypeDict in models)
            {
                foreach (ModelType modelType in modelTypeDict.Keys)
                {
                    if (ValidateUserAddedModelDimensionsForModelTypeModelDetails(modelTypeDict[modelType], modelFilepath))
                    {
                        validatedModelTypes.Add(modelType);
                    }
                }
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

    private static List<ModelType> GetLeafModelTypesFromParent(ModelType parentType)
    {
        Queue<ModelType> leafs = new();
        leafs.Enqueue(parentType);
        bool added = true;

        do
        {
            added = false;
            int initialCount = leafs.Count;

            for (int i = 0; i < initialCount; i++)
            {
                var leaf = leafs.Dequeue();
                if (Samples.ModelTypeHelpers.ParentMapping.TryGetValue(leaf, out List<ModelType>? values))
                {
                    if (values.Count > 0)
                    {
                        added = true;

                        foreach (var value in values)
                        {
                            leafs.Enqueue(value);
                        }
                    }
                }
                else
                {
                    leafs.Enqueue(leaf);
                }
            }
        }
        while (leafs.Count > 0 && added);

        return leafs.ToList();
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
}