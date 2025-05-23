// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class ModelDownloadQueue()
{
    private readonly List<ModelDownload> _queue = [];
    public event EventHandler<ModelDownloadCompletedEventArgs>? ModelDownloadCompleted;

    public delegate void ModelsChangedHandler(ModelDownloadQueue sender);
    public event ModelsChangedHandler? ModelsChanged;

    private Task? processingTask;

    public ModelDownload? AddModel(ModelDetails modelDetails)
    {
        if (App.ModelCache.IsModelCached(modelDetails.Url))
        {
            return null;
        }

        var existingDownload = GetDownload(modelDetails.Url);
        if (existingDownload != null)
        {
            return existingDownload;
        }

        var download = EnqueueModelDownload(modelDetails);
        return download;
    }

    private ModelDownload EnqueueModelDownload(ModelDetails modelDetails)
    {
        var url = UrlHelpers.GetFullUrl(modelDetails.Url);

        ModelDownload modelDownload;

        if (modelDetails.Url.StartsWith("fl:", StringComparison.InvariantCultureIgnoreCase))
        {
            modelDownload = new FoundryLocalModelDownload(modelDetails);
        }
        else
        {
            modelDownload = new OnnxModelDownload(modelDetails);
        }

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
            download.CancelDownload();
        }

        ModelDownloadCancelEvent.Log(download.Details.Url);
        _queue.Remove(download);
        ModelsChanged?.Invoke(this);
        download.Dispose();
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
                    await Download(download);
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

    private async Task Download(ModelDownload modelDownload)
    {
        ModelDownloadStartEvent.Log(modelDownload.Details.Url);

        bool success = await modelDownload.StartDownload();

        if (success)
        {
            ModelDownloadCompleteEvent.Log(modelDownload.Details.Url);
            ModelDownloadCompleted?.Invoke(this, new ModelDownloadCompletedEventArgs());
            SendNotification(modelDownload.Details);
        }
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

internal class ModelDownloadCompletedEventArgs
{
}