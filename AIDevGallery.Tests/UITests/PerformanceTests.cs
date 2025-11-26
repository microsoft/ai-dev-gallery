// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestApp;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        // We look for a ScrollViewer with AutomationId which indicates content is loaded
        var navViewResult = Retry.WhileNull(() => 
        {
            // Look for the MenuItemsScrollViewer which is part of NavigationView
            // This indicates the navigation menu has been rendered
            return MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsScrollViewer"));
        }, timeout: TimeSpan.FromSeconds(20));

        Assert.IsNotNull(navViewResult.Result, "Navigation view not found - Home page might not be loaded");

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
        var filtersBoxResult = Retry.WhileNull(() => 
        {
            return MainWindow.FindFirstDescendant(cf => cf.ByName("Filters").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)));
        }, timeout: TimeSpan.FromSeconds(10));

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        Assert.IsNotNull(filtersBoxResult.Result, "Samples page did not load (Filters box not found)");

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

        // Find SearchBox - it's inside a Group with AutomationId "SearchBox"
        var searchBoxGroup = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox"));
        Assert.IsNotNull(searchBoxGroup, "Search box group not found");
        
        // The actual text input is inside the group
        var searchBox = searchBoxGroup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.IsNotNull(searchBox, "Search box text input not found");

        // Act
        var startTime = DateTime.UtcNow;
        
        // Type "Phi" into the search box
        searchBox.AsTextBox().Text = "Phi";
        
        // Wait for results - look for any list item in the search results
        var retryResult = Retry.WhileNull(() => 
        {
            var items = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
            return items.Length > 0 ? items[0] : null;
        }, timeout: TimeSpan.FromSeconds(5));

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        var resultItem = retryResult.Result;

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
