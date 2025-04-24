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
                Name = "onnx",
                Id = "onnx",
                CreatePicker = () => new OnnxPickerView()
            }
        },
        {
            "wcr", new ModelPickerDefinition()
            {
                Name = "wcr",
                Id = "wcr",
                CreatePicker = () => new WinAIApiPickerView()
            }
        },
        {
            "ollama", new ModelPickerDefinition()
            {
                Name = "ollama",
                Id = "ollama",
                CreatePicker = () => new OllamaPickerView()
            }
        }
    };
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Id { get; set; }
    public required Func<BaseModelPickerView> CreatePicker { get; set; }
}