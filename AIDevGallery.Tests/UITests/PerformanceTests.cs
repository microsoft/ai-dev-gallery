// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UITests;

[TestClass]
public class PerformanceTests : FlaUITestBase
{
    /// <summary>
    /// Cleans the test environment by resetting model configuration and clearing cache.
    /// Uses direct API calls instead of UI automation for more reliable and faster cleanup.
    /// </summary>
    private async Task CleanTestEnvironmentAsync()
    {
        try
        {
            Console.WriteLine("Resetting model configuration...");

            // Clear usage history
            AIDevGallery.App.AppData.UsageHistoryV2?.Clear();

            // Clear user-added model mappings
            AIDevGallery.App.AppData.ModelTypeToUserAddedModelsMapping?.Clear();

            // Clear most recently used items
            AIDevGallery.App.AppData.MostRecentlyUsedItems.Clear();

            // Save AppData changes
            await AIDevGallery.App.AppData.SaveAsync();
            Console.WriteLine("Model configuration reset successfully");

            // Clear cache
            Console.WriteLine("Clearing cache...");
            await AIDevGallery.App.ModelCache.ClearCache();
            Console.WriteLine("Cache cleared successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during test environment cleanup: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Don't fail the test if cleanup has issues, just log it
        }
    }

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
            () =>
            {
                var element = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsScrollViewer"));
                // Ensure element exists, is visible, and not offscreen
                return (element != null && !element.IsOffscreen) ? element : null;
            },
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

        samplesItem.Click();
        var stopwatch = Stopwatch.StartNew();

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

        // Wait for SearchBox to appear (it's in the title bar)
        var searchBoxGroupResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox")),
            timeout: TimeSpan.FromSeconds(10));
        var searchBoxGroup = searchBoxGroupResult.Result;
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

    [TestMethod]
    [TestCategory("Performance")]
    [Description("Measures the time taken from first sample item navigation to model selection button availability")]
    public async Task Measure_ModelSelectionButton_LoadTime()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(App, "Application should be initialized");

        // Clean test environment before measuring performance
        Console.WriteLine("=== Cleaning test environment ===");
        await CleanTestEnvironmentAsync();
        Console.WriteLine("Test environment cleaned successfully\n");

        PerformanceCollector.Clear();

        var menuItemsHost = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
        Assert.IsNotNull(menuItemsHost, "MenuItemsHost should be found");
        var topLevelItems = menuItemsHost.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        var samplesItem = topLevelItems.FirstOrDefault(item => item.Name == "Samples") ?? topLevelItems.ElementAtOrDefault(1);
        Assert.IsNotNull(samplesItem, "Samples item should be found");
        samplesItem.Click();

        // Wait for Text category item to appear after navigation
        var textItemResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).And(cf.ByName("Text"))),
            timeout: TimeSpan.FromSeconds(5));
        var textItem = textItemResult.Result;
        Assert.IsNotNull(textItem, "Text list item should be found");
        textItem.Click();

        // Wait for child items to load under Text category
        Retry.WhileTrue(
            () => textItem.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Length == 0,
            timeout: TimeSpan.FromSeconds(5));

        // Get the first sample item under the Text category
        var textItemChildren = textItem.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        AutomationElement? firstSampleItem = null;

        if (textItemChildren.Length == 0)
        {
            var innerNavView = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavView"))
                ?.FindFirstDescendant(cf => cf.ByAutomationId("NavView"));
            if (innerNavView != null)
            {
                var innerMenuHost = innerNavView.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
                if (innerMenuHost != null)
                {
                    var allListItems = innerMenuHost.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
                    var categoryItems = innerMenuHost.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
                    firstSampleItem = allListItems.FirstOrDefault(item =>
                        item != null &&
                        categoryItems.All(cat => cat.AutomationId != item.AutomationId));
                }
            }
        }
        else
        {
            firstSampleItem = textItemChildren.FirstOrDefault();
        }

        Assert.IsNotNull(firstSampleItem, "First sample item under Text category should be found");
        Console.WriteLine($"Found first sample item: {firstSampleItem.Name}");

        var stopwatch = Stopwatch.StartNew();
        AutomationElement? modelButton = null;
        var buttonDetected = new ManualResetEventSlim(false);
        var eventHandlerLock = 0;

        Action<AutomationElement, FlaUI.Core.Definitions.StructureChangeType, int[]> onStructureChanged = (sender, changeType, runtimeId) =>
        {
            if (modelButton != null || changeType != FlaUI.Core.Definitions.StructureChangeType.ChildAdded)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref eventHandlerLock, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var button = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelBtn"));
                if (button != null && button.IsEnabled && !button.IsOffscreen)
                {
                    stopwatch.Stop();
                    modelButton = button;
                    buttonDetected.Set();
                    Console.WriteLine($"[Event] Button detected at {stopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Event] Exception: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref eventHandlerLock, 0);
            }
        };

        IDisposable? eventHandle = null;
        try
        {
            eventHandle = MainWindow.RegisterStructureChangedEvent(FlaUI.Core.Definitions.TreeScope.Descendants, onStructureChanged);
            if (eventHandle == null)
            {
                Console.WriteLine("[Warning] Event registration failed, using polling fallback");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Event registration error: {ex.Message}");
        }

        try
        {
            firstSampleItem.Click();

            var eventTriggered = buttonDetected.Wait(TimeSpan.FromSeconds(15));

            if (!eventTriggered)
            {
                Console.WriteLine("[Fallback] Event timeout, polling for button");
                for (int i = 0; i < 150; i++)
                {
                    var button = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelBtn"));
                    if (button == null)
                    {
                        button = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                            .FirstOrDefault(btn => btn.Name != null && btn.Name.Contains("Selected models", StringComparison.OrdinalIgnoreCase));
                    }

                    if (button != null && button.IsEnabled && !button.IsOffscreen)
                    {
                        stopwatch.Stop();
                        Console.WriteLine($"[Fallback] Button found at {stopwatch.ElapsedMilliseconds}ms");
                        break;
                    }

                    Thread.Sleep(100);
                }

                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                var fallbackButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelBtn"));
                Assert.IsNotNull(fallbackButton, "Selected models button should appear within timeout");
            }
            else
            {
                Assert.IsNotNull(modelButton, "Event-detected button should not be null");
            }

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Model selection button load time: {elapsedMs} ms");

            PerformanceCollector.Track("ModelSelectionButtonLoadTime", elapsedMs, "ms", new Dictionary<string, string>
            {
                { "From", "FirstSampleItem" },
                { "To", "ModelSelectionButtonReady" },
                { "Category", "Text" }
            }, category: "PageLoad");

            if (elapsedMs > 10000)
            {
                Console.WriteLine($"[Warning] Loading time ({elapsedMs}ms) exceeds 10s threshold");
            }

            var memoryTracked = PerformanceCollector.TrackMemoryUsage(App.ProcessId, "MemoryUsage_ModelSelection", new Dictionary<string, string>
            {
                { "State", "AfterModelButtonLoad" }
            }, category: "PageLoad");

            if (!memoryTracked)
            {
                Console.WriteLine("WARNING: Memory tracking failed for model selection test");
            }

            PerformanceCollector.Save();
        }
        finally
        {
            eventHandle?.Dispose();
            buttonDetected.Dispose();
        }
    }
}