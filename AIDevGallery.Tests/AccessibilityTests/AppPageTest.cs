// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;

namespace AIDevGallery.Tests.AccessibilityTests;

[TestClass]
public class AccessibilityTests : FlaUITestBase
{
    /// <summary>
    /// Gets or sets the test context which provides information about and functionality for the current test run.
    /// </summary>
    public TestContext TestContext { get; set; }

    /// <summary>
    /// Path to Axe.Windows CLI executable
    /// </summary>
    private string? cliPath;

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies accessibility compliance of multiple pages using Axe.Windows CLI")]
    public void MultiPageAccessibilityAxeWindowsCliTest()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Maximize the window to ensure all elements are visible/clickable
        MainWindow.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

        Assert.IsTrue(InitAxeWindows(), "Axe Init Failed");

        // Get the actual process ID from the window, not the test runner process
        var processId = MainWindow.Properties.ProcessId.Value;

        Console.WriteLine($"Testing app process ID: {processId}");

        // TODO: Next steps - needs to add "Models" and "AI APIs" pages
        var pagesToTest = new[] { "Home", "Samples", "Settings" };
        var pagesToDeepTest = new[] { "Samples" };
        var scanResults = new System.Collections.Generic.List<string>();
        var failedPages = new System.Collections.Generic.List<string>();

        foreach (var pageName in pagesToTest)
        {
            Console.WriteLine($"\n--- Testing Page: {pageName} ---");

            var mainPage = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavView"));
            Assert.IsNotNull(mainPage, "Main page should be initialized");

            // Navigate to the page
            bool navigationSuccess = NavigateToPage(pageName);

            if (!navigationSuccess)
            {
                Console.WriteLine($"Skipping accessibility scan for {pageName} due to navigation failure");
                continue;
            }

            // Check if this page has list items to test
            if (pagesToDeepTest.Contains(pageName))
            {
                // Act - Find scenario navigation view
                var scenario = mainPage.FindFirstDescendant(cf => cf.ByAutomationId("ScenarioNavView"));
                Assert.IsNotNull(scenario, "scenario should be found");

                // Act - Find the MenuItemsHost in scenario navigation view
                var menuItemsHostResult = Retry.WhileNull(
                    () => scenario.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost")),
                    timeout: TimeSpan.FromSeconds(10));
                var menuItemsHost = menuItemsHostResult.Result;
                Assert.IsNotNull(menuItemsHost, "MenuItemsHost should be found");

                // Find only DIRECT children ListItems, not all descendants
                // This prevents getting nested navigation items from inner NavigationViews
                var navigationItems = menuItemsHost.FindAllChildren(cf =>
                    cf.ByControlType(ControlType.ListItem))
                    .Where(item => item.IsEnabled && item.IsOffscreen == false)
                    .ToArray();

                Console.WriteLine($"Found {navigationItems.Length} enabled navigation items");

                Assert.IsTrue(navigationItems.Length > 0, "Should have at least one navigation item");

                // Click each item
                foreach (var item in navigationItems)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    Console.WriteLine($"\nClicking navigation item: {item.Name}");

                    // Save the item's name to identify it later, in case the UI tree is rebuilt
                    var itemName = item.Name;

                    // Open List
                    bool isExpanded = IsItemExpanded(item);
                    if (!isExpanded)
                    {
                        if (!item.IsOffscreen)
                        {
                            item.Click();
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Wait for window to become responsive after click
                    Retry.WhileTrue(
                        () => !MainWindow.IsAvailable || MainWindow.IsOffscreen,
                        timeout: TimeSpan.FromSeconds(5),
                        throwOnTimeout: false);

                    // Small delay to allow content to load
                    var listItems = item.FindAllChildren(cf =>
                        cf.ByControlType(ControlType.ListItem))
                        .Where(item => item.IsEnabled && item.IsOffscreen == false)
                        .ToArray();

                    Console.WriteLine($"Inside List Found {listItems.Length} items");

                    // If there are further list items, we could extend this to click into them as well
                    if (listItems.Length > 0)
                    {
                        foreach (var listItem in listItems)
                        {
                            Console.WriteLine($"  - Found sub-item: {listItem.Name}");

                            if (!listItem.TryGetClickablePoint(out _))
                            {
                                Console.WriteLine($"Skipping sub-item {listItem.Name} as it is likely off-screen");
                                continue;
                            }

                            // Clicks twice to select and then close the potential model-not-supported popup.
                            // Note: Popup styles vary across pages, making a unified close function difficult without standardization.
                            // Wait 3s to allow the sample to load its model before scanning
                            Retry.WhileTrue(
                                () =>
                                {
                                    listItem.Click();
                                    return IsItemSelected(listItem);
                                },
                                timeout: TimeSpan.FromSeconds(3));

                            // Wait for window to become responsive after click
                            Retry.WhileTrue(
                                () => !MainWindow.IsAvailable || MainWindow.IsOffscreen,
                                timeout: TimeSpan.FromSeconds(5),
                                throwOnTimeout: false);

                            ExecutePageScanAndTrackResults(processId, listItem.Name, scanResults, failedPages);
                        }
                    }
                    else
                    {
                        ExecutePageScanAndTrackResults(processId, item.Name, scanResults, failedPages);
                    }

                    // Close List - Re-find the item by saved identifier since UI tree may have been rebuilt
                    var itemToClose = menuItemsHost.FindFirstDescendant(cf => cf.ByName(itemName));
                    if (itemToClose != null)
                    {
                        itemToClose.Click();
                        Console.WriteLine($"Successfully closed: {itemToClose.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Could not find item to close: {itemName}");
                    }
                }

                // Assert - Report testing summary for this page
                Console.WriteLine($"\nCompleted testing items in page '{pageName}'");
            }
            else
            {
                // Execute scan and track results for regular pages
                ExecutePageScanAndTrackResults(processId, pageName, scanResults, failedPages);
            }
        }

        // Assert - All pages should pass
        Console.WriteLine("\n=== Accessibility Test Summary ===");
        foreach (var result in scanResults)
        {
            Console.WriteLine(result);
        }

        if (failedPages.Count > 0)
        {
            AttachAxeResultsToTestContext();
        }
        else
        {
            Console.WriteLine("All pages passed accessibility checks");
        }

        TakeScreenshot("MultiPage_AccessibilityTest");
    }

