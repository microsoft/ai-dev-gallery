// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode;

internal static class WinMLHelpers
{
    public static bool AppendExecutionProviderFromEpName(this SessionOptions sessionOptions, string epName, string? deviceType)
    {
        if (epName == "CPU")
        {
            // No need to append CPU execution provider
            return true;
        }

        var environment = OrtEnv.Instance();
        var epDeviceMap = GetEpDeviceMap();

        if (epDeviceMap.TryGetValue(epName, out var devices))
        {
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);
            switch (epName)
            {
                case ExecutionProviderNames.DML:
                    // Configure performance mode for Dml EP
                    // Dml some times have multiple devices which cause exception, we pick the first one here
                    sessionOptions.AppendExecutionProvider(environment, [devices[0]], epOptions);
                    return true;
                case ExecutionProviderNames.OpenVINO:
                    var device = devices.Where(d => d.HardwareDevice.Type.ToString().Equals(deviceType, StringComparison.Ordinal)).FirstOrDefault();
                    sessionOptions.AppendExecutionProvider(environment, [device], epOptions);
                    return true;
                case ExecutionProviderNames.QNN:
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
        if (IsCompileModelSupported(device) == false)
        {
            return null;
        }

        var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{device}.onnx";

        if (!File.Exists(compiledModelPath))
        {
            try
            {
                using OrtModelCompilationOptions compilationOptions = new(sessionOptions);
                compilationOptions.SetInputModelPath(modelPath);
                compilationOptions.SetOutputModelPath(compiledModelPath);
                compilationOptions.CompileModel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Model compilation failed for {device}: {ex.Message}");

                // Clean up any empty or corrupted files that may have been created
                if (File.Exists(compiledModelPath))
                {
                    try
                    {
                        File.Delete(compiledModelPath);
                        Debug.WriteLine($"Deleted corrupted compiled model file: {compiledModelPath}");
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }

                return null;
            }
        }

        // Validate that the compiled model file exists and is not empty
        if (File.Exists(compiledModelPath))
        {
            var fileInfo = new FileInfo(compiledModelPath);
            if (fileInfo.Length > 0)
            {
                return compiledModelPath;
            }
        }

        return null;
    }

    public static Dictionary<string, List<OrtEpDevice>> GetEpDeviceMap()
    {
        IReadOnlyList<OrtEpDevice> epDevices = DeviceUtils.GetEpDevices();
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

        return epDeviceMap;
    }

    /// <summary>
    /// Determines whether model compilation should be surfaced based on device type.
    /// </summary>
    /// <param name="deviceType">Device type string (e.g., "CPU", "GPU", "NPU").</param>
    /// <returns>False for CPU; true for other known accelerator types.</returns>
    public static bool IsCompileModelSupported(string? deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            return false;
        }

        // NOTE: Skip compilation for the CPU execution provider.
        // - EPContext is an EP-specific offline-compiled/partitioned graph artifact that requires the
        //   execution provider to implement serialization/deserialization of its optimized graph.
        // - ONNX Runtime's CPU EP does NOT implement EPContext model generation or loading. Invoking
        //   OrtModelCompilationOptions.CompileModel() for CPU attempts to emit a "*.CPU.onnx" EPContext
        //   artifact, which fails (commonly with InvalidProtobuf) because no EPContext is produced/understood
        //   by the CPU EP.
        // Behavior:
        // - For CPU, we return null here so callers fall back to the original ONNX model without attempting
        //   EPContext compilation.
        // - Other EPs (e.g., DirectML, OpenVINO, QNN) may support EPContext depending on the ORT build,
        //   platform drivers, and hardware; for those we allow compilation to proceed.
        if (string.Equals(deviceType, "CPU", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(deviceType, "GPU", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deviceType, "NPU", StringComparison.OrdinalIgnoreCase);
    }
}