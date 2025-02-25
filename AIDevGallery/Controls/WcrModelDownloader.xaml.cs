// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models; // <exclude-line>
using AIDevGallery.Telemetry.Events; // <exclude-line>
using AIDevGallery.Utils; // <exclude-line>
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace AIDevGallery.Controls;
internal sealed partial class WcrModelDownloader : UserControl
{
    public event EventHandler? DownloadClicked;
    private ModelType modelTypeHint; // <exclude-line>
    private string sampleId = string.Empty; // <exclude-line>

    public int DownloadProgress
    {
        get { return (int)GetValue(DownloadProgressProperty); }
        set { SetValue(DownloadProgressProperty, value); }
    }

    // Using a DependencyProperty as the backing store for DownloadProgress. This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DownloadProgressProperty =
        DependencyProperty.Register(nameof(DownloadProgress), typeof(int), typeof(WcrModelDownloader), new PropertyMetadata(0));

    public string ErrorMessage
    {
        get { return (string)GetValue(ErrorMessageProperty); }
        set { SetValue(ErrorMessageProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ErrorMessage. This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(WcrModelDownloader), new PropertyMetadata("Error downloading model"));

    public WcrApiDownloadState State
    {
        get { return (WcrApiDownloadState)GetValue(StateProperty); }
        set { SetValue(StateProperty, value); }
    }

    // Using a DependencyProperty as the backing store for State. This enables animation, styling, binding, etc...
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(WcrApiDownloadState), typeof(WcrModelDownloader), new PropertyMetadata(WcrApiDownloadState.Downloaded, OnStateChanged));

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WcrModelDownloader)d).UpdateState((WcrApiDownloadState)e.NewValue);
    }

    private void UpdateState(WcrApiDownloadState state = WcrApiDownloadState.Downloaded)
    {
        switch (state)
        {
            case WcrApiDownloadState.NotStarted:
                VisualStateManager.GoToState(this, "NotDownloaded", true);
                this.Visibility = Visibility.Visible;
                break;
            case WcrApiDownloadState.Downloading:
                VisualStateManager.GoToState(this, "Downloading", true);
                this.Visibility = Visibility.Visible;
                break;
            case WcrApiDownloadState.Downloaded:
                VisualStateManager.GoToState(this, "Downloaded", true);
                this.Visibility = Visibility.Collapsed;
                break;
            case WcrApiDownloadState.Error:
                VisualStateManager.GoToState(this, "Error", true);
                this.Visibility = Visibility.Visible;
                break;
            default:
                break;
        }
    }

    public WcrModelDownloader()
    {
        this.InitializeComponent();
        UpdateState();
    }

    public async Task<bool> SetDownloadOperation(IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress> operation)
    {
        if (operation == null)
        {
            return false;
        }

        WcrDownloadOperationTracker.Operations[this.modelTypeHint] = operation; // <exclude-line>
        operation.Progress = (result, progress) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadProgress = (int)(progress.Progress * 100);
            });
        };

        State = WcrApiDownloadState.Downloading;

        try
        {
            var result = await operation;

            if (result.Status == PackageDeploymentStatus.CompletedSuccess)
            {
                State = WcrApiDownloadState.Downloaded;
                return true;
            }
            else
            {
                State = WcrApiDownloadState.Error;
                ErrorMessage = result.ExtendedError.Message;
                WcrApiDownloadFailedEvent.Log(this.modelTypeHint, result.ExtendedError.Message); // <exclude-line>
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = WcrApiDownloadState.Error;
            WcrApiDownloadFailedEvent.Log(this.modelTypeHint, ex); // <exclude-line>
        }

        return false;
    }

    // <exclude>
    public Task<bool> SetDownloadOperation(ModelType modelType, string sampleId, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>> makeAvailable)
    {
        IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>? exisitingOperation;

        WcrDownloadOperationTracker.Operations.TryGetValue(modelType, out exisitingOperation);
        this.modelTypeHint = modelType;
        this.sampleId = sampleId;

        if (exisitingOperation != null && exisitingOperation.Status == AsyncStatus.Started)
        {
            // don't reuse same one because we can only have one Progress delegate
            return SetDownloadOperation(makeAvailable());
        }

        return Task.FromResult(false);
    }

    // </exclude>
    private void DownloadModelClicked(object sender, RoutedEventArgs e)
    {
        DownloadClicked?.Invoke(this, EventArgs.Empty);
        WcrApiDownloadRequestedEvent.Log(modelTypeHint, sampleId); // <exclude-line>
    }

    private async void WindowsUpdateHyperlinkClicked(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        var uri = new Uri("ms-settings:windowsupdate");
        await Launcher.LaunchUriAsync(uri);
    }

    private string ToFirstLine(string text)
    {
        return text.Split(new[] { Environment.NewLine }, StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
    }
}

internal enum WcrApiDownloadState
{
    NotStarted,
    Downloading,
    Downloaded,
    Error
}