// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.UnitTests.Helpers;
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
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        PerformanceCollector.Clear();

        // Act
        // Wait for the Home page content to appear
        // We look for the NavigationViewControl which indicates the main shell is loaded
        var navView = Retry.WhileNull(() => 
        {
            return MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavigationViewControl"));
        }, timeout: TimeSpan.FromSeconds(10));

        Assert.IsNotNull(navView, "Navigation view not found - Home page might not be loaded");

        // Calculate total startup time
        // Total Time = (Now - AppLaunchStartTime)
        var endTime = DateTime.UtcNow;
        var startupDuration = endTime - AppLaunchStartTime;

        // Assert
        Console.WriteLine($"App launch start time: {AppLaunchStartTime:O}");
        Console.WriteLine($"Home page ready time: {endTime:O}");
        Console.WriteLine($"Total startup duration: {startupDuration.TotalMilliseconds:F0} ms");

        // Record metrics
        PerformanceCollector.Track("AppStartupTime", startupDuration.TotalMilliseconds, "ms", new Dictionary<string, string>
        {
            { "Scenario", "ColdStart" }
        }, category: "AppLifecycle");

        // Save metrics
        PerformanceCollector.Save();
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken to navigate to the Samples page")]
    public void Measure_PageNavigation_Time()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        PerformanceCollector.Clear();

        // Find the Samples menu item
        var samplesItem = MainWindow.FindFirstDescendant(cf => cf.ByName("Samples"));
        Assert.IsNotNull(samplesItem, "Samples menu item not found");

        // Act
        var startTime = DateTime.UtcNow;
        samplesItem.Click();

        // Wait for the Samples page to load. 
        // We look for the "Filters" ComboBox in the ScenarioSelectionPage
        var filtersBox = Retry.WhileNull(() => 
        {
            return MainWindow.FindFirstDescendant(cf => cf.ByName("Filters").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)));
        }, timeout: TimeSpan.FromSeconds(10));

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        Assert.IsNotNull(filtersBox, "Samples page did not load (Filters box not found)");

        // Assert
        Console.WriteLine($"Navigation start time: {startTime:O}");
        Console.WriteLine($"Samples page ready time: {endTime:O}");
        Console.WriteLine($"Navigation duration: {duration.TotalMilliseconds:F0} ms");

        // Record metrics
        PerformanceCollector.Track("PageNavigationTime", duration.TotalMilliseconds, "ms", new Dictionary<string, string>
        {
            { "From", "Home" },
            { "To", "Samples" }
        }, category: "Navigation");

        // Save metrics
        PerformanceCollector.Save();
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken to search for a model")]
    public void Measure_Search_Time()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        PerformanceCollector.Clear();

        var searchBox = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox"));
        Assert.IsNotNull(searchBox, "Search box not found");

        // Act
        var startTime = DateTime.UtcNow;
        
        // Type "Phi" into the search box
        searchBox.AsTextBox().Text = "Phi";
        
        // Wait for results
        // We look for any list item in the search results
        var resultItem = Retry.WhileNull(() => 
        {
            return MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).And(cf.ByName("Phi-3-mini")));
        }, timeout: TimeSpan.FromSeconds(10));

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        Assert.IsNotNull(resultItem, "Search results did not appear");

        // Assert
        Console.WriteLine($"Search start time: {startTime:O}");
        Console.WriteLine($"Result appeared time: {endTime:O}");
        Console.WriteLine($"Search duration: {duration.TotalMilliseconds:F0} ms");

        // Record metrics
        PerformanceCollector.Track("SearchTime", duration.TotalMilliseconds, "ms", new Dictionary<string, string>
        {
            { "Query", "Phi" }
        }, category: "Search");

        // Save metrics
        PerformanceCollector.Save();
    }
}
