// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System;
using System.Text.Json.Serialization;
using System.Threading;

namespace AIDevGallery.Utils;

internal class ModelDownload : IDisposable
{
    public ModelDetails Details { get; set; }
    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.Waiting;
    public float DownloadProgress { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

    public ModelUrl ModelUrl { get; set; }

    public void Dispose()
    {
        CancellationTokenSource.Dispose();
    }

    public ModelDownload(ModelDetails details)
    {
        Details = details;
        if (details.Url.StartsWith("https://github.com", StringComparison.InvariantCulture))
        {
            ModelUrl = new GitHubUrl(details.Url);
        }
        else
        {
            ModelUrl = new HuggingFaceUrl(details.Url);
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