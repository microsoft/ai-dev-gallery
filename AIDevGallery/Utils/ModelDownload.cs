// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
            DownloadStatus = DownloadStatus.Canceled;

            var localPath = ModelUrl.GetLocalPath(App.AppData.ModelCachePath);
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
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

    private async Task<CachedModel> DownloadModel(string cacheDir, IProgress<float>? progress = null)
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

        long modelSize = filesToDownload.Sum(f => f.Size);
        long bytesDownloaded = 0;

        var internalProgress = new Progress<long>(p =>
        {
            var percentage = (float)(bytesDownloaded + p) / (float)modelSize;
            progress?.Report(percentage);
        });

        using var client = new HttpClient();

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
                // file did not download properly, should retry
            }

            bytesDownloaded += downloadableFile.Size;
        }

        var modelDirectory = url.GetLocalPath(cacheDir);

        return new CachedModel(Details, url.IsFile ? $"{modelDirectory}\\{filesToDownload.First().Name}" : modelDirectory, url.IsFile, modelSize);
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

        Progress<float> internalProgress = new(p =>
        {
            DownloadProgress = p;
        });

        bool result = false;

        try
        {
            result = await FoundryLocalModelProvider.Instance.DownloadModel(Details.Name, internalProgress, CancellationTokenSource.Token);
        }
        catch
        {
        }

        if (result)
        {
            DownloadStatus = DownloadStatus.Completed;
            return true;
        }
        else
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
    Completed,
    Canceled
}

internal class ModelDownloadEventArgs
{
    public required float Progress { get; init; }
    public required DownloadStatus Status { get; init; }
}