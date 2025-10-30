// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal static class WinMLHelpers
{
    public static bool AppendExecutionProviderFromEpName(this SessionOptions sessionOptions, string epName, string? deviceType, OrtEnv? environment = null)
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
            switch (epName)
            {
                case "DmlExecutionProvider":
                    // Configure performance mode for Dml EP
                    // Dml some times have multiple devices which cause exception, we pick the first one here
                    sessionOptions.AppendExecutionProvider(environment, [devices[0]], epOptions);
                    return true;
                case "OpenVINOExecutionProvider":
                    var device = devices.Where(d => d.HardwareDevice.Type.ToString().Equals(deviceType, StringComparison.Ordinal)).FirstOrDefault();
                    sessionOptions.AppendExecutionProvider(environment, [device], epOptions);
                    return true;
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
        // NOTE: Skip compilation for the CPU execution provider.
        // Rationale:
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
        if (string.Equals(device, "CPU", StringComparison.OrdinalIgnoreCase))
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

    public static Dictionary<string, List<OrtEpDevice>> GetEpDeviceMap(OrtEnv? environment = null)
    {
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

        return epDeviceMap;
    }

    /// <summary>
    /// Determines whether model compilation should be surfaced based on device type.
    /// </summary>
    /// <param name="deviceType">Device type string (e.g., "CPU", "GPU", "NPU").</param>
    /// <param name="environment">Unused; kept for signature stability if needed later.</param>
    /// <returns>False for CPU; true for other known accelerator types.</returns>
    public static bool IsCompileModelSupported(string? deviceType, OrtEnv? environment = null)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            return false;
        }

        if (string.Equals(deviceType, "CPU", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(deviceType, "GPU", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deviceType, "NPU", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests if DirectML execution provider is available on the system.
    /// </summary>
    /// <returns>True if DML is available, false otherwise.</returns>
    public static async Task<bool> TestDmlAvailability()
    {
        try
        {
            await Task.Run(() =>
            {
                // Create a minimal ONNX model in memory (identity operation)
                byte[] minimalModel = new byte[]
                {
                    0x08, 0x07, 0x12, 0x07, 0x62, 0x61, 0x63, 0x6B, 0x65, 0x6E, 0x64, 0x1A, 0x0D, 0x62, 0x61, 0x63, 0x6B,
                    0x65, 0x6E, 0x64, 0x2D, 0x74, 0x65, 0x73, 0x74, 0x22, 0x46, 0x0A, 0x08, 0x0A, 0x01, 0x78, 0x12, 0x01,
                    0x79, 0x22, 0x08, 0x49, 0x64, 0x65, 0x6E, 0x74, 0x69, 0x74, 0x79, 0x2A, 0x14, 0x0A, 0x04, 0x74, 0x79,
                    0x70, 0x65, 0x12, 0x0C, 0x0A, 0x01, 0x69, 0x12, 0x07, 0x0A, 0x05, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x3A,
                    0x18, 0x0A, 0x07, 0x65, 0x78, 0x61, 0x6D, 0x70, 0x6C, 0x65, 0x3A, 0x0D, 0x49, 0x64, 0x65, 0x6E, 0x74,
                    0x69, 0x74, 0x79, 0x20, 0x74, 0x65, 0x73, 0x74, 0x0A, 0x16, 0x0A, 0x01, 0x78, 0x12, 0x11, 0x0A, 0x0F,
                    0x08, 0x01, 0x12, 0x0B, 0x0A, 0x01, 0x4E, 0x0A, 0x01, 0x43, 0x0A, 0x01, 0x48, 0x0A, 0x01, 0x57, 0x0A,
                    0x16, 0x0A, 0x01, 0x79, 0x12, 0x11, 0x0A, 0x0F, 0x08, 0x01, 0x12, 0x0B, 0x0A, 0x01, 0x4E, 0x0A, 0x01,
                    0x43, 0x0A, 0x01, 0x48, 0x0A, 0x01, 0x57, 0x10, 0x01
                };

                using var sessionOptions = new SessionOptions();
                sessionOptions.AppendExecutionProvider("DML");
                using var session = new InferenceSession(minimalModel, sessionOptions);

                Debug.WriteLine("[WinMLHelpers] DML is available and working.");
            });

            return true;
        }
        catch (OnnxRuntimeException ex) when (ex.Message.Contains("No devices detected"))
        {
            Debug.WriteLine("[WinMLHelpers] DirectML execution provider is not available on this system.");
            Debug.WriteLine("[WinMLHelpers] This could be due to:");
            Debug.WriteLine("[WinMLHelpers]   (1) No compatible GPU is present");
            Debug.WriteLine("[WinMLHelpers]   (2) GPU drivers are outdated or not installed");
            Debug.WriteLine("[WinMLHelpers]   (3) DirectX 12 is not supported");
            Debug.WriteLine($"[WinMLHelpers] Original error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WinMLHelpers] DML test failed: {ex.Message}");
            return false;
        }
    }
}