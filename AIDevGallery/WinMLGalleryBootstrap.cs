// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace AIDevGallery.WinMLBootsrap;
internal class WinMLGalleryBootstrap
{
    internal static void Initialize()
    {
        int hr = NativeMethods.WinMLDeployMainPackage();
        hr = NativeMethods.WinMLInitialize();
        if (FAILED(hr))
        {
#if WINML_CONTINUE_ON_INIT_FAILURE
            // Continue even if initialization fails
#else
            // Exit with the error code if initialization fails
            Environment.Exit(hr);
#endif
        }
    }

    private static bool FAILED(int hr) => hr < 0;
}

internal static class NativeMethods
{
    [DllImport("WinMLBootstrap.dll")]
    internal static extern int WinMLInitialize();

    [DllImport("WinMLBootstrap.dll")]
    internal static extern void WinMLUninitialize();

    [DllImport("WinMLBootstrap.dll")]
    internal static extern int WinMLGetInitializationStatus();

    [DllImport("WinMLBootstrap.dll")]
    internal static extern int WinMLDeployMainPackage();
}