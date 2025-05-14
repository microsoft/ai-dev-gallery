// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Samples.SharedCode;
internal static class WinMLHelpers
{
    public static string? AppendNPUExecutionProvider(this SessionOptions sessionOptions, OrtEnv? environment = null)
    {
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
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "Vitis";

                case "OpenVINOExecutionProvider":
                    // Configure threading for OpenVINO EP
                    epOptions["num_of_threads"] = "4";
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "OpenVINO";

                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "QNN";

                case "NvTensorRTRTXExecutionProvider":
                    // Configure performance mode for TensorRT RTX EP
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "NvTensorRTRTX";

                default:
                    break;
            }
        }

        return null;
    }

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
}