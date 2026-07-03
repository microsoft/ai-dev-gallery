// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIDevGallery.Controls.ModelPickerViews;

internal class ModelPickerDefinition : ObservableObject
{
    public static readonly Dictionary<string, ModelPickerDefinition> Definitions = new()
    {
        {
            "winai", new ModelPickerDefinition()
            {
                Name = "Windows AI APIs",
                Id = "winai",
                Icon = "ms-appx:///Assets/ModelIcons/WCRAPI.png",
                CreatePicker = () => new WinAIApiPickerView()
            }
        },
        {
            "fl", new ModelPickerDefinition()
            {
                Name = "Foundry Local",
                Id = "fl",
                Icon = "ms-appx:///Assets/ModelIcons/Foundry.png",
                CreatePicker = () => new FoundryLocalPickerView()
            }
        },
        {
            "onnx", new ModelPickerDefinition()
            {
                Name = "Custom models",
                Id = "onnx",
                Icon = $"ms-appx:///Assets/ModelIcons/CustomModel{AppUtils.GetThemeAssetSuffix()}.png",
                CreatePicker = () => new OnnxPickerView()
            }
        },
        {
            "ollama", new ModelPickerDefinition()
            {
                Name = "Ollama",
                Id = "ollama",
                Icon = $"ms-appx:///Assets/ModelIcons/Ollama{AppUtils.GetThemeAssetSuffix()}.png",
                CreatePicker = () => new OllamaPickerView(),
                IsAvailable = OllamaModelProvider.Instance.IsAvailable
            }
        },
        {
            "openai", new ModelPickerDefinition()
            {
                Name = "OpenAI",
                Id = "openai",
                Icon = $"ms-appx:///Assets/ModelIcons/OpenAI{AppUtils.GetThemeAssetSuffix()}.png",
                CreatePicker = () => new OpenAIPickerView()
            }
        },
        {
            "lemonade", new ModelPickerDefinition()
            {
                Name = "Lemonade",
                Id = "lemonade",
                Icon = $"ms-appx:///Assets/ModelIcons/lemonade.png",
                CreatePicker = () => new LemonadePickerView(),
                IsAvailable = LemonadeModelProvider.Instance.IsAvailable
            }
        }
    };

    private string icon = string.Empty;

    public required string Name { get; set; }

    public required string Icon
    {
        get => icon;
        set => SetProperty(ref icon, value);
    }

    public required string Id { get; set; }
    public required Func<BaseModelPickerView> CreatePicker { get; set; }
    public Func<Task<bool>> IsAvailable { get; set; } = () => Task.FromResult(true);

    public static void RefreshThemeAwareIcons()
    {
        string suffix = AppUtils.GetThemeAssetSuffix();
        Definitions["onnx"].Icon = $"ms-appx:///Assets/ModelIcons/CustomModel{suffix}.png";
        Definitions["ollama"].Icon = $"ms-appx:///Assets/ModelIcons/Ollama{suffix}.png";
        Definitions["openai"].Icon = $"ms-appx:///Assets/ModelIcons/OpenAI{suffix}.png";
    }
}