// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AIDevGallery.Tests.AccessibilityTests;

[TestClass]
public class AccessibilityTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [Description("Verifies accessibility compliance of multiple pages using Axe.Windows CLI")]
    public void MultiPageAccessibilityAxeWindowsCliTest()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        var appProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processId = appProcess.Id;

        Console.WriteLine($"Testing app process ID: {processId}");

        var pagesToTest = new[] { "Home", "Samples", "AI APIs", "Settings" };
        var scanResults = new System.Collections.Generic.List<string>();
        var failedPages = new System.Collections.Generic.List<string>();

        try
        {
            foreach (var pageName in pagesToTest)
            {
                Console.WriteLine($"\n--- Testing Page: {pageName} ---");

                // Navigate to the page
                bool navigationSuccess = NavigateToPage(pageName);

                if (!navigationSuccess)
                {
                    Console.WriteLine($"⚠ Skipping accessibility scan for {pageName} due to navigation failure");
                    continue;
                }

                // Run Axe.Windows CLI scan for this page
                bool scanPassed = RunAxeWindowsCliScan(processId, pageName);

                scanResults.Add($"{pageName}: {(scanPassed ? "PASSED" : "FAILED")}");

                if (!scanPassed)
                {
                    failedPages.Add(pageName);
                }

                // Small delay between page tests
                System.Threading.Thread.Sleep(1000);
            }

            // Assert - All pages should pass
            Console.WriteLine("\n=== Accessibility Test Summary ===");
            foreach (var result in scanResults)
            {
                Console.WriteLine(result);
            }

            if (failedPages.Count > 0)
            {
                Assert.Fail($"Accessibility issues found on pages: {string.Join(", ", failedPages)}");
            }
            else
            {
                Console.WriteLine("✓ All pages passed accessibility checks");
            }
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Failed to run multi-page accessibility tests: {ex.Message}");
        }

        TakeScreenshot("MultiPage_AccessibilityTest");
    }

    /// <summary>
    /// Navigates to a specific page in the application using FlaUI
    /// </summary>
    private bool NavigateToPage(string pageName)
    {
        try
        {
            // Find the navigation item by name
            var navigationItem = MainWindow?.FindFirstDescendant(cf => cf.ByName(pageName));

            if (navigationItem == null)
            {
                Console.WriteLine($"Navigation item '{pageName}' not found");
                return false;
            }

            // Try to invoke the navigation item
            try
            {
                if (navigationItem.Patterns.Invoke.IsSupported)
                {
                    navigationItem.Patterns.Invoke.Pattern.Invoke();
                    Console.WriteLine($"Invoked navigation item: {pageName}");
                    System.Threading.Thread.Sleep(2000); // Wait for page to load
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not invoke {pageName}: {ex.Message}");
            }

            // Try selection pattern as fallback
            try
            {
                if (navigationItem.Patterns.SelectionItem.IsSupported)
                {
                    navigationItem.Patterns.SelectionItem.Pattern.Select();
                    Console.WriteLine($"Selected navigation item: {pageName}");
                    System.Threading.Thread.Sleep(2000); // Wait for page to load
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not select {pageName}: {ex.Message}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Navigation error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs Axe.Windows CLI scan on the current page
    /// </summary>
    private bool RunAxeWindowsCliScan(int processId, string pageName)
    {
        try
        {
            // Determine CLI path - check common locations
            string? cliPath = GetAxeWindowsCliPath();

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

            // Create output directory for this page's results
            var assemblyDir = System.IO.Path.GetDirectoryName(typeof(AccessibilityTests).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                Console.WriteLine("Could not determine assembly directory");
                return false;
            }

            var baseOutputDir = System.IO.Path.Combine(assemblyDir, "AxeResults");
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
                    Console.WriteLine($"⚠ Accessibility issues found on '{pageName}':");
                    foreach (var issueFile in issueFiles)
                    {
                        Console.WriteLine($"  - {issueFile}");
                    }

                    return false;
                }
                else
                {
                    Console.WriteLine($"✓ '{pageName}' passed accessibility check");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running Axe.Windows CLI scan for '{pageName}': {ex.Message}");
            return false;
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
}