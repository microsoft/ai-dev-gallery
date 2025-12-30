// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
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
            var epDevices = DeviceUtils.GetEpDevices();

            // Check if any device is an NVIDIA GPU by checking for CUDA or TensorRT execution providers
            foreach (var device in epDevices)
            {
                var epName = device.EpName?.ToLowerInvariant() ?? string.Empty;

                // Only consider it NVIDIA if it explicitly supports CUDA or TensorRT
                if (epName.Contains("cuda") || epName.Contains("tensorrt"))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if CUDA DLL is already available (either in app directory or downloaded)
    /// </summary>
    /// <returns>True if CUDA DLL is available</returns>
    public static bool IsCudaDllAvailable()
    {
        // Check if it exists in the download folder
        if (File.Exists(CudaDllPath))
        {
            return true;
        }

        // Check if it's in the app directory (included in package)
        var appDir = AppContext.BaseDirectory;
        var cudaDllInAppDir = Path.Combine(appDir, CudaDllName);
        return File.Exists(cudaDllInAppDir);
    }

    /// <summary>
    /// Attempts to download CUDA DLL if needed
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if DLL is available (either already exists or successfully downloaded)</returns>
    public static async Task<bool> EnsureCudaDllAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        // If already available, no need to download
        if (IsCudaDllAvailable())
        {
            LoadCudaDll();
            return true;
        }

        // If already attempted and failed, don't try again
        if (_downloadAttempted && !_isDownloading)
        {
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

            // Create directory if it doesn't exist
            Directory.CreateDirectory(CudaDllFolder);

            // Download and extract from NuGet package
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var tempNupkgPath = Path.Combine(CudaDllFolder, "temp.nupkg");

            try
            {
                // Download the NuGet package
                using (var fileStream = new FileStream(tempNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await client.DownloadAsync(NuGetPackageUrl, fileStream, progress, null, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }

                // Extract the CUDA DLL from the NuGet package (which is a ZIP file)
                using (var archive = System.IO.Compression.ZipFile.OpenRead(tempNupkgPath))
                {
                    var entry = archive.GetEntry(DllPathInPackage);
                    if (entry != null)
                    {
                        entry.ExtractToFile(CudaDllPath, overwrite: true);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Could not find {DllPathInPackage} in NuGet package");
                    }
                }

                // Clean up temp file
                File.Delete(tempNupkgPath);

                // Verify the downloaded file
                // File must be at least 1MB
                if (File.Exists(CudaDllPath) && new FileInfo(CudaDllPath).Length > 1024 * 1024)
                {
                    LoadCudaDll();
                    return true;
                }
                else
                {
                    // Invalid file, delete it
                    File.Delete(CudaDllPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Clean up on failure
                if (File.Exists(CudaDllPath))
                {
                    try
                    {
                        File.Delete(CudaDllPath);
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
        }
    }

    /// <summary>
    /// Loads the CUDA DLL into the process
    /// </summary>
    private static void LoadCudaDll()
    {
        try
        {
            if (File.Exists(CudaDllPath))
            {
                // Add the directory to the DLL search path
                NativeLibrary.SetDllImportResolver(typeof(CudaDllManager).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == CudaDllName)
                    {
                        if (NativeLibrary.TryLoad(CudaDllPath, out var handle))
                        {
                            return handle;
                        }
                    }

                    return IntPtr.Zero;
                });

                // Pre-load the DLL
                if (NativeLibrary.TryLoad(CudaDllPath, out _))
                {
                    var emptyEvent = new Telemetry.EmptyEvent(Microsoft.Diagnostics.Telemetry.Internal.PartA_PrivTags.ProductAndServiceUsage);
                    Telemetry.TelemetryFactory.Get<Telemetry.ITelemetry>().Log("CudaDllLoaded", Telemetry.LogLevel.Info, emptyEvent);
                }
            }
        }
        catch (Exception ex)
        {
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