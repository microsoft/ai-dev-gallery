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
}