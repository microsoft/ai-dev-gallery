// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class ModelDownloadQueue(string cacheDir)
{
    private readonly List<ModelDownload> _queue = [];
    public event EventHandler<ModelDownloadProgressEventArgs>? ModelDownloadProgressChanged;
    public event EventHandler<ModelDownloadCompletedEventArgs>? ModelDownloadCompleted;

    public delegate void ModelsChangedHandler(ModelDownloadQueue sender);
    public event ModelsChangedHandler? ModelsChanged;

    public string CacheDir { get; set; } = cacheDir;

    private Task? processingTask;

    public ModelDownload EnqueueModelDownload(ModelDetails modelDetails)
    {
        var url = UrlHelpers.GetFullUrl(modelDetails.Url);

        var modelDownload = new ModelDownload(modelDetails);

        _queue.Add(modelDownload);
        ModelDownloadEnqueueEvent.Log(modelDetails.Url);
        ModelsChanged?.Invoke(this);

        lock (this)
        {
            if (processingTask == null || processingTask.IsFaulted)
            {
                processingTask = Task.Run(ProcessDownloads);
            }
        }

        return modelDownload;
    }

    public void CancelModelDownload(string url)
    {
        var download = GetDownload(url);
        if (download != null)
        {
            CancelModelDownload(download);
        }
    }

    public void CancelModelDownload(ModelDownload download)
    {
        if (download.DownloadStatus != DownloadStatus.Canceled)
        {
            download.CancellationTokenSource.Cancel();
            download.DownloadStatus = DownloadStatus.Canceled;
        }

        ModelDownloadCancelEvent.Log(download.Details.Url);
        _queue.Remove(download);
        ModelsChanged?.Invoke(this);
        download.Dispose();
        OnModelDownloadProgressChanged(download, 0, DownloadStatus.Canceled);
    }

    public IReadOnlyList<ModelDownload> GetDownloads()
    {
        return _queue.AsReadOnly();
    }

    public ModelDownload? GetDownload(string url)
    {
        url = UrlHelpers.GetFullUrl(url);
        return _queue.FirstOrDefault(d => UrlHelpers.GetFullUrl(d.Details.Url) == url);
    }

    private async Task ProcessDownloads()
    {
        while (_queue.Count > 0)
        {
            var download = _queue[0];
            TaskCompletionSource<bool> tcs = new();
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await Download(download, download.CancellationTokenSource.Token);
                    _queue.Remove(download);
                    ModelsChanged?.Invoke(this);
                    download.Dispose();
                    tcs.SetResult(true);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            await tcs.Task;
        }

        processingTask = null;
    }

    private async Task Download(ModelDownload modelDownload, CancellationToken cancellationToken)
    {
        modelDownload.DownloadStatus = DownloadStatus.InProgress;

        Progress<float> progress = new(p =>
        {
            modelDownload.DownloadProgress = p;
            OnModelDownloadProgressChanged(modelDownload, p, DownloadStatus.InProgress);
        });
        ModelDownloadStartEvent.Log(modelDownload.Details.Url);
        CachedModel cachedModel;
        try
        {
            cachedModel = await DownloadModel(modelDownload.Details, CacheDir, progress, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                modelDownload.DownloadStatus = DownloadStatus.Canceled;
                var localPath = modelDownload.ModelUrl.GetLocalPath(CacheDir);
                if (Directory.Exists(localPath))
                {
                    Directory.Delete(localPath, true);
                }

                return;
            }
        }
        catch (Exception e)
        {
            modelDownload.DownloadStatus = DownloadStatus.Canceled;
            var localPath = modelDownload.ModelUrl.GetLocalPath(CacheDir);
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }

            ModelDownloadFailedEvent.Log(modelDownload.Details.Url, e);
            return;
        }

        modelDownload.DownloadStatus = DownloadStatus.Completed;

        ModelDownloadCompleteEvent.Log(cachedModel.Url);
        ModelDownloadCompleted?.Invoke(this, new ModelDownloadCompletedEventArgs
        {
            CachedModel = cachedModel
        });
        OnModelDownloadProgressChanged(modelDownload, 1, DownloadStatus.Completed);
        SendNotification(modelDownload.Details);
    }

    private void OnModelDownloadProgressChanged(ModelDownload modelDownload, float p, DownloadStatus downloadStatus)
    {
        ModelDownloadProgressChanged?.Invoke(this, new ModelDownloadProgressEventArgs
        {
            ModelUrl = modelDownload.Details.Url,
            Progress = p,
            Status = downloadStatus
        });
    }

    public static async Task<CachedModel> DownloadModel(ModelDetails model, string cacheDir, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        ModelUrl url;
        List<ModelFileDetails> filesToDownload;
        if (model.Url.StartsWith("https://github.com", StringComparison.InvariantCulture))
        {
            var ghUrl = new GitHubUrl(model.Url);
            filesToDownload = await ModelInformationHelper.GetDownloadFilesFromGitHub(ghUrl, cancellationToken);
            url = ghUrl;
        }
        else
        {
            var hfUrl = new HuggingFaceUrl(model.Url);
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

        filesToDownload = ModelInformationHelper.FilterFiles(filesToDownload, model.FileFilters);

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
        return new CachedModel(model, url.IsFile ? $"{modelDirectory}\\{filesToDownload.First().Name}" : modelDirectory, url.IsFile, modelSize);
    }

    private static void SendNotification(ModelDetails model)
    {
        var builder = new AppNotificationBuilder()
                        .AddText(model.Name + " is ready to use.")
                        .AddButton(new AppNotificationButton("Try it out")
                        .AddArgument("model", model.Id));

        var notificationManager = AppNotificationManager.Default;
        notificationManager.Show(builder.BuildNotification());
    }
}

internal class ModelDownloadProgressEventArgs
{
    public required string ModelUrl { get; init; }
    public required float Progress { get; init; }
    public required DownloadStatus Status { get; init; }
}

internal class ModelDownloadCompletedEventArgs
{
    public required CachedModel CachedModel { get; init; }
}