// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace AIDevGallery.Utils;

internal static class DeviceUtils
{
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

    public static bool IsArm64()
    {
        return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
    }

    public static bool HasOpenVINONPU()
    {
        try
        {
            // Method 1: Check for Intel NPU Software & Drivers installation
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var intelNpuPath = System.IO.Path.Combine(programFiles, "Intel", "Intel(R) NPU Software & Drivers");
            if (System.IO.Directory.Exists(intelNpuPath))
            {
                return true;
            }

            // Method 2: Check Windows Registry for NPU service
            if (CheckIntelNPUInRegistry())
            {
                return true;
            }

            // Method 3: Check for OpenVINO environment variable
            var openvinoPath = Environment.GetEnvironmentVariable("OPENVINO_INSTALL_DIR");
            if (!string.IsNullOrEmpty(openvinoPath) && System.IO.Directory.Exists(openvinoPath))
            {
                return true;
            }

            // Method 4: Check for OpenVINO runtime in common installation paths
            var openvinoPaths = new[]
            {
                System.IO.Path.Combine(programFiles, "Intel", "openvino_2024"),
                System.IO.Path.Combine(programFiles, "Intel", "openvino_2025"),
                System.IO.Path.Combine(programFiles, "Intel", "openvino"),
            };

            foreach (var path in openvinoPaths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckIntelNPUInRegistry()
    {
        try
        {
            // Check for NPU service in registry (the service is named "npu")
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npu");
            if (key != null)
            {
                // Verify it's an NPU driver by checking ImagePath
                var imagePath = key.GetValue("ImagePath") as string;
                if (!string.IsNullOrEmpty(imagePath) && imagePath.Contains("npu", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}