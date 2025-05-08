// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AIDevGallery.Utils;
internal static class UserAddedModelUtilsTemp
{
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
}