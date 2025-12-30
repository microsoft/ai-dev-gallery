// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace AIDevGallery.Utils;

/// <summary>
/// Manages on-demand downloading and loading of CUDA DLLs for NVIDIA GPU users
/// </summary>
internal static class CudaDllManager
{
    // Official Microsoft OnnxRuntimeGenAI NuGet package v0.11.4
    // This downloads the entire NuGet package, then extracts the CUDA DLL
    private const string NuGetPackageUrl = "https://www.nuget.org/api/v2/package/Microsoft.ML.OnnxRuntimeGenAI.Managed/0.11.4";
    private const string DllPathInPackage = "runtimes/win-x64/native/onnxruntime-genai-cuda.dll";

    private static readonly string CudaDllFolder = Path.Combine(ApplicationData.Current.LocalFolder.Path, "CudaDlls");
    private static readonly string CudaDllName = "onnxruntime-genai-cuda.dll";
    private static readonly string CudaDllPath = Path.Combine(CudaDllFolder, CudaDllName);
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    private static bool _downloadAttempted;
    private static bool _isDownloading;

    /// <summary>
    /// Checks if the system has an NVIDIA GPU
    /// </summary>
    /// <returns>True if NVIDIA GPU is detected</returns>
    public static bool HasNvidiaGpu()
    {
        try
        {
            Debug.WriteLine("[CUDA] Checking for NVIDIA GPU...");
            var epDevices = DeviceUtils.GetEpDevices();
            var deviceList = epDevices.ToList();
            Debug.WriteLine($"[CUDA] Found {deviceList.Count} execution provider devices");

            // Check if any device is an NVIDIA GPU by checking for CUDA or TensorRT execution providers
            foreach (var device in deviceList)
            {
                var epName = device.EpName?.ToLowerInvariant() ?? string.Empty;
                Debug.WriteLine($"[CUDA] Checking device with EP: {device.EpName}");

                // Only consider it NVIDIA if it explicitly supports CUDA or TensorRT
                if (epName.Contains("cuda") || epName.Contains("tensorrt"))
                {
                    Debug.WriteLine($"[CUDA] NVIDIA GPU detected via EP: {device.EpName}");
                    return true;
                }
            }

            Debug.WriteLine("[CUDA] No NVIDIA GPU detected");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CUDA] Error detecting NVIDIA GPU: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if CUDA DLL is already available (either in app directory or downloaded)
    /// </summary>
    /// <returns>True if CUDA DLL is available</returns>
    public static bool IsCudaDllAvailable()
    {
        Debug.WriteLine("[CUDA] Checking if CUDA DLL is available...");

        // Check if it exists in the download folder
        if (File.Exists(CudaDllPath))
        {
            Debug.WriteLine($"[CUDA] CUDA DLL found in download folder: {CudaDllPath}");
            return true;
        }

        // Check if it's in the app directory (included in package)
        var appDir = AppContext.BaseDirectory;
        var cudaDllInAppDir = Path.Combine(appDir, CudaDllName);
        if (File.Exists(cudaDllInAppDir))
        {
            Debug.WriteLine($"[CUDA] CUDA DLL found in app directory: {cudaDllInAppDir}");
            return true;
        }

        Debug.WriteLine("[CUDA] CUDA DLL not found");
        return false;
    }

    /// <summary>
    /// Attempts to download CUDA DLL if needed
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if DLL is available (either already exists or successfully downloaded)</returns>
    public static async Task<bool> EnsureCudaDllAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("[CUDA] EnsureCudaDllAsync called");

        // If already available, no need to download
        if (IsCudaDllAvailable())
        {
            Debug.WriteLine("[CUDA] CUDA DLL already available, loading it");
            LoadCudaDll();
            return true;
        }

        // If already attempted and failed, don't try again
        if (_downloadAttempted && !_isDownloading)
        {
            Debug.WriteLine("[CUDA] Download already attempted and failed, skipping");
            return false;
        }

        // Ensure only one download at a time
        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (IsCudaDllAvailable())
            {
                LoadCudaDll();
                return true;
            }

            if (_downloadAttempted && !_isDownloading)
            {
                return false;
            }

            _isDownloading = true;
            _downloadAttempted = true;
            Debug.WriteLine("[CUDA] Starting download process");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(CudaDllFolder);
            Debug.WriteLine($"[CUDA] Created/verified download folder: {CudaDllFolder}");

            // Download and extract from NuGet package
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var tempNupkgPath = Path.Combine(CudaDllFolder, "temp.nupkg");
            Debug.WriteLine($"[CUDA] Downloading NuGet package from: {NuGetPackageUrl}");

            try
            {
                // Download the NuGet package
                using (var fileStream = new FileStream(tempNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(NuGetPackageUrl, fileStream, progress, null, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }

                Debug.WriteLine($"[CUDA] NuGet package downloaded to: {tempNupkgPath}");

                // Extract the CUDA DLL from the NuGet package (which is a ZIP file)
                Debug.WriteLine("[CUDA] Extracting CUDA DLL from NuGet package...");
                using (var archive = System.IO.Compression.ZipFile.OpenRead(tempNupkgPath))
                {
                    var entry = archive.GetEntry(DllPathInPackage);
                    if (entry != null)
                    {
                        entry.ExtractToFile(CudaDllPath, overwrite: true);
                        Debug.WriteLine($"[CUDA] Extracted CUDA DLL to: {CudaDllPath}");
                    }
                    else
                    {
                        Debug.WriteLine($"[CUDA] ERROR: Could not find {DllPathInPackage} in NuGet package");
                        throw new FileNotFoundException($"Could not find {DllPathInPackage} in NuGet package");
                    }
                }

                // Clean up temp file
                File.Delete(tempNupkgPath);
                Debug.WriteLine("[CUDA] Cleaned up temporary NuGet package file");

                // Verify the downloaded file
                // File must be at least 1MB
                if (File.Exists(CudaDllPath) && new FileInfo(CudaDllPath).Length > 1024 * 1024)
                {
                    var fileSizeMB = new FileInfo(CudaDllPath).Length / (1024.0 * 1024.0);
                    Debug.WriteLine($"[CUDA] CUDA DLL verified successfully ({fileSizeMB:F2} MB)");
                    LoadCudaDll();
                    return true;
                }
                else
                {
                    Debug.WriteLine("[CUDA] ERROR: Downloaded file is invalid or too small");

                    // Invalid file, delete it
                    File.Delete(CudaDllPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CUDA] ERROR during download: {ex.Message}");
                Debug.WriteLine($"[CUDA] Stack trace: {ex.StackTrace}");

                // Clean up on failure
                if (File.Exists(CudaDllPath))
                {
                    try
                    {
                        File.Delete(CudaDllPath);
                        Debug.WriteLine("[CUDA] Cleaned up partial CUDA DLL file");
                    }
                    catch
                    {
                    }
                }

                if (File.Exists(tempNupkgPath))
                {
                    try
                    {
                        File.Delete(tempNupkgPath);
                        Debug.WriteLine("[CUDA] Cleaned up temporary NuGet package");
                    }
                    catch
                    {
                    }
                }

                Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().LogException("CudaDllDownloadFailed", ex);
                return false;
            }
        }
        finally
        {
            _isDownloading = false;
            _downloadLock.Release();
            Debug.WriteLine("[CUDA] Download process completed");
        }
    }

    /// <summary>
    /// Loads the CUDA DLL into the process
    /// </summary>
    private static void LoadCudaDll()
    {
        try
        {
            Debug.WriteLine("[CUDA] Attempting to load CUDA DLL...");
            if (File.Exists(CudaDllPath))
            {
                Debug.WriteLine($"[CUDA] CUDA DLL path: {CudaDllPath}");

                // Add the directory to the DLL search path
                NativeLibrary.SetDllImportResolver(typeof(CudaDllManager).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == CudaDllName)
                    {
                        Debug.WriteLine($"[CUDA] DLL import resolver called for: {libraryName}");
                        if (NativeLibrary.TryLoad(CudaDllPath, out var handle))
                        {
                            Debug.WriteLine($"[CUDA] Successfully loaded DLL via resolver, handle: {handle}");
                            return handle;
                        }
                    }

                    return IntPtr.Zero;
                });

                // Pre-load the DLL
                if (NativeLibrary.TryLoad(CudaDllPath, out var mainHandle))
                {
                    Debug.WriteLine($"[CUDA] Successfully pre-loaded CUDA DLL, handle: {mainHandle}");
                    var emptyEvent = new Telemetry.EmptyEvent(Microsoft.Diagnostics.Telemetry.Internal.PartA_PrivTags.ProductAndServiceUsage);
                    Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().Log("CudaDllLoaded", Telemetry.LogLevel.Info, emptyEvent);
                }
                else
                {
                    Debug.WriteLine("[CUDA] WARNING: Failed to pre-load CUDA DLL");
                }
            }
            else
            {
                Debug.WriteLine($"[CUDA] WARNING: CUDA DLL not found at expected path: {CudaDllPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CUDA] ERROR loading CUDA DLL: {ex.Message}");
            Debug.WriteLine($"[CUDA] Stack trace: {ex.StackTrace}");
            Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().LogException("CudaDllLoadFailed", ex);
        }
    }

    /// <summary>
    /// Gets the download status message for UI display
    /// </summary>
    /// <returns>Status message for UI display</returns>
    public static string GetStatusMessage()
    {
        if (IsCudaDllAvailable())
        {
            return "NVIDIA GPU acceleration (CUDA) is available";
        }

        if (_isDownloading)
        {
            return "Downloading NVIDIA GPU acceleration support...";
        }

        if (_downloadAttempted)
        {
            return "NVIDIA GPU acceleration download failed. Using DirectML instead.";
        }

        return "NVIDIA GPU detected. CUDA acceleration can be downloaded for better performance.";
    }

    /// <summary>
    /// Gets the folder path where CUDA DLL is stored
    /// </summary>
    /// <returns>The folder path</returns>
    public static string GetCudaDllFolderPath()
    {
        return CudaDllFolder;
    }
}