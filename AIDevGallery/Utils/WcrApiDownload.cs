// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Management.Deployment;
using Microsoft.Windows.Vision;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace AIDevGallery.Utils;
internal class WcrApiDownload
{
    private static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>>> MakeAvailables = new Dictionary<ModelType, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>>>
    {
        {
            ModelType.PhiSilica, LanguageModel.MakeAvailableAsync
        },
        {
            ModelType.TextRecognitionOCR, TextRecognizer.MakeAvailableAsync
        },
        {
            ModelType.ImageScaler, ImageScaler.MakeAvailableAsync
        },
        {
            ModelType.BackgroundRemover, ImageObjectExtractor.MakeAvailableAsync
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.MakeAvailableAsync
        }
    };

    private static Dictionary<string, WcrApiDownload> _downloads = new();

    public event EventHandler<WcrApiDownloadProgressEventArgs>? DownloadProgressChanged;

    public IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>? DownloadOperation { get; private set; }
    public ModelDetails Details { get; set; }
    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.NotStarted;
    public double DownloadProgress { get; set; }

    private ModelType modelType;

    public static WcrApiDownload? GetWcrApiDownload(ModelDetails details)
    {
        if (!_downloads.TryGetValue(details.Id, out WcrApiDownload? value))
        {
            if (!details.Url.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var typeStr = details.Url.Substring(7);
            if (!Enum.TryParse(typeStr, out ModelType type))
            {
                return null;
            }

            if (!MakeAvailables.ContainsKey(type))
            {
                return null;
            }

            value = new WcrApiDownload(details, type);
            _downloads[details.Id] = value;
        }

        return value;
    }

    private WcrApiDownload(ModelDetails details, ModelType type)
    {
        Details = details;
        modelType = type;
    }

    public IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress> MakeAvailableAsync()
    {
        if (DownloadOperation != null)
        {
            return DownloadOperation;
        }

        if (!MakeAvailables.TryGetValue(modelType, out var makeAvailable) || makeAvailable == null)
        {
            throw new NotSupportedException();
        }

        DownloadOperation = makeAvailable();

        DownloadStatus = DownloadStatus.Queued;
        DownloadOperation.Progress = DownloadOperation_Progress;

        return DownloadOperation;
    }

    private void DownloadOperation_Progress(IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress> asyncInfo, PackageDeploymentProgress progressInfo)
    {
        switch (progressInfo.Status)
        {
            case PackageDeploymentProgressStatus.Queued:
                DownloadStatus = DownloadStatus.Queued;
                DownloadProgress = 0;
                break;
            case PackageDeploymentProgressStatus.InProgress:
                DownloadStatus = DownloadStatus.InProgress;
                DownloadProgress = progressInfo.Progress;
                break;
            case PackageDeploymentProgressStatus.CompletedSuccess:
                DownloadStatus = DownloadStatus.Completed;
                DownloadProgress = 1;
                break;
            case PackageDeploymentProgressStatus.CompletedFailure:
                DownloadStatus = DownloadStatus.Canceled;
                DownloadProgress = 0;
                break;
        }

        DownloadProgressChanged?.Invoke(this, new WcrApiDownloadProgressEventArgs
        {
            Details = Details,
            ApiType = modelType,
            Progress = DownloadProgress,
            Status = DownloadStatus
        });
    }
}

internal class WcrApiDownloadProgressEventArgs
{
    public required ModelDetails Details { get; init; }
    public required ModelType ApiType { get; set; }
    public required double Progress { get; init; }
    public required DownloadStatus Status { get; set; }
}