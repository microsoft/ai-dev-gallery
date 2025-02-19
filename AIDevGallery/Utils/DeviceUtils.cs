// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DXCore;
using Windows.Win32.Graphics.Dxgi;

namespace AIDevGallery.Utils;

internal static class DeviceUtils
{
    private static readonly Guid DXCORE_ADAPTER_ATTRIBUTE_D3D12_GENERIC_ML = new(0xb71b0d41, 0x1088, 0x422f, 0xa2, 0x7c, 0x2, 0x50, 0xb7, 0xd3, 0xa9, 0x88);
    private static bool? _hasNpu;

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

    public static bool HasNpu()
    {
        if (_hasNpu.HasValue)
        {
            return _hasNpu.Value;
        }

        IDXCoreAdapterFactory adapterFactory;
        if (PInvoke.DXCoreCreateAdapterFactory(typeof(IDXCoreAdapterFactory).GUID, out var adapterFactoryObj) != HRESULT.S_OK)
        {
            throw new InvalidOperationException("Failed to create adapter factory");
        }

        adapterFactory = (IDXCoreAdapterFactory)adapterFactoryObj;

        // First try getting all GENERIC_ML devices, which is the broadest set of adapters
        // and includes both GPUs and NPUs; however, running this sample on an older build of
        // Windows may not have drivers that report GENERIC_ML.
        IDXCoreAdapterList adapterList;

        adapterFactory.CreateAdapterList([DXCORE_ADAPTER_ATTRIBUTE_D3D12_GENERIC_ML], typeof(IDXCoreAdapterList).GUID, out var adapterListObj);
        adapterList = (IDXCoreAdapterList)adapterListObj;

        // Fall back to CORE_COMPUTE if GENERIC_ML devices are not available. This is a more restricted
        // set of adapters and may filter out some NPUs.
        if (adapterList.GetAdapterCount() == 0)
        {
            adapterFactory.CreateAdapterList(
                [PInvoke.DXCORE_ADAPTER_ATTRIBUTE_D3D12_CORE_COMPUTE],
                typeof(IDXCoreAdapterList).GUID,
                out adapterListObj);
            adapterList = (IDXCoreAdapterList)adapterListObj;
        }

        if (adapterList.GetAdapterCount() == 0)
        {
            throw new InvalidOperationException("No compatible adapters found.");
        }

        // Sort the adapters by preference, with hardware and high-performance adapters first.
        ReadOnlySpan<DXCoreAdapterPreference> preferences =
        [
            DXCoreAdapterPreference.Hardware,
                DXCoreAdapterPreference.HighPerformance
        ];

        adapterList.Sort(preferences);

        List<IDXCoreAdapter> adapters = [];

        for (uint i = 0; i < adapterList.GetAdapterCount(); i++)
        {
            IDXCoreAdapter adapter;
            adapterList.GetAdapter(i, typeof(IDXCoreAdapter).GUID, out var adapterObj);
            adapter = (IDXCoreAdapter)adapterObj;

            adapter.GetPropertySize(
                DXCoreAdapterProperty.DriverDescription,
                out var descriptionSize);

            string adapterDescription;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal((int)descriptionSize);
                unsafe
                {
                    adapter.GetProperty(
                        DXCoreAdapterProperty.DriverDescription,
                        descriptionSize,
                        buffer.ToPointer());
                }

                adapterDescription = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            // Remove trailing null terminator written by DXCore.
            while (!string.IsNullOrEmpty(adapterDescription) && adapterDescription[^1] == '\0')
            {
                adapterDescription = adapterDescription[..^1];
            }

            adapters.Add(adapter);
            if (adapterDescription.Contains("NPU"))
            {
                _hasNpu = true;
                return true;
            }
        }

        _hasNpu = false;
        return false;
    }
}