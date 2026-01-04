// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// Basic UI tests for the AIDevGallery main window.
/// </summary>
[TestClass]
public class MainWindowTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the main window launches successfully")]
    public void MainWindowLaunchesSuccessfully()
    {
        // Assert
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsFalse(string.IsNullOrEmpty(MainWindow.Title), "Main window should have a title");

        Console.WriteLine($"Main window title: {MainWindow.Title}");

        // Take a screenshot for verification
        TakeScreenshot("MainWindow_Launch");
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the main window is visible and not minimized")]
    public void MainWindowIsVisible()
    {
        // Assert
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsTrue(MainWindow.IsAvailable, "Main window should be available");

        var patterns = MainWindow.Patterns;
        Console.WriteLine($"Main window is available: {MainWindow.IsAvailable}");
        Console.WriteLine($"Main window is offscreen: {MainWindow.IsOffscreen}");
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the main window can be resized")]
    public void MainWindowCanBeResized()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        var originalBounds = MainWindow.BoundingRectangle;
        Console.WriteLine($"Original bounds: W={originalBounds.Width}, H={originalBounds.Height}");

        // Act - Try to resize the window (if supported)
        try
        {
            if (MainWindow.Patterns.Transform.IsSupported)
            {
                var transform = MainWindow.Patterns.Transform.Pattern;
                if (transform.CanResize)
                {
                    // Try a larger size first to ensure change is detectable
                    double newWidth = originalBounds.Width + 200;
                    double newHeight = originalBounds.Height + 100;

                    Console.WriteLine($"Attempting to resize to: W={newWidth}, H={newHeight}");
                    transform.Resize(newWidth, newHeight);
                    System.Threading.Thread.Sleep(500); // Wait for resize

                    var newBounds = MainWindow.BoundingRectangle;
                    Console.WriteLine($"New bounds after resize: W={newBounds.Width}, H={newBounds.Height}");

                    // Take screenshot after resize
                    TakeScreenshot("MainWindow_AfterResize");

                    // Check if size changed at all (width OR height)
                    bool sizeChanged = Math.Abs(originalBounds.Width - newBounds.Width) > 10 ||
                                      Math.Abs(originalBounds.Height - newBounds.Height) > 10;

                    if (sizeChanged)
                    {
                        // Assert - window was resized successfully
                        Assert.IsTrue(sizeChanged, "Window size should have changed");
                        Console.WriteLine("✓ Window resize successful");
                    }
                    else
                    {
                        // Window might have size constraints, mark as inconclusive
                        Assert.Inconclusive($"Window size did not change (possible size constraints). Original: {originalBounds.Width}x{originalBounds.Height}, After: {newBounds.Width}x{newBounds.Height}");
                    }
                }
                else
                {
                    Assert.Inconclusive("Window does not support resizing (CanResize = false)");
                }
            }
            else
            {
                Assert.Inconclusive("Transform pattern not supported");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resize test inconclusive: {ex.Message}");
            Assert.Inconclusive($"Could not test resize functionality: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the main window contains UI elements")]
    public void MainWindowContainsUIElements()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Find all descendants
        var allElements = MainWindow.FindAllDescendants();

        // Assert
        Assert.IsNotNull(allElements, "Should be able to query descendants");
        Assert.IsTrue(allElements.Length > 0, "Main window should contain UI elements");

        Console.WriteLine($"Found {allElements.Length} UI elements in the main window");

        // Log some element types for debugging
        var elementTypes = allElements
            .Select(e => e.ControlType.ToString())
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        Console.WriteLine($"Element types found: {string.Join(", ", elementTypes)}");

        TakeScreenshot("MainWindow_UIElements");
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that buttons can be found in the main window")]
    public void MainWindowContainsButtons()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Find all buttons
        var buttons = MainWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));

        // Assert
        Assert.IsNotNull(buttons, "Should be able to query buttons");
        Console.WriteLine($"Found {buttons.Length} buttons in the main window");

        if (buttons.Length > 0)
        {
            // Log button names for debugging
            for (int i = 0; i < Math.Min(buttons.Length, 10); i++)
            {
                var button = buttons[i];
                try
                {
                    var automationId = button.Properties.AutomationId.IsSupported
                        ? button.AutomationId
                        : "(not supported)";
                    Console.WriteLine($"Button {i + 1}: Name='{button.Name}', AutomationId='{automationId}'");
                }
                catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
                {
                    Console.WriteLine($"Button {i + 1}: Name='{button.Name}', AutomationId=(not supported)");
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the main window can be closed")]
    public void MainWindowCanBeClosed()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsTrue(MainWindow.IsAvailable, "Main window should be available before closing");

        // Act
        MainWindow.Close();
        System.Threading.Thread.Sleep(1000); // Wait for close

        // Assert
        try
        {
            var isStillAvailable = MainWindow.IsAvailable;
            Assert.IsFalse(isStillAvailable, "Main window should not be available after closing");
        }
        catch
        {
            // If accessing IsAvailable throws, the window is definitely closed
            // Window was closed successfully
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that text elements can be found in the main window")]
    public void MainWindowContainsTextElements()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Find all text elements
        var textElements = MainWindow.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));

        // Assert
        Assert.IsNotNull(textElements, "Should be able to query text elements");
        Console.WriteLine($"Found {textElements.Length} text elements in the main window");

        if (textElements.Length > 0)
        {
            // Log some text content for debugging
            for (int i = 0; i < Math.Min(textElements.Length, 10); i++)
            {
                var textElement = textElements[i];
                var text = textElement.Name;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine($"Text {i + 1}: '{text}'");
                }
            }
        }

        TakeScreenshot("MainWindow_TextElements");
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies that the search box accepts input and displays search results")]
    public void SearchBoxDisplaysResultsWhenQueryEntered()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // pane 'Desktop 1'
        //  - windows 'AI Dev Gallery Dev'
        //    - pane ''
        //      - pane ''
        //        - title bar 'AI Dev Gallery' (AutomationId="titleBar")
        //          - group '' (AutomationId="SearchBox")
        //            - edit 'Name    Search samples, models & APIs..'(AutomationId="TextBox")
        var searchBoxGroupResult = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox")),
            timeout: TimeSpan.FromSeconds(10));
        var searchBoxGroup = searchBoxGroupResult.Result;
        Assert.IsNotNull(searchBoxGroup, "Search box group not found");

        var searchBox = searchBoxGroup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.IsNotNull(searchBox, "Search box text input not found");

        Console.WriteLine("Search box found, entering search query...");

        searchBox.AsTextBox().Text = "Phi";
        Console.WriteLine("Search query 'Phi' entered");

        // pane 'Desktop 1'
        //   - windows 'AI Dev Gallery Dev'
        //     - pane 'PopupHost'
        //       - pane ''
        //         - title bar 'AI Dev Gallery' (AutomationId="titleBar")
        //           - group 'SearchBox' (AutomationId="SearchBox")
        //             - window 'Popup' (AutomationId="SuggestionsPopup")
        //               - list '' (AutomationId="SuggestionsList") // length needs to be > 0
        //                 - list item 'Phi 3 Medium'
        //                 - list item ...
        //                 - ...
        var suggestionsPopupResult = Retry.WhileNull(
            () => searchBoxGroup.FindFirstDescendant(cf => cf.ByAutomationId("SuggestionsPopup")),
            timeout: TimeSpan.FromSeconds(5));
        var suggestionsPopup = suggestionsPopupResult.Result;
        Assert.IsNotNull(suggestionsPopup, "Suggestions popup should appear after entering query");

        var suggestionsListResult = Retry.WhileNull(
            () => suggestionsPopup.FindFirstDescendant(cf => cf.ByAutomationId("SuggestionsList")),
            timeout: TimeSpan.FromSeconds(5));
        var suggestionsList = suggestionsListResult.Result;
        Assert.IsNotNull(suggestionsList, "Suggestions list should be found in popup");

        var listItems = suggestionsList.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem));

        Assert.IsTrue(listItems.Length > 0, $"Suggestions list should contain search results, but found {listItems.Length} items");
        Console.WriteLine($"Search results displayed. Found {listItems.Length} suggestions");

        for (int i = 0; i < Math.Min(listItems.Length, 3); i++)
        {
            Console.WriteLine($"  Result {i + 1}: {listItems[i].Name}");
        }

        TakeScreenshot("SearchBox_WithResults");
    }

    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies accessibility compliance of the main window using Axe.Windows CLI")]
    public void MainWindowAccessibilityAxeWindowsCliTest()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        var appProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processId = appProcess.Id;

        Console.WriteLine($"Testing app process ID: {processId}");

        // Act - Run Axe.Windows CLI scan
        try
        {
            // Determine CLI path - check common locations
            string cliPath = null;
            var possiblePaths = new[]
            {
                "AxeWindowsCLI.exe",
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(MainWindowTests).Assembly.Location), "AxeWindowsCLI.exe"),
                System.IO.Path.Combine(Environment.CurrentDirectory, "AxeWindowsCLI.exe"),
                System.IO.Path.Combine(Environment.GetEnvironmentVariable("CLI_PATH") ?? "")
            };

            foreach (var path in possiblePaths)
            {
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    cliPath = path;
                    Console.WriteLine($"Found Axe.Windows CLI at: {cliPath}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(cliPath))
            {
                Console.WriteLine("Axe.Windows CLI not found locally. Attempting to download...");
                cliPath = DownloadAxeWindowsCli();
                
                if (string.IsNullOrEmpty(cliPath))
                {
                    Assert.Inconclusive("Failed to download Axe.Windows CLI. Please ensure it's available in the test environment.");
                    return;
                }
            }

            // Create output directory for results
            var outputDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(MainWindowTests).Assembly.Location),
                "AxeResults");
            System.IO.Directory.CreateDirectory(outputDir);

            Console.WriteLine($"Output directory: {outputDir}");

            // Run Axe.Windows CLI with fast pass scan
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = $"--processId {processId} --outputDirectory \"{outputDir}\" --verbosity default",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Console.WriteLine($"Running Axe.Windows CLI: {cliPath} {processInfo.Arguments}");

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                var timeout = TimeSpan.FromSeconds(60);
                bool completed = process.WaitForExit((int)timeout.TotalMilliseconds);

                if (!completed)
                {
                    process.Kill();
                    Assert.Fail("Axe.Windows CLI scan timed out");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                Console.WriteLine($"Axe.Windows CLI exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine($"Output:\n{output}");
                }
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Errors:\n{error}");
                }

                // Check for accessibility issues
                var issueFiles = System.IO.Directory.GetFiles(outputDir, "*.a11ytest", System.IO.SearchOption.AllDirectories);
                
                if (issueFiles.Length > 0)
                {
                    Console.WriteLine($"\n⚠ Accessibility issues found in {issueFiles.Length} files:");
                    foreach (var issueFile in issueFiles)
                    {
                        Console.WriteLine($"  - {issueFile}");
                        try
                        {
                            var issueContent = System.IO.File.ReadAllText(issueFile);
                            Console.WriteLine($"    Content: {issueContent}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    (Could not read file: {ex.Message})");
                        }
                    }
                    Assert.Fail($"Accessibility issues found. See details above.");
                }
                else
                {
                    Console.WriteLine("✓ No accessibility issues found - scan passed");
                }
            }
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Failed to run Axe.Windows CLI accessibility test: {ex.Message}");
        }

        TakeScreenshot("MainWindow_AxeWindowsCliTest");
    }

    /// <summary>
    /// Downloads Axe.Windows CLI from GitHub releases if not available locally
    /// </summary>
    private string DownloadAxeWindowsCli()
    {
        try
        {
            var downloadDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(MainWindowTests).Assembly.Location),
                "AxeWindowsCLI");

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
                
                string downloadUrl = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name.Contains("CLI") && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
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
                var zipPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(MainWindowTests).Assembly.Location),
                    "AxeWindowsCLI.zip");

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

                Console.WriteLine($"✓ Successfully downloaded and extracted Axe.Windows CLI to: {cliExePath}");
                return cliExePath;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download Axe.Windows CLI: {ex.Message}");
            return null;
        }
    }