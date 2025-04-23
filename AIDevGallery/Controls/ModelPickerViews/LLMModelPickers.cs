// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace AIDevGallery.Controls.LanguageModelPickerViews;
internal static class LLMModelPickers
{
    public static readonly Dictionary<string, Type> LLMModelPickerTypes = new()
    {
        { "onnx", typeof(OnnxPickerView) },
        { "ollama", typeof(OllamaPickerView) },
    };
}