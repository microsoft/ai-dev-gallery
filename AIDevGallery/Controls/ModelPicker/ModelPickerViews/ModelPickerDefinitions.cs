// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIDevGallery.Controls.ModelPickerViews;
internal class ModelPickerDefinition
{
    public static readonly Dictionary<string, ModelPickerDefinition> Definitions = new()
    {
        {
            "onnx", new ModelPickerDefinition()
            {
                Name = "ONNX",
                Id = "onnx",
                Icon = "ms-appx:///Assets/ModelIcons/Onnx.png",
                CreatePicker = () => new OnnxPickerView()
            }
        },
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

    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Id { get; set; }
    public required Func<BaseModelPickerView> CreatePicker { get; set; }
    public Func<Task<bool>> IsAvailable { get; set; } = () => Task.FromResult(true);
}