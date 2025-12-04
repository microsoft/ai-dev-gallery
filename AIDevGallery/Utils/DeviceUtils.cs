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
    private static readonly object _epDevicesLock = new();
    private static System.Collections.Generic.IReadOnlyList<OrtEpDevice>? _cachedEpDevices;

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

    public static ulong GetVram()
    {
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

        return maxDedicatedVideoMemory;
    }

    public static bool IsArm64() =>
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;

    public static bool HasNPU() => HasExecutionProvider(device =>
        device.HardwareDevice.Type.ToString().Equals("NPU", StringComparison.OrdinalIgnoreCase));

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
    /// <returns>A read-only list of available ONNX Runtime Execution Provider devices.</returns>
    public static System.Collections.Generic.IReadOnlyList<OrtEpDevice> GetEpDevices()
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
                catch (Exception ex)
                {
                    // Log but continue
                    Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().LogException("GetEpDevices_RegistrationFailed", ex);
                }

                _cachedEpDevices = OrtEnv.Instance().GetEpDevices();
            }
            catch (Exception ex)
            {
                // Log the failure to get EP devices - this could indicate ONNX Runtime initialization issues
                Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().LogException("GetEpDevices_Failed", ex);
                _cachedEpDevices = System.Array.Empty<OrtEpDevice>();
            }

            return _cachedEpDevices;
        }
    }
}