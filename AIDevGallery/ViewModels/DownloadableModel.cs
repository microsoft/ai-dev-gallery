// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;

namespace AIDevGallery.ViewModels;

internal partial class DownloadableModel : BaseModel
{
    private readonly DispatcherTimer _progressTimer;

    [ObservableProperty]
    public partial float Progress { get; set; }

    [ObservableProperty]
    public partial bool CanDownload { get; set; }

    [ObservableProperty]
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Waiting;

    public bool IsDownloadEnabled => Compatibility.CompatibilityState != ModelCompatibilityState.NotCompatible;

    private ModelDownload? _modelDownload;

    public ModelDownload? ModelDownload
    {
        get => _modelDownload;
        set
        {
            if (value != null && value == _modelDownload)
            {
                return;
            }

            if (_modelDownload != null)
            {
                _modelDownload.StateChanged -= ModelDownload_StateChanged;
            }

            _modelDownload = value;

            if (_modelDownload == null)
            {
                CanDownload = true;
                return;
            }

            _modelDownload.StateChanged += ModelDownload_StateChanged;
            Status = _modelDownload.DownloadStatus;
            Progress = _modelDownload.DownloadProgress;
            CanDownload = false;
        }
    }

    private DownloadableModel(ModelDetails modelDetails, ModelDownload? modelDownload)
        : base(modelDetails)
    {
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _progressTimer.Tick += ProgressTimer_Tick;
        ModelDownload = modelDownload;
    }

    public DownloadableModel(ModelDetails modelDetails)
        : this(modelDetails, App.ModelDownloadQueue.GetDownload(modelDetails.Url))
    {
    }

    public DownloadableModel(ModelDownload download)
        : this(download.Details, download)
    {
    }

    public void StartDownload()
    {
        ModelDownload ??= App.ModelDownloadQueue.AddModel(ModelDetails);
    }

    public void CancelDownload()
    {
        ModelDownload?.CancelDownload();
    }

    private void ModelDownload_StateChanged(object? sender, ModelDownloadEventArgs e)
    {
        if (!_progressTimer.IsEnabled)
        {
            _progressTimer.Start();
        }

        if (e.Progress == 1)
        {
            Status = DownloadStatus.Completed;
            ModelDownload = null;
        }

        if (e.Status == DownloadStatus.Canceled)
        {
            Status = DownloadStatus.Canceled;
            ModelDownload = null;
            Progress = 0;
        }
    }

    private void ProgressTimer_Tick(object? sender, object e)
    {
        _progressTimer.Stop();
        if (ModelDownload != null)
        {
            Progress = ModelDownload.DownloadProgress * 100;
            Status = ModelDownload.DownloadStatus;
        }
    }

    public static Visibility DownloadStatusProgressVisibility(DownloadStatus status)
    {
        if (status is DownloadStatus.InProgress or DownloadStatus.Waiting)
        {
            return Visibility.Visible;
        }
        else
        {
            return Visibility.Collapsed;
        }
    }

    public static Visibility DownloadStatusButtonVisibility(ModelDownload download)
    {
        if (download == null)
        {
            return Visibility.Visible;
        }
        else
        {
            return Visibility.Collapsed;
        }
    }

    public static Visibility VisibleWhenDownloading(DownloadStatus status)
    {
        return status is DownloadStatus.InProgress or DownloadStatus.Waiting ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenCanceled(DownloadStatus status)
    {
        return status == DownloadStatus.Canceled ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenDownloaded(DownloadStatus status)
    {
        return status == DownloadStatus.Completed ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility BoolToVisibilityInverse(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Visibility VisibleWhenCompatible(ModelCompatibilityState compatibility)
    {
        return compatibility != ModelCompatibilityState.Compatible ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Visibility VisibleWhenCompatibilityIssue(ModelCompatibilityState compatibility)
    {
        return compatibility == ModelCompatibilityState.Compatible ? Visibility.Collapsed : Visibility.Visible;
    }

    public static string StatusToText(DownloadStatus status)
    {
        switch (status)
        {
            case DownloadStatus.Waiting:
                return "Waiting..";
            case DownloadStatus.InProgress:
                return "Downloading..";
            case DownloadStatus.Completed:
                return "Downloaded";
            case DownloadStatus.Canceled:
                return "Canceled";
            default:
                return string.Empty;
        }
    }
}