// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace AIDevGallery.Tests.TestInfra;

/// <summary>
/// Base class for FlaUI-based UI tests.
/// Provides common functionality for launching and managing the AIDevGallery application.
/// </summary>
public abstract class FlaUITestBase
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }
    protected DateTime AppLaunchStartTime { get; private set; }

    /// <summary>
    /// Gets the path to the AIDevGallery executable.
    /// </summary>
    /// <returns>The full path to the AIDevGallery executable.</returns>
    protected virtual string GetApplicationPath()
    {
        // Try to find the built application
        var solutionDir = FindSolutionDirectory();
        if (solutionDir == null)
        {
            throw new FileNotFoundException("Could not find solution directory");
        }

        // Determine architecture
        var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";

        // Try multiple possible locations
        var possiblePaths = new[]
        {
            // Debug builds
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Debug", "net9.0-windows10.0.26100.0", $"win-{arch}", "AIDevGallery.exe"),
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Debug", "net9.0-windows10.0.26100.0", "AIDevGallery.exe"),

            // Release builds
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Release", "net9.0-windows10.0.26100.0", $"win-{arch}", "AIDevGallery.exe"),
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Release", "net9.0-windows10.0.26100.0", "AIDevGallery.exe"),

            // AppPackages (for packaged builds)
            Path.Combine(solutionDir, "AIDevGallery", "AppPackages", "AIDevGallery.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Found application at: {path}");
                return path;
            }
        }

        var searchedPaths = string.Join(Environment.NewLine + "  ", possiblePaths);
        throw new FileNotFoundException(
            $"Could not find AIDevGallery.exe at any expected location. Please build the application first.{Environment.NewLine}" +
            $"Searched paths:{Environment.NewLine}  {searchedPaths}");
    }

    /// <summary>
    /// Finds the solution directory by walking up from the current directory.
    /// </summary>
    private static string? FindSolutionDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir, "AIDevGallery.sln")))
            {
                return currentDir;
            }

            var parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        return null;
    }

    /// <summary>
    /// Try to get the package family name if the app is installed via MSIX.
    /// </summary>
    private static string? TryGetInstalledPackageFamilyName()
    {
        try
        {
            // Use PowerShell to query installed packages
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"Get-AppxPackage | Where-Object {{$_.Name -like '*{TestConfiguration.MsixPackageIdentityName}*'}} | Select-Object -First 1 -ExpandProperty PackageFamilyName\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output;
            }

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Error querying packages: {error}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while checking for MSIX package: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Find the AIDevGallery application process.
    /// </summary>
    private static Process? FindApplicationProcess()
    {
        // Try multiple times with delays
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var processes = Process.GetProcessesByName("AIDevGallery");
            if (processes.Length > 0)
            {
                // Return the most recently started process
                return processes.OrderByDescending(p => p.StartTime).First();
            }

            Thread.Sleep(500);
        }

        return null;
    }

    /// <summary>
    /// Initializes the test by launching the application.
    /// </summary>
    [TestInitialize]
    public virtual void TestInitialize()
    {
        Automation = new UIA3Automation();

        // Check if application is already running and close it
        CloseExistingApplicationInstances();

        // Record start time
        AppLaunchStartTime = DateTime.UtcNow;

        // Launch the application
        Console.WriteLine("Attempting to launch AIDevGallery...");

        // First try to find installed MSIX package
        var packageFamilyName = TryGetInstalledPackageFamilyName();

        if (!string.IsNullOrEmpty(packageFamilyName))
        {
            Console.WriteLine($"Found installed MSIX package: {packageFamilyName}");
            Console.WriteLine("Launching via PowerShell...");

            try
            {
                // Launch MSIX app using PowerShell Get-AppxPackage and Start-Process
                var appUserModelId = $"{packageFamilyName}!App";

                var psScript = $"Start-Process 'shell:AppsFolder\\{appUserModelId}'";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Launch via PowerShell
                using (var psProcess = Process.Start(startInfo))
                {
                    psProcess?.WaitForExit(5000);
                }

                // Wait for the app process to start
                Console.WriteLine("Waiting for application process to start...");
                Thread.Sleep(2000);

                // Find the app process
                var appProcess = FindApplicationProcess();
                if (appProcess == null)
                {
                    throw new InvalidOperationException("Application process not found after launch");
                }

                Console.WriteLine($"Found application process with PID: {appProcess.Id}");
                App = Application.Attach(appProcess.Id);
                Console.WriteLine($"Attached to application with PID: {App.ProcessId}");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    $"Access denied when launching MSIX package: {packageFamilyName}{Environment.NewLine}" +
                    $"Error: {ex.Message}{Environment.NewLine}" +
                    $"Try running Visual Studio or the test runner as Administrator.",
                    ex);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to launch MSIX package via PowerShell: {packageFamilyName}{Environment.NewLine}" +
                    $"Error: {ex.Message}{Environment.NewLine}" +
                    $"Ensure PowerShell is available and the package is properly installed.",
                    ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch via MSIX: {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not launch MSIX package: {packageFamilyName}{Environment.NewLine}" +
                    $"Error: {ex.Message}{Environment.NewLine}" +
                    $"Try launching the app manually from Start Menu to verify it works.",
                    ex);
            }
        }
        else
        {
            // Fall back to unpackaged exe (will likely fail with COM error)
            var appPath = GetApplicationPath();
            Console.WriteLine($"WARNING: No MSIX package found. Trying unpackaged exe: {appPath}");
            Console.WriteLine("This will likely fail with COM registration errors!");
            Console.WriteLine($"Please deploy the MSIX package first. See: MSIX_DEPLOYMENT_REQUIRED.md");

            try
            {
                App = Application.Launch(appPath);
                Console.WriteLine($"Application launched with PID: {App.ProcessId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch unpackaged application: {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not launch unpackaged application. WinUI3 requires MSIX deployment for testing.{Environment.NewLine}" +
                    $"Please run: msbuild AIDevGallery\\AIDevGallery.csproj /t:Deploy /p:Configuration=Debug /p:Platform=x64{Environment.NewLine}" +
                    $"See MSIX_DEPLOYMENT_REQUIRED.md for details.",
                    ex);
            }
        }

        // Wait for the main window to appear with extended timeout
        var timeout = TimeSpan.FromSeconds(60); // Increased timeout for first launch
        var startTime = DateTime.Now;
        var retryInterval = TimeSpan.FromMilliseconds(500);

        Console.WriteLine("Waiting for main window to appear...");

        while (MainWindow == null && DateTime.Now - startTime < timeout)
        {
            try
            {
                // Try to get main window with a short timeout for each attempt
                MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(2));

                if (MainWindow != null && MainWindow.IsAvailable)
                {
                    Console.WriteLine($"Main window found after {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
                    break;
                }
            }
            catch (Exception)
            {
                // Window not ready yet, continue waiting
                var elapsed = DateTime.Now - startTime;

                // Log every ~10 seconds
                if (elapsed.TotalSeconds % 10 < 1)
                {
                    Console.WriteLine($"Still waiting for window... ({elapsed.TotalSeconds:F0}s elapsed)");
                }
            }

            Thread.Sleep(retryInterval);
        }

        if (MainWindow == null)
        {
            var elapsed = DateTime.Now - startTime;
            Console.WriteLine($"Failed to find main window after {elapsed.TotalSeconds:F1} seconds");

            // Try to get diagnostic information
            try
            {
                var allWindows = App.GetAllTopLevelWindows(Automation);
                Console.WriteLine($"Found {allWindows.Length} top-level windows");
                foreach (var window in allWindows.Take(5))
                {
                    Console.WriteLine($"  Window: Title='{window.Title}', ClassName='{window.ClassName}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get diagnostic info: {ex.Message}");
            }

            throw new InvalidOperationException($"Failed to get main window within {timeout.TotalSeconds} seconds");
        }

        // Give the UI a moment to fully initialize
        Console.WriteLine("Main window ready, waiting for UI initialization...");
        Thread.Sleep(2000);
    }

    /// <summary>
    /// Cleans up after the test by closing the application.
    /// </summary>
    [TestCleanup]
    public virtual void TestCleanup()
    {
        Console.WriteLine("Cleaning up test...");

        try
        {
            if (MainWindow != null && MainWindow.IsAvailable)
            {
                Console.WriteLine("Closing main window...");
                MainWindow.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing main window: {ex.Message}");
        }

        try
        {
            if (App != null)
            {
                Console.WriteLine("Closing application...");
                App.Close();
                App.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing application: {ex.Message}");
        }
        finally
        {
            try
            {
                Automation?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing automation: {ex.Message}");
            }

            CloseExistingApplicationInstances();
            Console.WriteLine("Test cleanup completed");
        }
    }

    /// <summary>
    /// Closes any existing instances of AIDevGallery.
    /// </summary>
    private static void CloseExistingApplicationInstances()
    {
        var processes = Process.GetProcessesByName("AIDevGallery");
        if (processes.Length > 0)
        {
            Console.WriteLine($"Found {processes.Length} existing AIDevGallery process(es), terminating...");
        }

        foreach (var process in processes)
        {
            try
            {
                var processId = process.Id;
                process.Kill(entireProcessTree: true);
                var exited = process.WaitForExit(5000);

                if (exited)
                {
                    Console.WriteLine($"Terminated process {processId}");
                }
                else
                {
                    Console.WriteLine($"Process {processId} did not exit within timeout");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// Waits for an element to appear with the specified automation ID.
    /// </summary>
    /// <returns>The automation element if found, null otherwise.</returns>
    protected AutomationElement? WaitForElement(string automationId, TimeSpan timeout)
    {
        if (MainWindow == null)
        {
            return null;
        }

        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                var element = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (element != null)
                {
                    return element;
                }
            }
            catch
            {
                // Element not found yet
            }

            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>
    /// Takes a screenshot of the main window for debugging purposes.
    /// </summary>
    protected void TakeScreenshot(string filename)
    {
        if (MainWindow == null)
        {
            Console.WriteLine("Cannot take screenshot: MainWindow is null");
            return;
        }

        try
        {
            var screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(screenshotDir);

            var screenshotPath = Path.Combine(screenshotDir, $"{filename}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            // Get the window bounds
            var bounds = MainWindow.BoundingRectangle;
            Console.WriteLine($"Capturing window at: X={bounds.X}, Y={bounds.Y}, Width={bounds.Width}, Height={bounds.Height}");

            // Capture the entire window area including title bar and borders
            var screenshot = FlaUI.Core.Capturing.Capture.Rectangle(bounds);

            if (screenshot?.Bitmap != null)
            {
                screenshot.Bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine($"Screenshot saved to: {screenshotPath}");
                Console.WriteLine($"Screenshot size: {screenshot.Bitmap.Width}x{screenshot.Bitmap.Height}");
            }
            else
            {
                Console.WriteLine("Failed to capture screenshot: Bitmap is null");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to take screenshot: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}