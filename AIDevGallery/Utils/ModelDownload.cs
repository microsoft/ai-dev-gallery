// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal abstract class ModelDownload : IDisposable
{
    public event EventHandler<ModelDownloadEventArgs>? StateChanged;
    public ModelDetails Details { get; }

    private DownloadStatus _downloadStatus;
    public DownloadStatus DownloadStatus
    {
        get => _downloadStatus;
        protected set
        {
            _downloadStatus = value;
            StateChanged?.Invoke(this, new ModelDownloadEventArgs
            {
                Progress = DownloadProgress,
                Status = _downloadStatus
            });
        }
    }

    private float _downloadProgress;
    public float DownloadProgress
    {
        get => _downloadProgress;
        protected set
        {
            _downloadProgress = value;
            StateChanged?.Invoke(this, new ModelDownloadEventArgs
            {
                Progress = _downloadProgress,
                Status = DownloadStatus
            });
        }
    }

    private string? _verificationFailureMessage;
    public string? VerificationFailureMessage
    {
        get => _verificationFailureMessage;
        protected set
        {
            _verificationFailureMessage = value;
            StateChanged?.Invoke(this, new ModelDownloadEventArgs
            {
                Progress = DownloadProgress,
                Status = DownloadStatus,
                VerificationFailureMessage = _verificationFailureMessage
            });
        }
    }

    private string? _warningMessage;
    public string? WarningMessage
    {
        get => _warningMessage;
        protected set
        {
            _warningMessage = value;
            StateChanged?.Invoke(this, new ModelDownloadEventArgs
            {
                Progress = DownloadProgress,
                Status = DownloadStatus,
                WarningMessage = _warningMessage
            });
        }
    }

    protected CancellationTokenSource CancellationTokenSource { get; }

    public void Dispose()
    {
        CancellationTokenSource.Dispose();
    }

    public ModelDownload(ModelDetails details)
    {
        Details = details;
        CancellationTokenSource = new CancellationTokenSource();
        DownloadStatus = DownloadStatus.Waiting;
    }

    public abstract Task<bool> StartDownload();

    public abstract void CancelDownload();
}

internal class OnnxModelDownload : ModelDownload
{
    public ModelUrl ModelUrl { get; set; }

