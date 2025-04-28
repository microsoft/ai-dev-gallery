// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

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
                Icon = "ms-appx:///Assets/ModelIcons/Onnx.png", // TO DO : theme aware
                CreatePicker = () => new OnnxPickerView()
            }
        },
        {
            "wcr", new ModelPickerDefinition()
            {
                Name = "WCR",
                Id = "wcr",
                Icon = "ms-appx:///Assets/ModelIcons/WCRAPI.png",
                CreatePicker = () => new WinAIApiPickerView()
            }
        },
        {
            "ollama", new ModelPickerDefinition()
            {
                Name = "Ollama",
                Id = "ollama",
                Icon = "ms-appx:///Assets/ModelIcons/ollama.light.png", // TO DO : theme aware
                CreatePicker = () => new OllamaPickerView()
            }
        },
        {
            "openai", new ModelPickerDefinition()
            {
                Name = "OpenAI",
                Id = "openai",
                Icon = "ms-appx:///Assets/ModelIcons/OpenAI.png",
                CreatePicker = () => new OpenAIPickerView()
            }
        }
    };
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Id { get; set; }
    public required Func<BaseModelPickerView> CreatePicker { get; set; }
}