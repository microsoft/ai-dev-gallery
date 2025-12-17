// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

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
    public void SmokeApplicationLaunches()
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
    public void SmokeMainWindowHasBasicProperties()
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
    public void SmokeCanQueryUIElements()
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

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Logs details for elements that expose AutomationId values")]
    public void SampleLogAutomationIds()
    {
        var window = MainWindow ?? throw new InvalidOperationException("Main window should be initialized");

        Console.WriteLine("=== Elements Exposing AutomationId ===");
        var allElements = window.FindAllDescendants();
        var elementsWithId = new List<(FlaUI.Core.AutomationElements.AutomationElement Element, string AutomationId)>();
        var elementsWithoutId = new List<FlaUI.Core.AutomationElements.AutomationElement>();
        var unsupportedCount = 0;

        foreach (var element in allElements)
        {
            if (!element.Properties.AutomationId.IsSupported)
            {
                unsupportedCount++;
                elementsWithoutId.Add(element);
                continue;
            }

            var automationId = element.AutomationId;
            if (string.IsNullOrWhiteSpace(automationId))
            {
                elementsWithoutId.Add(element);
                continue;
            }

            elementsWithId.Add((element, automationId));
        }

        Console.WriteLine($"Total elements scanned: {allElements.Length}");
        Console.WriteLine($"Elements with AutomationId: {elementsWithId.Count}");
        Console.WriteLine($"Elements without AutomationId value: {elementsWithoutId.Count}");
        Console.WriteLine($"Elements lacking AutomationId support: {unsupportedCount}");

        static string DescribeName(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (!element.Properties.Name.IsSupported)
            {
                return "(name property not supported)";
            }

            var rawName = element.Name;
            return string.IsNullOrEmpty(rawName) ? "(no name)" : rawName;
        }

        static string DescribeClass(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (!element.Properties.ClassName.IsSupported)
            {
                return "(class property not supported)";
            }

            var rawClass = element.ClassName;
            return string.IsNullOrEmpty(rawClass) ? "(no class)" : rawClass;
        }

        static string DescribeFramework(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (!element.Properties.FrameworkId.IsSupported)
            {
                return "(framework property not supported)";
            }

            var rawFramework = element.Properties.FrameworkId.ValueOrDefault;
            return string.IsNullOrEmpty(rawFramework) ? "(no framework)" : rawFramework;
        }

        static string DescribeIsEnabled(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (!element.Properties.IsEnabled.IsSupported)
            {
                return "(IsEnabled not supported)";
            }

            return element.IsEnabled ? "true" : "false";
        }

        static string DescribeBounds(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            if (!element.Properties.BoundingRectangle.IsSupported)
            {
                return "(bounds not supported)";
            }

            var bounds = element.BoundingRectangle;
            return $"[{bounds.Left}, {bounds.Top}, {bounds.Width}x{bounds.Height}]";
        }

        foreach (var elementInfo in elementsWithId.Take(50))
        {
            var name = DescribeName(elementInfo.Element);
            var className = DescribeClass(elementInfo.Element);
            var framework = DescribeFramework(elementInfo.Element);
            var isEnabled = DescribeIsEnabled(elementInfo.Element);
            var bounds = DescribeBounds(elementInfo.Element);

            Console.WriteLine($"  - ControlType='{elementInfo.Element.ControlType}', AutomationId='{elementInfo.AutomationId}',Name='{name}', Class='{className}', Framework='{framework}', Bounds={bounds}");
        }

        if (elementsWithId.Count > 50)
        {
            Console.WriteLine($"  ... and {elementsWithId.Count - 50} more with AutomationId");
        }

        Console.WriteLine();
        Console.WriteLine("================= Elements Without AutomationId ===");
        foreach (var element in elementsWithoutId.Take(50))
        {
            var name = DescribeName(element);
            var className = DescribeClass(element);
            var framework = DescribeFramework(element);
            var isEnabled = DescribeIsEnabled(element);
            var bounds = DescribeBounds(element);

            Console.WriteLine($"  - ControlType='{element.ControlType}', Name='{name}', Class='{className}', Framework='{framework}', Bounds={bounds}");
        }

        if (elementsWithoutId.Count > 50)
        {
            Console.WriteLine($"  ... and {elementsWithoutId.Count - 50} more without AutomationId");
        }

        // This sample is intended only for logging AutomationId information
    }
}