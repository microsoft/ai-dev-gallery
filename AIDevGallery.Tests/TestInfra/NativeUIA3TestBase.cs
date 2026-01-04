// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Interop.UIAutomationClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AIDevGallery.Tests.TestInfra;

/// <summary>
/// Base class for native UIA3-based UI tests.
/// Uses Windows native UIAutomation API directly without FlaUI.
/// </summary>
public abstract class NativeUIA3TestBase
{
    protected Process? AppProcess { get; private set; }
    protected CUIAutomation? Automation { get; private set; }
    protected IUIAutomationElement? MainWindow { get; private set; }
    protected DateTime AppLaunchStartTime { get; private set; }

    /// <summary>
    /// Gets the path to the AIDevGallery executable.
    /// </summary>
    /// <returns>The full path to the AIDevGallery executable.</returns>
    protected virtual string GetApplicationPath()
    {
        var solutionDir = FindSolutionDirectory();
        if (solutionDir == null)
        {
            throw new FileNotFoundException("Could not find solution directory");
        }

        var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";

        var possiblePaths = new[]
        {
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Debug", "net9.0-windows10.0.26100.0", $"win-{arch}", "AIDevGallery.exe"),
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Debug", "net9.0-windows10.0.26100.0", "AIDevGallery.exe"),
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Release", "net9.0-windows10.0.26100.0", $"win-{arch}", "AIDevGallery.exe"),
            Path.Combine(solutionDir, "AIDevGallery", "bin", arch, "Release", "net9.0-windows10.0.26100.0", "AIDevGallery.exe"),
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
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var processes = Process.GetProcessesByName("AIDevGallery");
            if (processes.Length > 0)
            {
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
        // Create UIA automation object
        Automation = new CUIAutomation();

        // Close any existing instances
        CloseExistingApplicationInstances();

        // Record start time
        AppLaunchStartTime = DateTime.UtcNow;

        // Launch the application
        Console.WriteLine("Attempting to launch AIDevGallery...");

        var packageFamilyName = TryGetInstalledPackageFamilyName();

        if (!string.IsNullOrEmpty(packageFamilyName))
        {
            Console.WriteLine($"Found installed MSIX package: {packageFamilyName}");
            Console.WriteLine("Launching via PowerShell...");

            try
            {
                var appUserModelId = $"{packageFamilyName}!App";
                var psScript = $"Start-Process 'shell:AppsFolder\\{appUserModelId}'";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var psProcess = Process.Start(startInfo))
                {
                    psProcess?.WaitForExit(5000);
                }

                Console.WriteLine("Waiting for application process to start...");
                Thread.Sleep(2000);

                AppProcess = FindApplicationProcess();
                if (AppProcess == null)
                {
                    throw new InvalidOperationException("Application process not found after launch");
                }

                Console.WriteLine($"Found application process with PID: {AppProcess.Id}");
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
            var appPath = GetApplicationPath();
            Console.WriteLine($"WARNING: No MSIX package found. Trying unpackaged exe: {appPath}");
            Console.WriteLine("This will likely fail with COM registration errors!");

            try
            {
                AppProcess = Process.Start(appPath);
                if (AppProcess == null)
                {
                    throw new InvalidOperationException("Failed to start process");
                }

                Console.WriteLine($"Application launched with PID: {AppProcess.Id}");
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

        // Wait for the main window to appear
        var timeout = TimeSpan.FromSeconds(60);
        var startTime = DateTime.Now;

        Console.WriteLine("Waiting for main window to appear...");

        while (MainWindow == null && DateTime.Now - startTime < timeout)
        {
            try
            {
                MainWindow = FindMainWindow();

                if (MainWindow != null)
                {
                    Console.WriteLine($"Main window found after {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
                    break;
                }
            }
            catch (Exception)
            {
                var elapsed = DateTime.Now - startTime;
                if (elapsed.TotalSeconds % 10 < 1)
                {
                    Console.WriteLine($"Still waiting for window... ({elapsed.TotalSeconds:F0}s elapsed)");
                }
            }

            Thread.Sleep(500);
        }

        if (MainWindow == null)
        {
            var elapsed = DateTime.Now - startTime;
            Console.WriteLine($"Failed to find main window after {elapsed.TotalSeconds:F1} seconds");
            throw new InvalidOperationException($"Failed to get main window within {timeout.TotalSeconds} seconds");
        }

        Console.WriteLine("Main window ready, waiting for UI initialization...");
        Thread.Sleep(2000);
    }

    /// <summary>
    /// Find the main window using native UIA3.
    /// </summary>
    private IUIAutomationElement? FindMainWindow()
    {
        if (Automation == null || AppProcess == null)
        {
            return null;
        }

        try
        {
            // Get all windows from root
            var rootElement = Automation.GetRootElement();
            var condition = Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ProcessIdPropertyId, AppProcess.Id);
            var windows = rootElement.FindAll(TreeScope.TreeScope_Children, condition);

            if (windows == null || windows.Length == 0)
            {
                return null;
            }

            // Return the first window found for this process
            return windows.GetElement(0);
        }
        catch (COMException)
        {
            return null;
        }
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
            if (MainWindow != null)
            {
                Console.WriteLine("Closing main window...");
                try
                {
                    // Try to close window pattern if available
                    var windowPattern = MainWindow.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) as IUIAutomationWindowPattern;
                    windowPattern?.Close();
                }
                catch (COMException ex)
                {
                    Console.WriteLine($"Could not close window via pattern: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing main window: {ex.Message}");
        }

        try
        {
            if (AppProcess != null && !AppProcess.HasExited)
            {
                Console.WriteLine("Terminating application process...");
                AppProcess.Kill(entireProcessTree: true);
                AppProcess.WaitForExit(5000);
                AppProcess.Dispose();
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
                // Release COM objects
                if (MainWindow != null)
                {
                    Marshal.ReleaseComObject(MainWindow);
                    MainWindow = null;
                }

                if (Automation != null)
                {
                    Marshal.ReleaseComObject(Automation);
                    Automation = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error releasing COM objects: {ex.Message}");
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
    /// <returns>The UI automation element if found, null otherwise.</returns>
    protected IUIAutomationElement? WaitForElement(string automationId, TimeSpan timeout)
    {
        if (MainWindow == null || Automation == null)
        {
            return null;
        }

        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                var condition = Automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, automationId);
                var element = MainWindow.FindFirst(TreeScope.TreeScope_Descendants, condition);
                if (element != null)
                {
                    return element;
                }
            }
            catch (COMException)
            {
                // Element not found yet
            }

            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>
    /// Gets all descendant elements.
    /// </summary>
    /// <returns>Array of all descendant UI automation elements.</returns>
    protected IUIAutomationElement[] GetAllDescendants()
    {
        if (MainWindow == null || Automation == null)
        {
            return Array.Empty<IUIAutomationElement>();
        }

        try
        {
            var condition = Automation.CreateTrueCondition();
            var elements = MainWindow.FindAll(TreeScope.TreeScope_Descendants, condition);

            if (elements == null)
            {
                return Array.Empty<IUIAutomationElement>();
            }

            var result = new IUIAutomationElement[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                result[i] = elements.GetElement(i);
            }

            return result;
        }
        catch (COMException)
        {
            return Array.Empty<IUIAutomationElement>();
        }
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
            var rect = MainWindow.CurrentBoundingRectangle;
            Console.WriteLine($"Capturing window at: X={rect.left}, Y={rect.top}, Width={rect.right - rect.left}, Height={rect.bottom - rect.top}");

            // Use GDI+ to capture screenshot
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;

            using var bitmap = new System.Drawing.Bitmap(width, height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new System.Drawing.Size(width, height));
            bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

            Console.WriteLine($"Screenshot saved to: {screenshotPath}");
            Console.WriteLine($"Screenshot size: {bitmap.Width}x{bitmap.Height}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to take screenshot: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}