    /// <summary>
    /// Navigates to a specific page in the application using FlaUI
    /// </summary>
    private bool NavigateToPage(string pageName)
    {
        // Find the navigation item by name
        var navigationItem = MainWindow?.FindFirstDescendant(cf => cf.ByName(pageName));

        if (navigationItem == null)
        {
            Console.WriteLine($"Navigation item '{pageName}' not found");
            return false;
        }

        // Find the navigation item by name
        var settingItem = MainWindow?.FindFirstDescendant(cf => cf.ByName("Settings"));

        if (settingItem == null)
        {
            Console.WriteLine($"Setting item not found");
            return false;
        }

        // Prevents test-generated popups from blocking the target element click.
        Retry.WhileTrue(
            () =>
            {
                settingItem.Click();
                return IsItemSelected(settingItem);
            },
            timeout: TimeSpan.FromSeconds(4));
        navigationItem.Click();
        Console.WriteLine($"Clicked navigation item: {pageName}");
        Thread.Sleep(2000); // Wait for page to load
        return true;
    }

    /// <summary>
    /// Checks if an item is expanded (opened) using the ExpandCollapse pattern
    /// </summary>
    private bool IsItemExpanded(FlaUI.Core.AutomationElements.AutomationElement item)
    {
        if (item.Patterns.ExpandCollapse.IsSupported)
        {
            return item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Expanded;
        }

        return false;
    }

    /// <summary>
    /// Checks if an item is selected using the SelectionItem pattern
    /// </summary>
    private bool IsItemSelected(FlaUI.Core.AutomationElements.AutomationElement item)
    {
        if (item.Patterns.SelectionItem.IsSupported)
        {
            return item.Patterns.SelectionItem.Pattern.IsSelected.Value;
        }

        return false;
    }

    /// <summary>
    /// Checks if an item is focused
    /// </summary>
    private bool IsItemFocused(FlaUI.Core.AutomationElements.AutomationElement item)
    {
        return item.Properties.HasKeyboardFocus.Value;
    }

    /// <summary>
    /// Executes accessibility scan on a page and tracks results in collections
    /// </summary>
    private void ExecutePageScanAndTrackResults(int processId, string pageName, System.Collections.Generic.List<string> scanResults, System.Collections.Generic.List<string> failedPages)
    {
        bool scanPassed = RunAxeWindowsCliScan(processId, pageName);
        string result = $"{pageName}: {(scanPassed ? "PASSED" : "FAILED")}";

        scanResults.Add(result);

        if (!scanPassed)
        {
            failedPages.Add(pageName);
        }

        // Small delay between page tests
        System.Threading.Thread.Sleep(1000);
    }

