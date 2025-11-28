// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AIDevGallery.Tests.UITests;

[TestClass]
public class PerformanceTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken from application launch until the home page is fully loaded")]
    public void Measure_AppStartup_Time()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(App, "Application should be initialized");
        PerformanceCollector.Clear();

        // Wait for NavigationView menu to render (indicates home page is ready)
        var navViewResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsScrollViewer")),
            timeout: TimeSpan.FromSeconds(20));

        Assert.IsNotNull(navViewResult.Result, "Navigation view not found - Home page might not be loaded");

        var startupDuration = (DateTime.UtcNow - AppLaunchStartTime).TotalMilliseconds;

        Console.WriteLine($"App launch start time: {AppLaunchStartTime:O}");
        Console.WriteLine($"Home page ready time: {DateTime.UtcNow:O}");
        Console.WriteLine($"Total startup duration: {startupDuration:F0} ms");

        PerformanceCollector.Track("AppStartupTime", startupDuration, "ms", new Dictionary<string, string>
        {
            { "Scenario", "ColdStart" }
        }, category: "AppLifecycle");

        Console.WriteLine($"Measuring memory for app process ID: {App.ProcessId}");
        var memoryTracked = PerformanceCollector.TrackMemoryUsage(App.ProcessId, "MemoryUsage_Startup", new Dictionary<string, string>
        {
            { "Scenario", "ColdStart" }
        }, category: "AppLifecycle");

        if (!memoryTracked)
        {
            Console.WriteLine("WARNING: Memory tracking failed for startup test");
        }

        PerformanceCollector.Save();
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken to navigate to the Samples page")]
    public void Measure_PageNavigation_Time()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(App, "Application should be initialized");
        PerformanceCollector.Clear();

        var samplesItem = MainWindow.FindFirstDescendant(cf => cf.ByName("Samples"));
        Assert.IsNotNull(samplesItem, "Samples menu item not found");

        var stopwatch = Stopwatch.StartNew();
        samplesItem.Click();

        // Wait for Filters ComboBox to appear (indicates Samples page is loaded)
        var filtersBoxResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByName("Filters").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox))),
            timeout: TimeSpan.FromSeconds(10));

        stopwatch.Stop();

        Assert.IsNotNull(filtersBoxResult.Result, "Samples page did not load (Filters box not found)");

        Console.WriteLine($"Navigation duration: {stopwatch.ElapsedMilliseconds} ms");

        PerformanceCollector.Track("PageNavigationTime", stopwatch.ElapsedMilliseconds, "ms", new Dictionary<string, string>
        {
            { "From", "Home" },
            { "To", "Samples" }
        }, category: "Navigation");

        var memoryTracked = PerformanceCollector.TrackMemoryUsage(App.ProcessId, "MemoryUsage_Navigation", new Dictionary<string, string>
        {
            { "Page", "Samples" }
        }, category: "Navigation");

        if (!memoryTracked)
        {
            Console.WriteLine("WARNING: Memory tracking failed for navigation test");
        }

        PerformanceCollector.Save();
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken to search for a model")]
    public void Measure_Search_Time()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        PerformanceCollector.Clear();

        var searchBoxGroup = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox"));
        Assert.IsNotNull(searchBoxGroup, "Search box group not found");

        var searchBox = searchBoxGroup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.IsNotNull(searchBox, "Search box text input not found");

        AutomationElement? resultItem;
        using (PerformanceCollector.BeginTiming("SearchTime", new Dictionary<string, string>
        {
            { "Query", "Phi" }
        }, category: "Search"))
        {
            searchBox.AsTextBox().Text = "Phi";

            var retryResult = Retry.WhileNull(
                () =>
            {
                var items = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
                return items.Length > 0 ? items[0] : null;
            }, timeout: TimeSpan.FromSeconds(5));

            resultItem = retryResult.Result;
        }

        Assert.IsNotNull(resultItem, "Search results did not appear");

        PerformanceCollector.Save();
    }
}