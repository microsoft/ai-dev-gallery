// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using static Windows.Win32.PInvoke;

namespace AIDevGallery.Controls;

internal class WndProcHelper
{
    private HWND Handle { get; set; }
    private WNDPROC? newMainWindowWndProc;
    private WNDPROC? oldMainWindowWndProc;

    private WNDPROC? newInputNonClientPointerSourceWndProc;
    private WNDPROC? oldInputNonClientPointerSourceWndProc;

    public WndProcHelper(Window window)
    {
        Handle = new HWND(WindowNative.GetWindowHandle(window));
    }

    public LRESULT CallWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        return Windows.Win32.PInvoke.CallWindowProc(oldMainWindowWndProc, hWnd, Msg, wParam, lParam);
    }

    public LRESULT CallInputNonClientPointerSourceWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        return Windows.Win32.PInvoke.CallWindowProc(oldInputNonClientPointerSourceWndProc, hWnd, Msg, wParam, lParam);
    }

    public void RegisterWndProc(WNDPROC wndProc)
    {
        newMainWindowWndProc = wndProc;
        oldMainWindowWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(SetWindowLongPtr(Handle, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(newMainWindowWndProc)));
    }

    public void RegisterInputNonClientPointerSourceWndProc(WNDPROC wndProc)
    {
        HWND inputNonClientPointerSourceHandle = FindWindowEx(Handle, HWND.Null, "InputNonClientPointerSource", string.Empty);

        if (inputNonClientPointerSourceHandle != IntPtr.Zero)
        {
            int style = GetWindowLong(Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            var hr = SetWindowLong(Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style & ~(int)WINDOW_STYLE.WS_SYSMENU);

            newInputNonClientPointerSourceWndProc = wndProc;
            oldInputNonClientPointerSourceWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(SetWindowLongPtr(inputNonClientPointerSourceHandle, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(newInputNonClientPointerSourceWndProc)));
        }
    }
}