    public OnnxModelDownload(ModelDetails details)
        : base(details)
    {
        if (details.Url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
        {
            ModelUrl = new GitHubUrl(details.Url);
        }
        else
        {
            ModelUrl = new HuggingFaceUrl(details.Url);
        }
    }

    public override async Task<bool> StartDownload()
    {
        DownloadStatus = DownloadStatus.InProgress;

        Progress<float> internalProgress = new(p =>
        {
            DownloadProgress = p;
        });

        CachedModel? cachedModel = null;

        try
        {
            cachedModel = await DownloadModel(App.AppData.ModelCachePath, internalProgress);
        }
        catch (Exception ex)
        {
            ModelDownloadFailedEvent.Log(Details.Url, ex);
        }

        if (cachedModel == null)
        {
            if (DownloadStatus != DownloadStatus.VerificationFailed)
            {
                DownloadStatus = DownloadStatus.Canceled;

                var localPath = ModelUrl.GetLocalPath(App.AppData.ModelCachePath);
                if (Directory.Exists(localPath))
                {
                    Directory.Delete(localPath, true);
                }
            }

            return false;
        }

        await App.ModelCache.CacheStore.AddModel(cachedModel);
        DownloadStatus = DownloadStatus.Completed;
        return true;
    }

    public override void CancelDownload()
    {
        CancellationTokenSource.Cancel();
        DownloadStatus = DownloadStatus.Canceled;
    }

    private async Task<CachedModel?> DownloadModel(string cacheDir, IProgress<float>? progress = null)
    {
        ModelUrl url;
        List<ModelFileDetails> filesToDownload;
        var cancellationToken = CancellationTokenSource.Token;

        if (Details.Url.StartsWith("https://github.com", StringComparison.InvariantCulture))
        {
            var ghUrl = new GitHubUrl(Details.Url);
            filesToDownload = await ModelInformationHelper.GetDownloadFilesFromGitHub(ghUrl, cancellationToken);
            url = ghUrl;
        }
        else
        {
            var hfUrl = new HuggingFaceUrl(Details.Url);
            using var socketsHttpHandler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 4
            };
            filesToDownload = await ModelInformationHelper.GetDownloadFilesFromHuggingFace(hfUrl, socketsHttpHandler, cancellationToken);
            url = hfUrl;
        }

        var localFolderPath = $"{cacheDir}\\{url.Organization}--{url.Repo}\\{url.Ref}";
        Directory.CreateDirectory(localFolderPath);

        var existingFiles = Directory.GetFiles(localFolderPath, "*", SearchOption.AllDirectories);

        filesToDownload = ModelInformationHelper.FilterFiles(filesToDownload, Details.FileFilters);

        if (filesToDownload.Count == 0)
        {
            ModelDownloadFailedEvent.Log(Details.Url, new InvalidOperationException("No files to download after filtering"));
            DownloadStatus = DownloadStatus.Canceled;
            return null;
        }

        long modelSize = filesToDownload.Sum(f => f.Size);
        long bytesDownloaded = 0;

        var internalProgress = new Progress<long>(p =>
        {
            var percentage = (float)(bytesDownloaded + p) / (float)modelSize;
            progress?.Report(percentage);
        });

        using var client = new HttpClient();

        // Track files that need verification
        List<(string FilePath, ModelFileDetails FileDetails)> filesToVerify = [];

        foreach (var downloadableFile in filesToDownload)
        {
            if (downloadableFile.DownloadUrl == null)
            {
                continue;
            }

            var filePath = Path.Combine(localFolderPath, downloadableFile.Path!.Replace("/", "\\"));

            var existingFile = existingFiles.Where(f => f == filePath).FirstOrDefault();
            if (existingFile != null)
            {
                // check if the file is the same size as the one on the server
                var existingFileInfo = new FileInfo(existingFile);
                if (existingFileInfo.Length == downloadableFile.Size)
                {
                    // Still need to verify existing files if they have a hash
                    if (downloadableFile.ShouldVerifyIntegrity && downloadableFile.HasVerificationHash)
                    {
                        filesToVerify.Add((filePath, downloadableFile));
                    }

                    continue;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using (FileStream file = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await client.DownloadAsync(downloadableFile.DownloadUrl, file, null, internalProgress, cancellationToken);
                file.Close();
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != downloadableFile.Size)
            {
                // Size mismatch - log telemetry
                ModelIntegrityVerificationFailedEvent.Log(
                    Details.Url,
                    downloadableFile.Name ?? filePath,
                    verificationType: "Size",
                    expectedValue: downloadableFile.Size.ToString(CultureInfo.InvariantCulture),
                    actualValue: fileInfo.Length.ToString(CultureInfo.InvariantCulture));
                VerificationFailureMessage = $"Size verification failed for: {downloadableFile.Name}";
                DownloadStatus = DownloadStatus.VerificationFailed;

                var localPath = url.GetLocalPath(cacheDir);
                if (Directory.Exists(localPath))
                {
                    Directory.Delete(localPath, true);
                }

                return null;
            }

            // Add to verification list if it's a main model file with hash
            if (downloadableFile.ShouldVerifyIntegrity && downloadableFile.HasVerificationHash)
            {
                filesToVerify.Add((filePath, downloadableFile));
            }

            bytesDownloaded += downloadableFile.Size;
        }

        // Verify integrity of main model files
        if (filesToVerify.Count > 0)
        {
            DownloadStatus = DownloadStatus.Verifying;

            foreach (var (filePath, fileDetails) in filesToVerify)
            {
                if (string.IsNullOrEmpty(fileDetails.Sha256))
                {
                    continue;
                }

                var expectedHash = fileDetails.Sha256;
                var actualHash = await ComputeSha256Async(filePath, cancellationToken);
                var verified = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

                if (!verified)
                {
                    ModelIntegrityVerificationFailedEvent.Log(
                        Details.Url,
                        fileDetails.Name ?? filePath,
                        verificationType: "SHA256",
                        expectedValue: expectedHash,
                        actualValue: actualHash);
                    VerificationFailureMessage = $"Integrity verification failed for: {fileDetails.Name ?? filePath}";
                    DownloadStatus = DownloadStatus.VerificationFailed;

                    // Delete the downloaded files
                    var localPath = url.GetLocalPath(cacheDir);
                    if (Directory.Exists(localPath))
                    {
                        Directory.Delete(localPath, true);
                    }

                    return null;
                }
            }
        }

        var modelDirectory = url.GetLocalPath(cacheDir);

        return new CachedModel(Details, url.IsFile ? $"{modelDirectory}\\{filesToDownload.First().Name}" : modelDirectory, url.IsFile, modelSize);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

internal class FoundryLocalModelDownload : ModelDownload
{
    public FoundryLocalModelDownload(ModelDetails details)
        : base(details)
    {
    }

    public override void CancelDownload()
    {
        CancellationTokenSource.Cancel();
        DownloadStatus = DownloadStatus.Canceled;
    }

    public override async Task<bool> StartDownload()
    {
        DownloadStatus = DownloadStatus.InProgress;

        var internalProgress = new Progress<float>(p => DownloadProgress = p);

        try
        {
            var downloadResult = await FoundryLocalModelProvider.Instance.DownloadModel(
                Details, internalProgress, CancellationTokenSource.Token);

            if (downloadResult.Success)
            {
                DownloadStatus = DownloadStatus.Completed;
                WarningMessage = downloadResult.ErrorMessage; // May be null or contain warning
                return true;
            }

            DownloadStatus = DownloadStatus.Canceled;
            return false;
        }
        catch
        {
            DownloadStatus = DownloadStatus.Canceled;
            return false;
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<DownloadStatus>))]
internal enum DownloadStatus
{
    Waiting,
    InProgress,
    Verifying,
    Completed,
    Canceled,
    VerificationFailed
}

internal class ModelDownloadEventArgs
{
    public required float Progress { get; init; }
    public required DownloadStatus Status { get; init; }
    public string? VerificationFailureMessage { get; init; }
    public string? WarningMessage { get; init; }
}