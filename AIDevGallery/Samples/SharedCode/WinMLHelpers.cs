// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.IO;

namespace AIDevGallery.Samples.SharedCode;
internal static class WinMLHelpers
{
    public static string? AppendExecutionProviderForPreferedEp(this SessionOptions sessionOptions, string preferedEP, OrtEnv? environment = null)
    {
        if (preferedEP == "CPU")
        {
            return "CPU";
        }

        environment ??= OrtEnv.Instance();
        IReadOnlyList<OrtEpDevice> epDevices = environment.GetEpDevices();
        Dictionary<string, List<OrtEpDevice>> epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (OrtEpDevice device in epDevices)
        {
            string epName = device.EpName;

            if (!epDeviceMap.TryGetValue(epName, out List<OrtEpDevice>? value))
            {
                value = [];
                epDeviceMap[epName] = value;
            }

            value.Add(device);
        }

        // Configure execution providers
        foreach (KeyValuePair<string, List<OrtEpDevice>> epGroup in epDeviceMap)
        {
            string epName = epGroup.Key;
            List<OrtEpDevice> devices = epGroup.Value;

            // Configure EP with all its devices
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);

            switch (epName)
            {
                case "VitisAIExecutionProvider":
                    if (preferedEP == "Vitis" || preferedEP == "NPU")
                    {
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return "Vitis";
                    }

                    break;

                case "OpenVINOExecutionProvider":
                    if (preferedEP == "OpenVINO" || preferedEP == "NPU")
                    {
                        // Configure threading for OpenVINO EP
                        epOptions["num_of_threads"] = "4";
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return "OpenVINO";
                    }

                    break;

                case "QNNExecutionProvider":
                    if (preferedEP == "QNN" || preferedEP == "NPU")
                    {
                        // Configure performance mode for QNN EP
                        epOptions["htp_performance_mode"] = "high_performance";
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return "QNN";
                    }

                    break;

                case "DmlExecutionProvider":
                    if (preferedEP == "DML" || preferedEP == "GPU")
                    {
                        sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                        return "DML";
                    }

                    break;

                default:
                    break;
            }
        }

        return null;
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