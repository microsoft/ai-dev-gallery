// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models; // <exclude-line>
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events; // <exclude-line>
using AIDevGallery.Utils; // <exclude-line>
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AI;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;

namespace AIDevGallery.Controls;

internal sealed partial class WcrModelDownloader : UserControl
{
    public event EventHandler? DownloadClicked;
    private ModelType? modelTypeHint; // <exclude-line>
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

                // TODO: Remove after SDXL is released to retail
                WindowsInsiderErrorText.Visibility = (modelTypeHint != null && WcrApiHelpers.IsImageGeneratorBacked(modelTypeHint.Value))
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

    public async Task<bool> SetDownloadOperation(IAsyncOperationWithProgress<AIFeatureReadyResult, double> operation)
    {
        if (operation == null)
        {
            return false;
        }

        if (modelTypeHint != null)
        {
            WcrDownloadOperationTracker.Operations[modelTypeHint.Value] = operation; // <exclude-line>
        }

        operation.Progress = (result, progress) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadProgress = (int)(progress * 100);
            });
        };

        State = WcrApiDownloadState.Downloading;

        WcrDiagnosticsLogger.LogEnvironmentOnce(); // <exclude-line>
        WcrDiagnosticsLogger.LogSection($"EnsureReadyAsync for {modelTypeHint?.ToString() ?? "WCR API"}"); // <exclude-line>
        var diagnosticsStopwatch = System.Diagnostics.Stopwatch.StartNew(); // <exclude-line>

        try
        {
            var result = await operation;

            WcrDiagnosticsLogger.Log($"EnsureReadyAsync returned Status={result.Status} after {diagnosticsStopwatch.ElapsedMilliseconds} ms"); // <exclude-line>

            if (result.Status == AIFeatureReadyResultState.Success)
            {
                State = WcrApiDownloadState.Downloaded;
                if (modelTypeHint != null)
                {
                    WcrApiHelpers.IsModelReadyWorkaround[modelTypeHint.Value] = true;
                }

                return true;
            }
            else
            {
                State = WcrApiDownloadState.Error;
                ErrorMessage = result.ExtendedError != null
                    ? $"HRESULT 0x{result.ExtendedError.HResult:X8}{Environment.NewLine}{result.ExtendedError}"
                    : $"Model not ready (status: {result.Status})";
                var extendedError = result.ExtendedError; // <exclude-line>
                WcrDiagnosticsLogger.Log($"EnsureReadyAsync FAILED: Status={result.Status}, HResult=0x{extendedError?.HResult ?? 0:X8}, Message={extendedError?.Message}"); // <exclude-line>
                WcrDiagnosticsLogger.Log($"ExtendedError detail: {extendedError}"); // <exclude-line>
                if (modelTypeHint != null)
                {
                    WcrApiDownloadFailedEvent.Log(modelTypeHint.Value, extendedError?.Message ?? string.Empty); // <exclude-line>
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"HRESULT 0x{ex.HResult:X8}{Environment.NewLine}{ex}";
            State = WcrApiDownloadState.Error;
            WcrDiagnosticsLogger.Log($"EnsureReadyAsync THREW: HResult=0x{ex.HResult:X8} {ex}"); // <exclude-line>
            if (modelTypeHint != null)
            {
                WcrApiDownloadFailedEvent.Log(modelTypeHint.Value, ex); // <exclude-line>
            }
        }

        return false;
    }

    // <exclude>
    public Task<bool> SetDownloadOperation(ModelType modelType, string sampleId, Func<IAsyncOperationWithProgress<AIFeatureReadyResult, double>> makeAvailable)
    {
        IAsyncOperationWithProgress<AIFeatureReadyResult, double>? exisitingOperation;

        WcrDownloadOperationTracker.Operations.TryGetValue(modelType, out exisitingOperation);
        this.modelTypeHint = modelType;
        this.sampleId = sampleId;

        var (hardwareRequirement, supportedHardwareUri) = WcrApiHelpers.GetHardwareRequirementInfo(modelType);
        HardwareRequirementText.Text = hardwareRequirement;
        HardwareRequirementLink.NavigateUri = new Uri(supportedHardwareUri);
        ErrorHardwareRequirementText.Text = hardwareRequirement;
        ErrorHardwareRequirementLink.NavigateUri = new Uri(supportedHardwareUri);

        // TODO: Remove after SDXL is released to retail
        WindowsInsiderInfoText.Visibility = WcrApiHelpers.IsImageGeneratorBacked(modelType)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // TODO: Remove once the Speech Recognition ships through Windows Update
        var isSpeechRecognition = modelType == ModelType.SpeechRecognition;
        ModelDownloadInfoRun.Text = isSpeechRecognition
            ? "This Windows AI API requires a one-time model download."
            : "This Windows AI API requires a one-time model download via Windows Update.";
        WindowsUpdateTrackingText.Visibility = isSpeechRecognition
            ? Visibility.Collapsed
            : Visibility.Visible;

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
        if (modelTypeHint != null && sampleId != string.Empty)
        {
            WcrApiDownloadRequestedEvent.Log(modelTypeHint.Value, sampleId); // <exclude-line>
        }
    }

    private async void WindowsUpdateHyperlinkClicked(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        var uri = new Uri("ms-settings:windowsupdate");
        await Launcher.LaunchUriAsync(uri);
    }

    // <exclude>
    private void CopyDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(WcrDiagnosticsLogger.GetLogText());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy diagnostics: {ex.Message}");
        }
    }

    private async void OpenLogFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = WcrDiagnosticsLogger.LogFolderPath;
            if (!string.IsNullOrEmpty(path))
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
                await Launcher.LaunchFolderAsync(folder);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open log folder: {ex.Message}");
        }
    }

    // </exclude>
}

internal enum WcrApiDownloadState
{
    NotStarted,
    Downloading,
    Downloaded,
    Error
}