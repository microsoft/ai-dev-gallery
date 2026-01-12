// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace AIDevGallery.Helpers;

/// <summary>
/// Helper methods for starting external processes (browser, explorer, etc.)
/// where the process lifecycle is independent of the application.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Starts a process using the shell to open a URL in the default browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    public static void OpenUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            // Process.Start for external process (browser) doesn't need disposal - process lifecycle is independent
#pragma warning disable IDISP004 // Don't ignore created IDisposable
            Process.Start(new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true
            });
#pragma warning restore IDISP004
        }
    }

    /// <summary>
    /// Opens Windows Explorer to the specified folder path.
    /// </summary>
    /// <param name="folderPath">The folder path to open in Explorer.</param>
    public static void OpenFolder(string folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            // Process.Start for explorer doesn't need disposal - process lifecycle is independent
#pragma warning disable IDISP004 // Don't ignore created IDisposable
            Process.Start("explorer.exe", folderPath);
#pragma warning restore IDISP004
        }
    }
}