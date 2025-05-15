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
        IReadOnlyList<OrtEpDevice> epDevices = environment.GetEpDevices();
        Dictionary<string, List<OrtEpDevice>> epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (OrtEpDevice device in epDevices)
        {
            string name = device.EpName;

            if (!epDeviceMap.TryGetValue(name, out List<OrtEpDevice>? value))
            {
                value = [];
                epDeviceMap[name] = value;
            }

            value.Add(device);
        }

        // Configure execution providers
        foreach (KeyValuePair<string, List<OrtEpDevice>> epGroup in epDeviceMap)
        {
            string name = epGroup.Key;
            List<OrtEpDevice> devices = epGroup.Value;

            // Configure EP with all its devices
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);

            switch (name)
            {
                case "VitisAIExecutionProvider":
                    if (epName == "VitisAIExecutionProvider")
                    {
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return true;
                    }

                    break;

                case "OpenVINOExecutionProvider":
                    if (epName == "OpenVINOExecutionProvider")
                    {
                        // Configure threading for OpenVINO EP
                        epOptions["num_of_threads"] = "4";
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return true;
                    }

                    break;

                case "QNNExecutionProvider":
                    if (epName == "QNNExecutionProvider")
                    {
                        // Configure performance mode for QNN EP
                        epOptions["htp_performance_mode"] = "high_performance";
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return true;
                    }

                    break;

                case "DmlExecutionProvider":
                    if (epName == "DmlExecutionProvider")
                    {
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return true;
                    }

                    break;

                case "NvTensorRTRTXExecutionProvider":
                    if (epName == "DmlExecutionProvider")
                    {
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return true;
                    }
                    break;

                default:
                    break;
            }
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
}