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
        variantDownloadButton.Click();
        Thread.Sleep(3000);

        Console.WriteLine("\n=== Step 8: Monitor Download Performance ===");
        
        // Verify ModelDownloadQueue is available
        Assert.IsNotNull(AIDevGallery.App.ModelDownloadQueue, "ModelDownloadQueue should not be null. App may not be properly initialized.");
        
        // Setup performance monitoring variables
        DateTime? downloadStartTime = null;
        DateTime? downloadEndTime = null;
        bool downloadCompleted = false;
        bool downloadStarted = false;
        float lastProgress = 0f;
        
        // Create event handler to monitor download progress
        EventHandler<AIDevGallery.Utils.ModelDownloadEventArgs>? stateChangedHandler = null;
        stateChangedHandler = (sender, args) =>
        {
            try
            {
                if (args.Status == AIDevGallery.Utils.DownloadStatus.InProgress && !downloadStarted)
                {
                    downloadStartTime = DateTime.UtcNow;
                    downloadStarted = true;
                    lastProgress = args.Progress;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download started - Progress: {args.Progress:P2}");
                }
                else if (args.Status == AIDevGallery.Utils.DownloadStatus.InProgress)
                {
                    // Log progress every 10% or significant change
                    if (args.Progress - lastProgress >= 0.1f || args.Progress >= 0.99f)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download progress: {args.Progress:P2}");
                        lastProgress = args.Progress;
                    }
                }
                else if (args.Status == AIDevGallery.Utils.DownloadStatus.Completed)
                {
                    downloadEndTime = DateTime.UtcNow;
                    downloadCompleted = true;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download completed!");
                }
                else if (args.Status == AIDevGallery.Utils.DownloadStatus.Canceled)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Download was canceled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in StateChanged handler: {ex.Message}");
            }
        };

        // Subscribe to the download queue's events through the App
        AIDevGallery.Utils.ModelDownload? currentDownload = null;
        EventHandler<AIDevGallery.Utils.ModelDownloadCompletedEventArgs>? completedHandler = null;
        completedHandler = (sender, args) =>
        {
            try
            {
                Console.WriteLine("ModelDownloadCompleted event fired");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ModelDownloadCompleted handler: {ex.Message}");
            }
        };

        try
        {
            AIDevGallery.App.ModelDownloadQueue.ModelDownloadCompleted += completedHandler;
            Console.WriteLine("✓ Subscribed to ModelDownloadCompleted event");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to subscribe to ModelDownloadCompleted event: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Click the variant download button to start download
        Console.WriteLine("About to click variant download button...");
        try
        {
            variantDownloadButton.Click();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to click variant download button: {ex.Message}");
            throw;
        }
        Console.WriteLine("✓ Variant download button clicked");
        Thread.Sleep(2000); // Give more time for download to be queued
        
        // Find the ModelDownload object that was just created
        var downloads = AIDevGallery.App.ModelDownloadQueue.GetDownloads();
        Console.WriteLine($"Found {downloads.Count} download(s) in queue");
        
        currentDownload = downloads.LastOrDefault();
        
        if (currentDownload != null)
        {
            Console.WriteLine($"Found active download for: {currentDownload.Details.Name}");
            if (stateChangedHandler != null)
            {
                currentDownload.StateChanged += stateChangedHandler;
                Console.WriteLine("✓ StateChanged event handler attached");
            }
            else
            {
                Console.WriteLine("ERROR: stateChangedHandler is null");
            }
        }
        else
        {
            Console.WriteLine("WARNING: Could not find active download object");
            Console.WriteLine("This may indicate the download was already completed or failed to start");
            
            // Try to wait a bit more and check again
            Thread.Sleep(2000);
            downloads = AIDevGallery.App.ModelDownloadQueue.GetDownloads();
            currentDownload = downloads.LastOrDefault();
            
            if (currentDownload != null)
            {
                Console.WriteLine($"✓ Found download on retry: {currentDownload.Details.Name}");
                if (stateChangedHandler != null)
                {
                    currentDownload.StateChanged += stateChangedHandler;
                }
            }
            else
            {
                Console.WriteLine("ERROR: Still no download found. Test may fail.");
            }
        }

        // Wait for download to complete with timeout (10 minutes for large models)
        var timeout = TimeSpan.FromMinutes(10);
        var waitStartTime = DateTime.UtcNow;
        var lastLogTime = DateTime.UtcNow;
        
        Console.WriteLine($"Waiting for download to complete (timeout: {timeout.TotalMinutes} minutes)...");
        
        try
        {
            while (!downloadCompleted && DateTime.UtcNow - waitStartTime < timeout)
            {
                Thread.Sleep(500);
                
                // Log status every 10 seconds
                if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 10)
                {
                    var elapsed = DateTime.UtcNow - waitStartTime;
                    Console.WriteLine($"Still downloading... Elapsed: {elapsed.TotalSeconds:F1}s");
                    lastLogTime = DateTime.UtcNow;
                }
            }
        }
        finally
        {
            // Cleanup event handlers - must happen even if test fails
            Console.WriteLine("Cleaning up event handlers...");
            
            try
            {
                if (currentDownload != null && stateChangedHandler != null)
                {
                    currentDownload.StateChanged -= stateChangedHandler;
                    Console.WriteLine("✓ Unsubscribed from StateChanged event");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error unsubscribing from StateChanged: {ex.Message}");
            }

            try
            {
                if (completedHandler != null && AIDevGallery.App.ModelDownloadQueue != null)
                {
                    AIDevGallery.App.ModelDownloadQueue.ModelDownloadCompleted -= completedHandler;
                    Console.WriteLine("✓ Unsubscribed from ModelDownloadCompleted event");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error unsubscribing from ModelDownloadCompleted: {ex.Message}");
            }
        }

        // Assert and report results
        Console.WriteLine("\n=== Validating Test Results ===");
        
        if (!downloadStarted)
        {
            Console.WriteLine("ERROR: Download never started");
            if (currentDownload == null)
            {
                Console.WriteLine("  - currentDownload was null");
            }
            else
            {
                Console.WriteLine($"  - currentDownload status: {currentDownload.DownloadStatus}");
                Console.WriteLine($"  - currentDownload progress: {currentDownload.DownloadProgress:P2}");
            }
        }
        
        Assert.IsTrue(downloadStarted, "Download should have started. Check if ModelDownload object was found and event handler was attached.");
        Assert.IsTrue(downloadCompleted, $"Download should have completed within {timeout.TotalMinutes} minutes");
        Assert.IsNotNull(downloadStartTime, "Download start time should be recorded");
        Assert.IsNotNull(downloadEndTime, "Download end time should be recorded");

        TimeSpan downloadDuration = downloadEndTime.Value - downloadStartTime.Value;
        double downloadDurationMs = downloadDuration.TotalMilliseconds;
        
        Console.WriteLine("\n=== Performance Results ===");
        Console.WriteLine($"Model: {modelName}");
        Console.WriteLine($"Variant: {variantName}");
        Console.WriteLine($"Download Start Time: {downloadStartTime.Value:yyyy-MM-dd HH:mm:ss.fff} UTC");
        Console.WriteLine($"Download End Time: {downloadEndTime.Value:yyyy-MM-dd HH:mm:ss.fff} UTC");
        Console.WriteLine($"Download Duration: {downloadDurationMs:F0} ms ({downloadDuration.TotalSeconds:F2} seconds / {downloadDuration.TotalMinutes:F2} minutes)");
        
        // Track performance metrics using PerformanceCollector
        PerformanceCollector.Track("ModelDownloadTime", downloadDurationMs, "ms", new Dictionary<string, string>
        {
            { "ModelName", modelName ?? "Unknown" },
            { "VariantName", variantName ?? "Unknown" },
            { "Source", "FoundryLocal" }
        }, category: "ModelDownload");

        // Track memory usage after download
        var memoryTracked = PerformanceCollector.TrackMemoryUsage(App.ProcessId, "MemoryUsage_AfterDownload", new Dictionary<string, string>
        {
            { "ModelName", modelName ?? "Unknown" },
            { "VariantName", variantName ?? "Unknown" }
        }, category: "ModelDownload");

        if (!memoryTracked)
        {
            Console.WriteLine("WARNING: Memory tracking failed for download test");
        }

        // Check for performance warnings
        if (downloadDuration.TotalMinutes > 5)
        {
            Console.WriteLine($"[Warning] Download time ({downloadDuration.TotalMinutes:F2} minutes) exceeds 5-minute threshold");
        }

        PerformanceCollector.Save();
        Console.WriteLine($"✓ Performance metrics saved successfully");
    }
}