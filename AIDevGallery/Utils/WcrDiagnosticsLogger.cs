// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.System.Profile;

namespace AIDevGallery.Utils;

/// <summary>
/// Lightweight, always-on diagnostics logger used to capture the full context around
/// Windows AI (WCR) model readiness failures on a customer device. It keeps an in-memory
/// copy (for one-click clipboard copy) and appends to a local log file so the information
/// can be retrieved even when telemetry is not accessible. Intended for private diagnostic drops.
/// Every operation is defensive: diagnostics logging must never throw into the app.
/// </summary>
internal static class WcrDiagnosticsLogger
{
    private const string LogFolderName = "Diagnostics";
    private const string LogFileName = "wcr-diagnostic.log";

    private static readonly object SyncRoot = new();
    private static readonly StringBuilder Buffer = new();
    private static bool _environmentLogged;
    private static string? _logFilePath;

    /// <summary>
    /// Gets the full path to the diagnostics log folder, or null if it could not be resolved yet.
    /// </summary>
    public static string? LogFolderPath { get; private set; }

    /// <summary>
    /// Appends a single timestamped line to the diagnostics log.
    /// </summary>
    public static void Log(string message)
    {
        Write($"[{Timestamp()}] {message}");
    }

    /// <summary>
    /// Appends a titled section header to the diagnostics log.
    /// </summary>
    public static void LogSection(string title)
    {
        Write(string.Empty);
        Write($"===== {title} =====");
    }

    /// <summary>
    /// Returns the full in-memory diagnostics log for the current session (for clipboard copy).
    /// </summary>
    /// <returns>The accumulated diagnostics text, or a placeholder when nothing has been captured yet.</returns>
    public static string GetLogText()
    {
        lock (SyncRoot)
        {
            return Buffer.Length == 0
                ? "No diagnostics have been captured yet."
                : Buffer.ToString();
        }
    }

    /// <summary>
    /// Captures device / OS / runtime / locale environment information. Runs only once per process.
    /// </summary>
    public static void LogEnvironmentOnce()
    {
        lock (SyncRoot)
        {
            if (_environmentLogged)
            {
                return;
            }

            _environmentLogged = true;
        }

        LogSection($"AI Dev Gallery WCR diagnostics - session started {Timestamp()}");
        Safe("App version", () => AppUtils.GetAppVersion());
        Safe("Process architecture", () => RuntimeInformation.ProcessArchitecture.ToString());
        Safe("OS architecture", () => RuntimeInformation.OSArchitecture.ToString());
        Safe("OS version (AnalyticsInfo)", GetOsVersionFromAnalyticsInfo);
        Safe("OS version (Environment)", () => Environment.OSVersion.Version.ToString());
        Safe("OS DisplayVersion", () => ReadRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion"));
        Safe("OS ProductName", () => ReadRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName"));
        Safe("OS UBR", () => ReadRegistryValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR"));
        Safe("Processor", () => ReadRegistryValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"));
        Safe("Framework dependencies", GetFrameworkDependencies);
    }

    private static string GetOsVersionFromAnalyticsInfo()
    {
        string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
        if (!ulong.TryParse(deviceFamilyVersion, out ulong v))
        {
            return deviceFamilyVersion;
        }

        ulong major = (v & 0xFFFF000000000000UL) >> 48;
        ulong minor = (v & 0x0000FFFF00000000UL) >> 32;
        ulong build = (v & 0x00000000FFFF0000UL) >> 16;
        ulong revision = v & 0x000000000000FFFFUL;
        return $"{major}.{minor}.{build}.{revision} (DeviceFamily: {AnalyticsInfo.VersionInfo.DeviceFamily})";
    }

    private static string GetFrameworkDependencies()
    {
        var sb = new StringBuilder();
        foreach (var dep in Package.Current.Dependencies)
        {
            var version = dep.Id.Version;
            sb.Append(CultureInfo.InvariantCulture, $"{Environment.NewLine}    {dep.Id.FamilyName} {version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
        }

        return sb.Length == 0 ? "(none)" : sb.ToString();
    }

    private static string ReadRegistryValue(string subKey, string valueName)
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
        return key?.GetValue(valueName)?.ToString() ?? "(not found)";
    }

    private static void Safe(string label, Func<string> getter)
    {
        string value;
        try
        {
            value = getter() ?? "(null)";
        }
        catch (Exception ex)
        {
            value = $"(error: {ex.Message})";
        }

        Write($"{label}: {value}");
    }

    private static void Write(string line)
    {
        lock (SyncRoot)
        {
            Buffer.AppendLine(line);

            try
            {
                EnsureLogFilePath();
                if (_logFilePath != null)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never let diagnostics logging break the app; the in-memory buffer still has the content.
            }
        }
    }

    private static void EnsureLogFilePath()
    {
        if (_logFilePath != null)
        {
            return;
        }

        string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        string folder = Path.Combine(localFolder, LogFolderName);
        Directory.CreateDirectory(folder);
        LogFolderPath = folder;
        _logFilePath = Path.Combine(folder, LogFileName);
    }

    private static string Timestamp()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
    }
}