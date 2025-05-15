// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.WinMLBootsrap;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery;

/// <summary>
/// Program class
/// </summary>
public class Program
{
    // Replaces the standard App.g.i.cs.
    // Note: We can't declare Main to be async because in a WinUI app
    // this prevents Narrator from reading XAML elements.
    [STAThread]
    private static void Main()
    {
        WinMLGalleryBootstrap.Initialize();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        bool isRedirect = DecideRedirection();

        using OgaHandle ogaHandle = new();

        if (!isRedirect)
        {
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }

        NativeMethods.WinMLUninitialize();
    }

    private static bool DecideRedirection()
    {
        bool isRedirect = false;
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("AIDevGalleryApp");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            RedirectActivationTo(args, keyInstance);
        }

        return isRedirect;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // Do the redirection on another thread, and use a non-blocking
    // wait method to wait for the redirection to complete.
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        var redirectSemaphore = new Semaphore(0, 1);
        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            redirectSemaphore.Release();
        });
        redirectSemaphore.WaitOne();
        redirectSemaphore.Dispose();

        // Bring the window to the foreground
        Process process = Process.GetProcessById((int)keyInstance.ProcessId);

        SetForegroundWindow(process.MainWindowHandle);
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        var activationParam = ActivationHelper.GetActivationParam(args);
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage(activationParam);
        }
    }
}