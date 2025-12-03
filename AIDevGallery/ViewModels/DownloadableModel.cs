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

    [ObservableProperty]
    public partial string? VerificationFailureMessage { get; set; }

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
            VerificationFailureMessage = _modelDownload.VerificationFailureMessage;
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

    /// <summary>
    /// Deletes the model files when user rejects a verification-failed model.
    /// </summary>
    public void DeleteVerificationFailedModel()
    {
        if (ModelDownload is OnnxModelDownload onnxDownload)
        {
            onnxDownload.DeleteFailedModel();
        }

        Status = DownloadStatus.Canceled;
        ModelDownload = null;
        VerificationFailureMessage = null;
    }

    /// <summary>
    /// Keeps the model despite verification failure (user's choice).
    /// </summary>
    public async void KeepVerificationFailedModel()
    {
        if (ModelDownload is OnnxModelDownload onnxDownload)
        {
            await onnxDownload.KeepModelDespiteVerificationFailure();
        }

        Status = DownloadStatus.Completed;
        ModelDownload = null;
        VerificationFailureMessage = null;
    }

    private void ModelDownload_StateChanged(object? sender, ModelDownloadEventArgs e)
    {
        if (!_progressTimer.IsEnabled)
        {
            _progressTimer.Start();
        }

        if (e.Progress == 1 && e.Status == DownloadStatus.InProgress)
        {
            // Download complete, but may still need verification
            return;
        }

        if (e.Status == DownloadStatus.Completed)
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

        if (e.Status == DownloadStatus.VerificationFailed)
        {
            Status = DownloadStatus.VerificationFailed;
            VerificationFailureMessage = e.VerificationFailureMessage;
        }

        if (e.Status == DownloadStatus.Verifying)
        {
            Status = DownloadStatus.Verifying;
        }
    }

    private void ProgressTimer_Tick(object? sender, object e)
    {
        _progressTimer.Stop();
        if (ModelDownload != null)
        {
            Progress = ModelDownload.DownloadProgress * 100;
            Status = ModelDownload.DownloadStatus;
            VerificationFailureMessage = ModelDownload.VerificationFailureMessage;
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
        return status is DownloadStatus.InProgress or DownloadStatus.Waiting or DownloadStatus.Verifying ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenCanceled(DownloadStatus status)
    {
        return status == DownloadStatus.Canceled ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenDownloaded(DownloadStatus status)
    {
        return status == DownloadStatus.Completed ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenVerificationFailed(DownloadStatus status)
    {
        return status == DownloadStatus.VerificationFailed ? Visibility.Visible : Visibility.Collapsed;
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
            case DownloadStatus.Verifying:
                return "Verifying integrity..";
            case DownloadStatus.Completed:
                return "Downloaded";
            case DownloadStatus.Canceled:
                return "Canceled";
            case DownloadStatus.VerificationFailed:
                return "Verification failed";
            default:
                return string.Empty;
        }
    }
}