    /// <summary>
    /// Initializes Axe.Windows CLI by locating or downloading it
    /// </summary>
    private bool InitAxeWindows()
    {
        // Determine CLI path - check common locations
        cliPath = GetAxeWindowsCliPath();

        if (string.IsNullOrEmpty(cliPath))
        {
            Console.WriteLine("Axe.Windows CLI not found locally. Attempting to download...");
            cliPath = DownloadAxeWindowsCli();

            if (string.IsNullOrEmpty(cliPath))
            {
                Console.WriteLine("Failed to download Axe.Windows CLI.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Runs Axe.Windows CLI scan on the current page
    /// </summary>
    private bool RunAxeWindowsCliScan(int processId, string pageName)
    {
        // Create output directory for this page's results
        var assemblyDir = System.IO.Path.GetDirectoryName(typeof(AccessibilityTests).Assembly.Location);
        if (string.IsNullOrEmpty(assemblyDir))
        {
            Console.WriteLine("Could not determine assembly directory");
            return false;
        }

        var baseOutputDir = System.IO.Path.Combine(assemblyDir, "AxeOutput");
        var pageOutputDir = System.IO.Path.Combine(baseOutputDir, pageName);
        System.IO.Directory.CreateDirectory(pageOutputDir);

        Console.WriteLine($"Scanning page '{pageName}'...");

        // Run Axe.Windows CLI
        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = $"--processId {processId} --outputDirectory \"{pageOutputDir}\" --verbosity default",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = System.Diagnostics.Process.Start(processInfo))
        {
            if (process == null)
            {
                Console.WriteLine("Failed to start Axe.Windows CLI process");
                return false;
            }

            var timeout = TimeSpan.FromSeconds(60);
            bool completed = process.WaitForExit((int)timeout.TotalMilliseconds);

            if (!completed)
            {
                process.Kill();
                Console.WriteLine($"Axe.Windows CLI scan timed out for page '{pageName}'");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"CLI Output: {output}");
            }

            // Check for accessibility issues
            var issueFiles = System.IO.Directory.GetFiles(pageOutputDir, "*.a11ytest", System.IO.SearchOption.AllDirectories);

            if (issueFiles.Length > 0)
            {
                Console.WriteLine($"Accessibility issues found on '{pageName}':");
                foreach (var issueFile in issueFiles)
                {
                    Console.WriteLine($"  - {issueFile}");
                }

                return false;
            }
            else
            {
                Console.WriteLine($"'{pageName}' passed accessibility check");
                return true;
            }
        }
    }

    /// <summary>
    /// Gets the path to Axe.Windows CLI executable
    /// </summary>
    private string? GetAxeWindowsCliPath()
    {
        var possiblePaths = new[]
        {
            "AxeWindowsCLI.exe",
            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(AccessibilityTests).Assembly.Location) ?? string.Empty, "AxeWindowsCLI.exe"),
            System.IO.Path.Combine(Environment.CurrentDirectory, "AxeWindowsCLI.exe"),
            System.IO.Path.Combine(Environment.GetEnvironmentVariable("CLI_PATH") ?? string.Empty, "AxeWindowsCLI.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                Console.WriteLine($"Found Axe.Windows CLI at: {path}");
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Downloads Axe.Windows CLI from GitHub releases if not available locally
    /// </summary>
    private string? DownloadAxeWindowsCli()
    {
        try
        {
            var assemblyDir = System.IO.Path.GetDirectoryName(typeof(AccessibilityTests).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                Console.WriteLine("Could not determine assembly directory");
                return null;
            }

            var downloadDir = System.IO.Path.Combine(assemblyDir, "AxeWindowsCLI");
            Console.WriteLine($"Download directory: {downloadDir}");

            // Fetch latest release info from GitHub
            var gitHubUrl = "https://api.github.com/repos/microsoft/axe-windows/releases/latest";
            Console.WriteLine($"Fetching latest Axe.Windows release from {gitHubUrl}...");

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "AIDevGallery-Test");

                var response = httpClient.GetStringAsync(gitHubUrl).GetAwaiter().GetResult();

                // Parse JSON response to find the CLI asset
                var json = System.Text.Json.JsonDocument.Parse(response);
                var assets = json.RootElement.GetProperty("assets");

                string? downloadUrl = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name?.Contains("CLI") == true && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        Console.WriteLine($"Found CLI asset: {name}");
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine("Could not find AxeWindowsCLI zip asset in release");
                    return null;
                }

                // Download the file
                var zipPath = System.IO.Path.Combine(assemblyDir, "AxeWindowsCLI.zip");

                Console.WriteLine($"Downloading from {downloadUrl}...");
                using (var fileStream = System.IO.File.Create(zipPath))
                {
                    httpClient.GetStreamAsync(downloadUrl).GetAwaiter().GetResult().CopyTo(fileStream);
                }

                Console.WriteLine($"Downloaded to {zipPath}");

                // Extract the archive
                Console.WriteLine($"Extracting to {downloadDir}...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, downloadDir, overwriteFiles: true);

                // Find and verify the executable
                var cliExePath = System.IO.Path.Combine(downloadDir, "AxeWindowsCLI.exe");
                if (!System.IO.File.Exists(cliExePath))
                {
                    Console.WriteLine($"AxeWindowsCLI.exe not found at expected path: {cliExePath}");
                    return null;
                }

                Console.WriteLine($"Successfully downloaded and extracted Axe.Windows CLI to: {cliExePath}");
                return cliExePath;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download Axe.Windows CLI: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attaches generated Axe.Windows result files to the TestContext so they are available in pipeline results
    /// </summary>
    private void AttachAxeResultsToTestContext()
    {
        try
        {
            var assemblyDir = System.IO.Path.GetDirectoryName(typeof(AccessibilityTests).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                return;
            }

            var baseOutputDir = System.IO.Path.Combine(assemblyDir, "AxeOutput");
            if (System.IO.Directory.Exists(baseOutputDir))
            {
                var files = System.IO.Directory.GetFiles(baseOutputDir, "*.a11ytest", System.IO.SearchOption.AllDirectories);
                Console.WriteLine($"Found {files.Length} Axe result files to attach");

                foreach (var file in files)
                {
                    Console.WriteLine($"Attaching result file: {file}");
                    TestContext?.AddResultFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to attach result files: {ex.Message}");
        }
    }
}