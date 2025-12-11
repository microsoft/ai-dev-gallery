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
    [Description("Measures the time taken from first sample item navigation to model selection button availability")]
    public async Task Measure_ModelSelectionButton_LoadTime()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(App, "Application should be initialized");
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

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Model")]
    [Description(@"Measures the time taken to download a model from Foundry Local catalog.
    
    Background: Download performance is critical for user experience. This test measures the actual download time
    from when the download starts to when it completes, providing metrics for performance tracking and regression detection.
    
    Method: Navigates through the UI to select a Foundry Local model, then monitors the ModelDownload StateChanged event 
    to precisely measure download duration. Captures start time when status changes to InProgress and end time when status 
    changes to Completed. Uses PerformanceCollector to track metrics in a consistent format.
    
    Goal: Track model download performance over time, detect performance regressions, and validate download optimization efforts.
    Reports download time in milliseconds and can be integrated with performance dashboards.")]
    public async Task Measure_FoundryLocalModel_DownloadTime()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(App, "Application should be initialized");

        PerformanceCollector.Clear();

        Console.WriteLine("=== Step 1: Navigate to Samples ===");
        var menuItemsHost = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
        Assert.IsNotNull(menuItemsHost, "MenuItemsHost should be found");
        var topLevelItems = menuItemsHost.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));
        var samplesItem = topLevelItems.FirstOrDefault(item => item.Name == "Samples") ?? topLevelItems.ElementAtOrDefault(1);
        Assert.IsNotNull(samplesItem, "Samples item should be found");
        samplesItem.Click();
        Thread.Sleep(2000);

        Console.WriteLine("=== Step 2: Navigate to Text > The First Sample Item ===");
        var textItemResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).And(cf.ByName("Text"))),
            timeout: TimeSpan.FromSeconds(5));
        var textItem = textItemResult.Result;
        Assert.IsNotNull(textItem, "Text list item should be found");
        
        try
        {
            textItem.Click();
            Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: {ex.Message}");
        }

        // Wait for child items to load under Text category
        Retry.WhileTrue(
            () => textItem.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Length == 0,
            timeout: TimeSpan.FromSeconds(5));

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

        Console.WriteLine("=== Step 3: Open Model Selection ===");
        firstSampleItem.Click();
        Thread.Sleep(5000);

        var modelTypeSelector = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("modelTypeSelector"));

        if (modelTypeSelector == null)
        {
            var modelButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelBtn"));
            if (modelButton == null)
            {
                var buttons = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                modelButton = buttons.FirstOrDefault(btn => btn.Name != null && btn.Name.Contains("Selected models", StringComparison.OrdinalIgnoreCase));
            }

            Assert.IsNotNull(modelButton, "Selected models button should be found");
            modelButton.Click();
            Thread.Sleep(1000);
            modelTypeSelector = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("modelTypeSelector"));
        }

        Console.WriteLine("=== Step 4: Select Foundry Local ===");
        Assert.IsNotNull(modelTypeSelector, "Model type selector should be found");
        var foundryLocalOption = modelTypeSelector.FindFirstDescendant(cf => 
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).And(cf.ByName("Foundry Local")));
        Assert.IsNotNull(foundryLocalOption, "Foundry Local item should be found");

        try
        {
            if (foundryLocalOption.Patterns.SelectionItem.IsSupported)
            {
                foundryLocalOption.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                foundryLocalOption.Click();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Selection fallback: {ex.Message}");
            foundryLocalOption.Click();
        }

        Thread.Sleep(1000);

        Console.WriteLine("=== Step 5: Find Available Model ===");
        AutomationElement? downloadableHeader = null;
        for (int i = 0; i < 10; i++)
        {
            downloadableHeader = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DownloadableModelsTxt"));
            if (downloadableHeader != null)
            {
                Console.WriteLine("✓ Models section loaded");
                break;
            }

            Console.WriteLine($"Waiting ({i + 1}/10)...");
            Thread.Sleep(500);
        }

        Assert.IsNotNull(downloadableHeader, "DownloadableModelsTxt should be found");

        var modelsContainer = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelsView"));
        Assert.IsNotNull(modelsContainer, "ModelsView should be found");

        var modelsViewChildren = modelsContainer.FindAllChildren();
        AutomationElement? downloadButton = null;
        string? modelName = null;

        bool pastDownloadableHeader = false;
        foreach (var child in modelsViewChildren)
        {
            string? automationId = null;
            try
            {
                if (child.Properties.AutomationId.IsSupported)
                {
                    automationId = child.AutomationId;
                }
            }
            catch
            {
            }

            if (automationId == "DownloadableModelsTxt")
            {
                pastDownloadableHeader = true;
                continue;
            }

            if (pastDownloadableHeader && child.ControlType == FlaUI.Core.Definitions.ControlType.Group)
            {
                var buttons = child.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)).ToArray();
                var texts = child.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)).ToArray();

                string? targetModelName = null;
                foreach (var text in texts)
                {
                    try
                    {
                        if (text.Properties.Name.IsSupported)
                        {
                            var textName = text.Name;
                            if (!string.IsNullOrEmpty(textName) && textName != "More info")
                            {
                                targetModelName = textName;
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (targetModelName != null)
                {
                    foreach (var button in buttons)
                    {
                        string? buttonName = null;
                        try
                        {
                            if (button.Properties.Name.IsSupported)
                            {
                                buttonName = button.Name;
                            }
                        }
                        catch
                        {
                        }

                        if (buttonName == targetModelName && buttonName != "More info")
                        {
                            downloadButton = button;
                            modelName = targetModelName;
                            Console.WriteLine($"✓ Found downloadable: {modelName}");
                            break;
                        }
                    }
                }

                if (downloadButton != null)
                {
                    break;
                }
            }
        }

        Assert.IsNotNull(downloadButton, "First available model button should be found");

        Console.WriteLine($"=== Step 6: Click Model Button ({modelName}) ===");
        downloadButton.Click();
        Thread.Sleep(2000);

        Console.WriteLine("=== Step 7: Select Variant to Download ===");
        
        // Wait for popup to appear with retry
        var variantPopupResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window).And(cf.ByName("Popup"))),
            timeout: TimeSpan.FromSeconds(5));
        
        var variantPopup = variantPopupResult.Result;
        Assert.IsNotNull(variantPopup, "Popup window should be found within timeout");
        Console.WriteLine("✓ Variant popup found");

        var variantDownloadButton = variantPopup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
        Assert.IsNotNull(variantDownloadButton, "Variant button in Popup should be found");

        string? variantName = null;
        try
        {
            if (variantDownloadButton.Properties.Name.IsSupported)
            {
                variantName = variantDownloadButton.Name;
            }
        }
        catch
        {
        }

        Console.WriteLine($"✓ Selecting variant: {variantName}");
        
        var downloadStopwatch = Stopwatch.StartNew();
        variantDownloadButton.Click();
        Thread.Sleep(1000);

        Console.WriteLine("\n=== Step 8: Monitor Download Performance ===");
//pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - window 'Popup'
//          - pane '' (AutomationId="DownloadFlyout")
//            - group 'openai-whisper-tiny-generic-cpu'
//              - text '4.6GB - Downloaded' (AutomationId="DownloadStatus") // 这里在下中进行中是“Downloading”，下载成功后会变成”Downloaded“，下载失败时会是其他文字。
        // Wait for DownloadFlyout to appear
        var downloadFlyoutResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DownloadFlyout")),
            timeout: TimeSpan.FromSeconds(10));
        
        var downloadFlyout = downloadFlyoutResult.Result;
        Assert.IsNotNull(downloadFlyout, "DownloadFlyout should appear after clicking variant button");
        Console.WriteLine("✓ DownloadFlyout detected");

        // Monitor download status with optimized polling
        bool downloadCompleted = false;
        string? lastStatus = null;
        const int maxWaitMinutes = 5; // Maximum wait time for download
        const int pollIntervalMs = 500; // Check every half second
        const int maxIterations = maxWaitMinutes * 60 * 1000 / pollIntervalMs; // Total iterations

        for (int iteration = 0; iteration < maxIterations && !downloadCompleted; iteration++)
        {
            try
            {
                var downloadStatusElement = downloadFlyout.FindFirstDescendant(cf => 
                    cf.ByAutomationId("DownloadStatus"));

                if (downloadStatusElement != null && downloadStatusElement.Properties.Name.IsSupported)
                {
                    var textContent = downloadStatusElement.Name;
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        // Check if download completed
                        if (textContent.Contains("Downloaded", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadStopwatch.Stop();
                            downloadCompleted = true;
                            lastStatus = textContent;
                            Console.WriteLine($"✓ Download completed: {textContent}");
                            Console.WriteLine($"Download duration: {downloadStopwatch.ElapsedMilliseconds} ms ({downloadStopwatch.Elapsed.TotalSeconds:F1}s)");
                            break;
                        }
                        // Track downloading status
                        else if (textContent.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
                        {
                            if (lastStatus != textContent) {
                                lastStatus = textContent;
                                Console.WriteLine($"Download in progress: {textContent} (elapsed: {downloadStopwatch.Elapsed.TotalSeconds:F1}s)");
                            }
                        } else {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Error checking download status: {ex.Message}");
            }

            if (!downloadCompleted)
            {
                Thread.Sleep(pollIntervalMs);
            }
        }

        if (!downloadStopwatch.IsRunning)
        {
            downloadStopwatch.Stop();
        }

        Assert.IsTrue(downloadCompleted, 
            $"Download should complete within {maxWaitMinutes} minutes. Last status: {lastStatus ?? "Unknown"}");

        // Track performance metrics
        var downloadTimeMs = downloadStopwatch.ElapsedMilliseconds;
        Console.WriteLine($"\n=== Download Performance Summary ===");
        Console.WriteLine($"Model: {modelName}");
        Console.WriteLine($"Variant: {variantName}");
        Console.WriteLine($"Total download time: {downloadTimeMs:F0} ms ({downloadStopwatch.Elapsed.TotalSeconds:F1}s)");

        PerformanceCollector.Track("ModelDownloadTime", downloadTimeMs, "ms", new Dictionary<string, string>
        {
            { "Model", modelName ?? "Unknown" },
            { "Variant", variantName ?? "Unknown" },
            { "Source", "FoundryLocal" }
        }, category: "ModelDownload");

        var memoryTracked = PerformanceCollector.TrackMemoryUsage(App.ProcessId, "MemoryUsage_AfterDownload", new Dictionary<string, string>
        {
            { "Model", modelName ?? "Unknown" },
            { "State", "DownloadCompleted" }
        }, category: "ModelDownload");

        if (!memoryTracked)
        {
            Console.WriteLine("WARNING: Memory tracking failed for download test");
        }

        PerformanceCollector.Save();
        Console.WriteLine("✓ Performance data saved");
    }
}