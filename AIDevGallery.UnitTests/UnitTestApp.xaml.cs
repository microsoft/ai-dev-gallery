// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;

namespace AIDevGallery.UnitTests;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class UnitTestApp : Application
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestApp"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public UnitTestApp()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();

        window = new UnitTestAppWindow();
        window.Activate();

        UITestMethodAttribute.DispatcherQueue = window.DispatcherQueue;

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(Environment.CommandLine);
    }

    private static UnitTestAppWindow? window;

    public static void SetWindowContent(UIElement content)
    {
        window?.SetRootGridContent(content);
    }
}