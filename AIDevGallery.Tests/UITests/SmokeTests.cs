// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// Quick smoke tests to verify FlaUI setup is working.
/// These tests run fast and help diagnose setup issues.
/// </summary>
[TestClass]
public class SmokeTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [Description("Verifies that the application can be found and launched")]
    public void Smoke_ApplicationLaunches()
    {
        // The TestInitialize already launched the app and got the main window
        // This test just verifies that setup worked
        
        Assert.IsNotNull(App, "Application should be launched");
        Assert.IsNotNull(Automation, "Automation should be initialized");
        Assert.IsNotNull(MainWindow, "Main window should be available");
        
        Console.WriteLine("✓ Application launched successfully");
        Console.WriteLine($"✓ Process ID: {App.ProcessId}");
        Console.WriteLine($"✓ Main window title: {MainWindow.Title}");
        
        TakeScreenshot("Smoke_ApplicationLaunched");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [Description("Verifies that the main window has basic properties")]
    public void Smoke_MainWindowHasBasicProperties()
    {
        Assert.IsNotNull(MainWindow, "Main window should exist");
        
        // Check basic window properties
        Assert.IsTrue(MainWindow.IsAvailable, "Window should be available");
        Assert.IsFalse(string.IsNullOrEmpty(MainWindow.Title), "Window should have a title");
        
        var bounds = MainWindow.BoundingRectangle;
        Assert.IsTrue(bounds.Width > 0, "Window should have width");
        Assert.IsTrue(bounds.Height > 0, "Window should have height");
        
        Console.WriteLine("✓ Main window has valid properties");
        Console.WriteLine($"  Title: {MainWindow.Title}");
        Console.WriteLine($"  Size: {bounds.Width}x{bounds.Height}");
        Console.WriteLine($"  Position: ({bounds.X}, {bounds.Y})");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [Description("Verifies that UI elements can be queried")]
    public void Smoke_CanQueryUIElements()
    {
        Assert.IsNotNull(MainWindow, "Main window should exist");
        
        // Try to get all descendants
        var allElements = MainWindow.FindAllDescendants();
        
        Assert.IsNotNull(allElements, "Should be able to query elements");
        Assert.IsTrue(allElements.Length > 0, "Should find at least some UI elements");
        
        Console.WriteLine($"✓ Found {allElements.Length} UI elements");
        
        // Count element types
        var uniqueTypes = new System.Collections.Generic.HashSet<string>();
        foreach (var element in allElements)
        {
            uniqueTypes.Add(element.ControlType.ToString());
        }
        
        Console.WriteLine($"✓ Found {uniqueTypes.Count} different element types");
    }
}
