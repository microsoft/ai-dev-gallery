// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using System;
using System.Linq;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace AIDevGallery.Utils;

internal static class DeviceUtils
{
    private static (ulong dedicated, ulong total)? _cachedVramInfo;
    private static readonly object _vramLock = new();
    private static System.Collections.Generic.IReadOnlyList<OrtEpDevice>? _cachedEpDevices;
    private static readonly object _epDevicesLock = new();

    public static int GetBestDeviceId()
    {
        var (deviceId, _, _) = EnumerateAdapters((desc, idx, maxVram) =>
        {
            if (desc.DedicatedVideoMemory > maxVram)
            {
                return ((int)idx, desc.DedicatedVideoMemory, 0UL);
            }
            return (0, maxVram, 0UL);
        });

        return deviceId;
    }

    public static ulong GetVram() => GetVramInfo().dedicated;

    public static ulong GetTotalVram() => GetVramInfo().total;

    private static (ulong dedicated, ulong total) GetVramInfo()
    {
        if (_cachedVramInfo.HasValue)
        {
            return _cachedVramInfo.Value;
        }

        lock (_vramLock)
        {
            if (_cachedVramInfo.HasValue)
            {
                return _cachedVramInfo.Value;
            }

            var (_, maxDedicated, maxTotal) = EnumerateAdapters((desc, _, maxVram) =>
            {
                if (desc.DedicatedVideoMemory > maxVram)
                {
                    var total = desc.DedicatedVideoMemory + desc.SharedSystemMemory;
                    return (0, desc.DedicatedVideoMemory, total);
                }
                return (0, maxVram, 0UL);
            });

            _cachedVramInfo = (maxDedicated, maxTotal);
            return _cachedVramInfo.Value;
        }
    }

    private static (T result, nuint maxVram, ulong total) EnumerateAdapters<T>(Func<DXGI_ADAPTER_DESC1, uint, nuint, (T, nuint, ulong)> selector)
    {
        T result = default!;
        nuint maxDedicatedVideoMemory = 0;
        ulong totalMemory = 0;

        try
        {
            Windows.Win32.PInvoke.CreateDXGIFactory2(0, typeof(IDXGIFactory2).GUID, out object dxgiFactoryObj).ThrowOnFailure();
            IDXGIFactory2? dxgiFactory = (IDXGIFactory2)dxgiFactoryObj;

            var index = 0u;
            while (true)
            {
                var enumResult = dxgiFactory.EnumAdapters1(index, out IDXGIAdapter1? dxgiAdapter1);

                if (enumResult.Failed)
                {
                    if (enumResult != HRESULT.DXGI_ERROR_NOT_FOUND)
                    {
                        enumResult.ThrowOnFailure();
                    }
                    break;
                }

                var desc = dxgiAdapter1.GetDesc1();
                var (newResult, newMaxVram, newTotal) = selector(desc, index, maxDedicatedVideoMemory);

                if (newMaxVram > maxDedicatedVideoMemory)
                {
                    result = newResult;
                    maxDedicatedVideoMemory = newMaxVram;
                    totalMemory = newTotal;
                }

                index++;
            }
        }
        catch (Exception)
        {
        }

        return (result, maxDedicatedVideoMemory, totalMemory);
    }

    public static bool IsArm64() =>
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;

    public static bool HasNPU() => HasExecutionProvider(device =>
        device.HardwareDevice.Type.ToString().Equals("NPU", StringComparison.OrdinalIgnoreCase));

    public static bool HasOpenVINO() => HasExecutionProvider(device =>
        device.EpName.Equals("OpenVINOExecutionProvider", StringComparison.OrdinalIgnoreCase));

    private static bool HasExecutionProvider(Func<OrtEpDevice, bool> predicate)
    {
        try
        {
            return GetEpDevices().Any(predicate);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the list of available ONNX Runtime Execution Provider devices.
    /// This method ensures that certified EPs (like OpenVINO, QNN, DML) are registered before querying,
    /// as OrtEnv.GetEpDevices() only returns already-registered providers.
    /// Results are cached to avoid repeated registration overhead.
    /// </summary>
    private static System.Collections.Generic.IReadOnlyList<OrtEpDevice> GetEpDevices()
    {
        if (_cachedEpDevices != null)
        {
            return _cachedEpDevices;
        }

        lock (_epDevicesLock)
        {
            if (_cachedEpDevices != null)
            {
                return _cachedEpDevices;
            }

            try
            {
                OrtEnv.Instance();
                var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();

                try
                {
                    catalog.EnsureAndRegisterCertifiedAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore registration errors
                }

                _cachedEpDevices = OrtEnv.Instance().GetEpDevices();
            }
            catch
            {
                _cachedEpDevices = System.Array.Empty<OrtEpDevice>();
            }

            return _cachedEpDevices;
        }
    }
}