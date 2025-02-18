// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models; // <exclude-line>
using AIDevGallery.Utils; // <exclude-line>
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace AIDevGallery.Samples.SharedCode;
internal sealed partial class WcrModelDownloader : UserControl
{
    public event EventHandler? DownloadClicked;
    private ModelType? modelTypeHint; // <exclude-line>

    public int DownloadProgress
    {
        get { return (int)GetValue(DownloadProgressProperty); }
        set { SetValue(DownloadProgressProperty, value); }
    }

    // Using a DependencyProperty as the backing store for DownloadProgress.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty DownloadProgressProperty =
        DependencyProperty.Register("DownloadProgress", typeof(int), typeof(WcrModelDownloader), new PropertyMetadata(0));

    public string ErrorMessage
    {
        get { return (string)GetValue(ErrorMessageProperty); }
        set { SetValue(ErrorMessageProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ErrorMessage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register("ErrorMessage", typeof(string), typeof(WcrModelDownloader), new PropertyMetadata("Error downloading model"));

    public WcrApiDownloadState State
    {
        get { return (WcrApiDownloadState)GetValue(StateProperty); }
        set { SetValue(StateProperty, value); }
    }

    // Using a DependencyProperty as the backing store for State.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register("State", typeof(WcrApiDownloadState), typeof(WcrModelDownloader), new PropertyMetadata(WcrApiDownloadState.Downloaded, OnStateChanged));

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((WcrModelDownloader)d).UpdateState((WcrApiDownloadState)e.NewValue);
    }

    private void UpdateState(WcrApiDownloadState state)
    {
        switch (state)
        {
            case WcrApiDownloadState.NotStarted:
                NotDownloadedContent.Visibility = Visibility.Visible;
                loadingRingContainer.Visibility = Visibility.Collapsed;
                errorContent.Visibility = Visibility.Collapsed;
                this.Visibility = Visibility.Visible;
                break;
            case WcrApiDownloadState.Downloading:
                NotDownloadedContent.Visibility = Visibility.Collapsed;
                loadingRingContainer.Visibility = Visibility.Visible;
                errorContent.Visibility = Visibility.Collapsed;
                this.Visibility = Visibility.Visible;
                break;
            case WcrApiDownloadState.Downloaded:
                NotDownloadedContent.Visibility = Visibility.Collapsed;
                loadingRingContainer.Visibility = Visibility.Collapsed;
                errorContent.Visibility = Visibility.Collapsed;
                this.Visibility = Visibility.Collapsed;
                break;
            case WcrApiDownloadState.Error:
                NotDownloadedContent.Visibility = Visibility.Collapsed;
                loadingRingContainer.Visibility = Visibility.Collapsed;
                errorContent.Visibility = Visibility.Visible;
                this.Visibility = Visibility.Visible;
                break;
            default:
                break;
        }
    }

    public WcrModelDownloader()
    {
        this.InitializeComponent();
    }

    public async Task<bool> SetDownloadOperation(IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress> operation)
    {
        if (operation == null)
        {
            return false;
        }

        // <exclude>
        if (this.modelTypeHint.HasValue)
        {
            WcrDownloadOperationTracker.Operations[this.modelTypeHint.Value] = operation;
        }

        // </exclude>
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
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = WcrApiDownloadState.Error;
        }

        return false;
    }

    // <exclude>
    public Task<bool> SetDownloadOperation(ModelType modelType, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>> makeAvailable)
    {
        IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>? exisitingOperation;

        WcrDownloadOperationTracker.Operations.TryGetValue(modelType, out exisitingOperation);
        this.modelTypeHint = modelType;

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