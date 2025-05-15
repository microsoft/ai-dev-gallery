// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.IO;

namespace AIDevGallery.Samples.SharedCode;
internal static class WinMLHelpers
{
    public static bool AppendExecutionProviderFromEpName(this SessionOptions sessionOptions, string epName, OrtEnv? environment = null)
    {
        if (epName == "CPU")
        {
            // No need to append CPU execution provider
            return true;
        }

        environment ??= OrtEnv.Instance();
        var epDeviceMap = GetEpDeviceMap(environment);

        if (epDeviceMap.TryGetValue(epName, out var devices))
        {
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);
            switch(epName)
            {
                case "OpenVINOExecutionProvider":
                    // Configure threading for OpenVINO EP
                    epOptions["num_of_threads"] = "4";
                    break;
                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    break;
                default:
                    break;
            }

            sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
            return true;
        }

        return false;
    }

    public static string? GetCompiledModel(this SessionOptions sessionOptions, string modelPath, string device)
    {
        var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{device}.onnx";

        if (!File.Exists(compiledModelPath))
        {
            using OrtModelCompilationOptions compilationOptions = new(sessionOptions);
            compilationOptions.SetInputModelPath(modelPath);
            compilationOptions.SetOutputModelPath(compiledModelPath);
            compilationOptions.CompileModel();
        }

        if (File.Exists(compiledModelPath))
        {
            return compiledModelPath;
        }

        return null;
    }

    private static Dictionary<string, List<OrtEpDevice>>? _epDeviceMap;

    public static Dictionary<string, List<OrtEpDevice>> GetEpDeviceMap(OrtEnv? environment = null)
    {
        if (_epDeviceMap == null)
        {
            environment ??= OrtEnv.Instance();
            IReadOnlyList<OrtEpDevice> epDevices = environment.GetEpDevices();
            _epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

            foreach (OrtEpDevice device in epDevices)
            {
                string name = device.EpName;

                if (!_epDeviceMap.TryGetValue(name, out List<OrtEpDevice>? value))
                {
                    value = [];
                    _epDeviceMap[name] = value;
                }

                value.Add(device);
            }
        }

        return _epDeviceMap;
    }
}