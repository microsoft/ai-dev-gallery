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

    public static int GetBestDeviceId()
    {
        int deviceId = 0;
        nuint maxDedicatedVideoMemory = 0;
        try
        {
            DXGI_CREATE_FACTORY_FLAGS createFlags = 0;
            Windows.Win32.PInvoke.CreateDXGIFactory2(createFlags, typeof(IDXGIFactory2).GUID, out object dxgiFactoryObj).ThrowOnFailure();
            IDXGIFactory2? dxgiFactory = (IDXGIFactory2)dxgiFactoryObj;

            IDXGIAdapter1? selectedAdapter = null;

            var index = 0u;
            do
            {
                var result = dxgiFactory.EnumAdapters1(index, out IDXGIAdapter1? dxgiAdapter1);

                if (result.Failed)
                {
                    if (result != HRESULT.DXGI_ERROR_NOT_FOUND)
                    {
                        result.ThrowOnFailure();
                    }

                    index = 0;
                }
                else
                {
                    DXGI_ADAPTER_DESC1 dxgiAdapterDesc = dxgiAdapter1.GetDesc1();

                    if (selectedAdapter == null || dxgiAdapterDesc.DedicatedVideoMemory > maxDedicatedVideoMemory)
                    {
                        maxDedicatedVideoMemory = dxgiAdapterDesc.DedicatedVideoMemory;
                        selectedAdapter = dxgiAdapter1;
                        deviceId = (int)index;
                    }

                    index++;
                    dxgiAdapter1 = null;
                }
            }
            while (index != 0);
        }
        catch (Exception)
        {
        }

        return deviceId;
    }

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

            nuint maxDedicatedVideoMemory = 0;
            nuint maxTotalVideoMemory = 0;

            try
            {
                DXGI_CREATE_FACTORY_FLAGS createFlags = 0;
                Windows.Win32.PInvoke.CreateDXGIFactory2(createFlags, typeof(IDXGIFactory2).GUID, out object dxgiFactoryObj).ThrowOnFailure();
                IDXGIFactory2? dxgiFactory = (IDXGIFactory2)dxgiFactoryObj;

                IDXGIAdapter1? selectedAdapter = null;

                var index = 0u;
                do
                {
                    var result = dxgiFactory.EnumAdapters1(index, out IDXGIAdapter1? dxgiAdapter1);

                    if (result.Failed)
                    {
                        if (result != HRESULT.DXGI_ERROR_NOT_FOUND)
                        {
                            result.ThrowOnFailure();
                        }

                        index = 0;
                    }
                    else
                    {
                        DXGI_ADAPTER_DESC1 dxgiAdapterDesc = dxgiAdapter1.GetDesc1();

                        if (selectedAdapter == null || dxgiAdapterDesc.DedicatedVideoMemory > maxDedicatedVideoMemory)
                        {
                            maxDedicatedVideoMemory = dxgiAdapterDesc.DedicatedVideoMemory;
                            maxTotalVideoMemory = dxgiAdapterDesc.DedicatedVideoMemory + dxgiAdapterDesc.SharedSystemMemory;
                            selectedAdapter = dxgiAdapter1;
                        }

                        index++;
                        dxgiAdapter1 = null;
                    }
                }
                while (index != 0);
            }
            catch (Exception)
            {
            }

            _cachedVramInfo = (maxDedicatedVideoMemory, maxTotalVideoMemory);
            return _cachedVramInfo.Value;
        }
    }

    public static ulong GetVram() => GetVramInfo().dedicated;

    public static ulong GetTotalVram() => GetVramInfo().total;

    public static bool IsArm64()
    {
        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
    }

    private static System.Collections.Generic.IReadOnlyList<OrtEpDevice>? _cachedEpDevices;
    private static readonly object _epDevicesLock = new();

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

    public static bool HasNPU()
    {
        try
        {
            return GetEpDevices().Any(device =>
                device.HardwareDevice.Type.ToString().Equals("NPU", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static bool HasOpenVINO()
    {
        try
        {
            return GetEpDevices().Any(device =>
                device.EpName.Equals("OpenVINOExecutionProvider", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}