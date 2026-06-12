// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AI.Speech;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Media.Transcoding;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Speech Recognition",
    Model1Types = [ModelType.SpeechRecognition],
    Scenario = ScenarioType.AudioAndVideoTranscribeLiveAudio,
    Id = "9c5b2e8a-1f7d-4d3c-9e6a-3b1c8e7f4d20",
    Icon = "\uE720")]
internal sealed partial class SpeechRecognition : BaseSamplePage
{
    private SpeechRecognitionModel? _speechModel;
    private StreamingRecognition? _streamingRecognition;
    private Task? _streamingSessionTask;
    private StreamingRecognition? _fileStreamingRecognition;
    private CancellationTokenSource? _fileStreamingCts;
    private MediaPlayer? _filePlaybackPlayer;
    private TaskCompletionSource<bool>? _filePlaybackCompletion;

    private string _finalText = string.Empty;
    private bool _isRecognizing;

    public SpeechRecognition()
    {
        this.Unloaded += (_, _) => CleanUp();
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            await catalog.EnsureAndRegisterCertifiedAsync();

            var readyState = SpeechRecognitionModel.GetReadyState();
            if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
            {
                if (readyState == AIFeatureReadyState.NotReady)
                {
                    var op = await SpeechRecognitionModel.EnsureReadyAsync();
                    if (op.Status != AIFeatureReadyResultState.Success)
                    {
                        ShowException(op.ExtendedError, "Speech Recognition is not available.");
                        return;
                    }
                }

                var modelResult = await SpeechRecognitionModel.TryCreateAsync();
                if (modelResult.ExtendedError != null)
                {
                    ShowException(modelResult.ExtendedError, "Failed to load the Speech Recognition model.");
                    return;
                }

                _speechModel = modelResult.SpeechModel;
            }
            else
            {
                var msg = readyState == AIFeatureReadyState.DisabledByUser
                    ? "Disabled by user."
                    : "Not supported on this system.";
                ShowException(null, $"Speech Recognition is not available: {msg}");
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load the Speech Recognition model.");
        }
        finally
        {
            sampleParams.NotifyCompletion();
        }
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecognizing)
        {
            await StopRecognitionAsync();
        }
        else
        {
            await StartRecognitionAsync();
        }
    }

    private async Task StartRecognitionAsync()
    {
        if (_speechModel == null)
        {
            ShowException(null, "Speech Recognition model is not loaded yet.");
            return;
        }

        SendSampleInteractedEvent("StartSpeechRecognition");
        StartStopButton.IsEnabled = false;

        try
        {
            if (!await EnsureMicrophoneAccessAsync())
            {
                return;
            }

            // Stream audio from the default microphone
            var audioConfig = AudioConfiguration.FromAudioDevice(string.Empty);

            _streamingRecognition = new StreamingRecognition(audioConfig, _speechModel);
            var session = _streamingRecognition;
            session.Recognizing += OnRecognizing;
            session.Recognized += OnRecognized;

            _finalText = string.Empty;
            FinalTranscriptionTextBlock.Text = string.Empty;
            InterimTranscriptionTextBlock.Text = string.Empty;
            _isRecognizing = true;
            UpdateUiState(running: true, status: "Listening on default microphone...");

            var sessionTask = session.StartContinuousRecognitionAsync().AsTask();
            _streamingSessionTask = sessionTask;
            _ = MonitorStreamingSessionAsync(session, sessionTask);
        }
        catch (Exception ex)
        {
            await StopRecognitionAsync();
            ShowException(ex, $"Failed to start speech recognition: {FormatError(ex)}");
        }
        finally
        {
            StartStopButton.IsEnabled = true;
        }
    }

    private async Task StopRecognitionAsync()
    {
        SendSampleInteractedEvent("StopSpeechRecognition");

        if (_fileStreamingRecognition is { } fileSession)
        {
            DetachHandlers(fileSession);
            _fileStreamingCts?.Cancel();
        }

        var streaming = _streamingRecognition;
        var sessionTask = _streamingSessionTask;
        _streamingRecognition = null;
        _streamingSessionTask = null;

        if (streaming != null)
        {
            try
            {
                // Await the Start operation after stopping so the on-disk model cache flushes before disposal.
                streaming.StopContinuousRecognition();

                if (sessionTask != null)
                {
                    // Faults were already surfaced by MonitorStreamingSessionAsync
                    await sessionTask.ContinueWith(static _ => { }, TaskScheduler.Default);
                }
            }
            catch (Exception ex)
            {
                ShowException(ex, "Failed to stop speech recognition cleanly.");
            }
            finally
            {
                DetachHandlers(streaming);
                streaming.Dispose();
            }
        }

        _isRecognizing = false;
        StopFilePlayback();
        UpdateUiState(running: false, status: null);
    }

    // Awaits the streaming session task on a background thread; if it faults (e.g., mic device
    // not found or wrong audio format), marshals the exception back to the UI thread and shows
    // it to the user so the failure is visible instead of silently swallowed.
    private async Task MonitorStreamingSessionAsync(StreamingRecognition session, Task sessionTask)
    {
        try
        {
            await sessionTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Ignore if the user already stopped or a different session is now active.
                if (_streamingRecognition == session)
                {
                    HandleStreamingFailure(session, ex);
                }
            });
        }
    }

    private void HandleStreamingFailure(StreamingRecognition session, Exception ex)
    {
        _streamingRecognition = null;
        _streamingSessionTask = null;
        _isRecognizing = false;

        DetachHandlers(session);
        session.Dispose();

        UpdateUiState(running: false, status: null);
        ShowException(ex, $"Speech recognition failed: {FormatError(ex)}");
    }

    private async void RecognizeFileBatch_Click(object sender, RoutedEventArgs e)
    {
        await RecognizeFromFileAsync(streamMode: false);
    }

    private async void RecognizeFileStreaming_Click(object sender, RoutedEventArgs e)
    {
        await RecognizeFromFileAsync(streamMode: true);
    }

    private async Task RecognizeFromFileAsync(bool streamMode)
    {
        if (_speechModel == null)
        {
            ShowException(null, "Speech Recognition model is not loaded yet.");
            return;
        }

        if (_isRecognizing)
        {
            await StopRecognitionAsync();
        }

        SendSampleInteractedEvent("RecognizeFromFile");

        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".m4a");

        var file = await picker.PickSingleFileAsync();
        if (file == null)
        {
            return;
        }

        StorageFile? transcodedFile = null;
        StreamingRecognition? fileStreaming = null;
        CancellationTokenSource? fileCts = null;
        try
        {
            UpdateUiState(running: true, status: $"Transcoding \"{file.Name}\" to 16 kHz mono...");
            FinalTranscriptionTextBlock.Text = $"Transcoding \"{file.Name}\" to 16 kHz mono...";
            InterimTranscriptionTextBlock.Text = string.Empty;
            _finalText = string.Empty;
            _isRecognizing = true;

            transcodedFile = await TranscodeTo16kMonoCanonicalWavAsync(file);

            if (streamMode)
            {
                UpdateUiState(running: true, status: $"Streaming recognition of \"{file.Name}\"...");
                FinalTranscriptionTextBlock.Text = string.Empty;

                fileStreaming = new StreamingRecognition(
                    AudioConfiguration.FromFile(transcodedFile.Path),
                    _speechModel);
                fileStreaming.Recognizing += OnRecognizing;
                fileStreaming.Recognized += OnRecognized;
                _fileStreamingRecognition = fileStreaming;
                fileCts = new CancellationTokenSource();
                _fileStreamingCts = fileCts;

                // Play the picked file so the transcript can be followed as it streams in,
                // rather than appearing in silence.
                var playbackTask = StartFilePlayback(file);

                var fileSessionTask = fileStreaming.StartContinuousRecognitionAsync().AsTask(fileCts.Token);

                try
                {
                    await fileSessionTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when the user presses Stop or navigates away mid-file.
                }

                if (_isRecognizing)
                {
                    if (string.IsNullOrWhiteSpace(_finalText))
                    {
                        FinalTranscriptionTextBlock.Text = "(no speech detected in file)";
                    }

                    await playbackTask;

                    UpdateUiState(running: false, status: $"Streaming recognition of \"{file.Name}\" completed.");
                }
            }
            else
            {
                // BatchRecognition returns the full transcript in a single call.
                UpdateUiState(running: true, status: $"Recognizing from \"{file.Name}\"...");
                FinalTranscriptionTextBlock.Text = $"Recognizing from file: {file.Name}...";

                using var batch = new BatchRecognition(_speechModel);
                var transcript = await batch.RecognizeFromFile(transcodedFile.Path);

                _finalText = transcript ?? string.Empty;
                FinalTranscriptionTextBlock.Text = string.IsNullOrWhiteSpace(_finalText)
                    ? "(no speech detected in file)"
                    : _finalText;
                UpdateUiState(running: false, status: $"Recognition of \"{file.Name}\" completed.");
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, $"Failed to recognize from \"{file.Name}\".\n\n{ex.Message}");
            UpdateUiState(running: false, status: null);
        }
        finally
        {
            _isRecognizing = false;
            StopFilePlayback();

            if (fileStreaming != null)
            {
                DetachHandlers(fileStreaming);
                fileStreaming.Dispose();
            }

            fileCts?.Dispose();

            // Clear shared state only if a newer run hasn't already replaced it.
            if (ReferenceEquals(_fileStreamingRecognition, fileStreaming))
            {
                _fileStreamingRecognition = null;
            }

            if (ReferenceEquals(_fileStreamingCts, fileCts))
            {
                _fileStreamingCts = null;
            }

            await TryDeleteAsync(transcodedFile);
        }
    }

    private Task StartFilePlayback(StorageFile file)
    {
        StopFilePlayback();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _filePlaybackCompletion = completion;

        try
        {
            var player = new MediaPlayer();
            player.MediaEnded += OnFilePlaybackEnded;
            player.MediaFailed += OnFilePlaybackFailed;
            player.Source = MediaSource.CreateFromStorageFile(file);
            _filePlaybackPlayer = player;
            player.Play();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpeechRecognition] Failed to start file playback: {ex.Message}");
            StopFilePlayback();
        }

        return completion.Task;
    }

    private void StopFilePlayback()
    {
        var player = _filePlaybackPlayer;
        _filePlaybackPlayer = null;

        // Unblock anyone awaiting playback completion (e.g. a Stop click before the clip ends).
        _filePlaybackCompletion?.TrySetResult(false);
        _filePlaybackCompletion = null;

        if (player == null)
        {
            return;
        }

        player.MediaEnded -= OnFilePlaybackEnded;
        player.MediaFailed -= OnFilePlaybackFailed;

        try
        {
            player.Pause();
            if (player.Source is IDisposable source)
            {
                player.Source = null;
                source.Dispose();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpeechRecognition] Failed to stop file playback: {ex.Message}");
        }

        player.Dispose();
    }

    private void OnFilePlaybackEnded(MediaPlayer sender, object args)
    {
        _filePlaybackCompletion?.TrySetResult(true);
    }

    private void OnFilePlaybackFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"[SpeechRecognition] File playback failed: {args.ErrorMessage}");
        _filePlaybackCompletion?.TrySetResult(false);
    }

    private static async Task<StorageFile> TranscodeTo16kMonoCanonicalWavAsync(StorageFile inputFile)
    {
        var mfFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
            $"speech-recognition-mf-{Guid.NewGuid():N}.wav",
            CreationCollisionOption.ReplaceExisting);
        var canonicalFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
            $"speech-recognition-{Guid.NewGuid():N}.wav",
            CreationCollisionOption.ReplaceExisting);

        try
        {
            var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
            profile.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16);

            var transcoder = new MediaTranscoder();
            var prepare = await transcoder.PrepareFileTranscodeAsync(inputFile, mfFile, profile);
            if (!prepare.CanTranscode)
            {
                throw new InvalidOperationException(
                    $"MediaTranscoder cannot transcode \"{inputFile.Name}\" to 16 kHz mono PCM WAV: {prepare.FailureReason}");
            }

            await prepare.TranscodeAsync();
            RewriteWavAsCanonicalPcm(mfFile.Path, canonicalFile.Path);
            return canonicalFile;
        }
        catch
        {
            await TryDeleteAsync(canonicalFile);
            throw;
        }
        finally
        {
            await TryDeleteAsync(mfFile);
        }
    }

    private static void RewriteWavAsCanonicalPcm(string sourcePath, string destPath)
    {
        var src = File.ReadAllBytes(sourcePath);
        if (src.Length < 12 || Encoding.ASCII.GetString(src, 0, 4) != "RIFF" || Encoding.ASCII.GetString(src, 8, 4) != "WAVE")
        {
            throw new InvalidOperationException("Source file is not a RIFF/WAVE.");
        }

        ushort audioFormat = 0, channels = 0, blockAlign = 0, bitsPerSample = 0;
        uint sampleRate = 0, byteRate = 0;
        int dataOffset = -1, dataSize = 0;

        int offset = 12;
        while (offset + 8 <= src.Length)
        {
            var chunkId = Encoding.ASCII.GetString(src, offset, 4);
            var chunkSize = (int)BitConverter.ToUInt32(src, offset + 4);

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                audioFormat = BitConverter.ToUInt16(src, offset + 8);
                channels = BitConverter.ToUInt16(src, offset + 10);
                sampleRate = BitConverter.ToUInt32(src, offset + 12);
                byteRate = BitConverter.ToUInt32(src, offset + 16);
                blockAlign = BitConverter.ToUInt16(src, offset + 20);
                bitsPerSample = BitConverter.ToUInt16(src, offset + 22);
            }
            else if (chunkId == "data")
            {
                dataOffset = offset + 8;
                dataSize = chunkSize;
                break;
            }

            offset += 8 + chunkSize;
            if ((chunkSize & 1) == 1)
            {
                offset += 1;
            }
        }

        if (audioFormat != 1)
        {
            throw new InvalidOperationException($"Source WAV is not WAVE_FORMAT_PCM (got 0x{audioFormat:X4}).");
        }

        if (dataOffset < 0 || dataSize <= 0)
        {
            throw new InvalidOperationException("Source WAV has no data chunk.");
        }

        if (bitsPerSample != 16)
        {
            throw new InvalidOperationException($"Source WAV is not 16-bit PCM (got {bitsPerSample}).");
        }

        const int CanonicalFmtSize = 16;
        int canonicalSize = 12 + 8 + CanonicalFmtSize + 8 + dataSize;
        var dst = new byte[canonicalSize];

        Encoding.ASCII.GetBytes("RIFF").CopyTo(dst, 0);
        BitConverter.GetBytes((uint)(canonicalSize - 8)).CopyTo(dst, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(dst, 8);

        Encoding.ASCII.GetBytes("fmt ").CopyTo(dst, 12);
        BitConverter.GetBytes((uint)CanonicalFmtSize).CopyTo(dst, 16);
        BitConverter.GetBytes((ushort)1).CopyTo(dst, 20);
        BitConverter.GetBytes(channels).CopyTo(dst, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(dst, 24);
        BitConverter.GetBytes(byteRate).CopyTo(dst, 28);
        BitConverter.GetBytes(blockAlign).CopyTo(dst, 32);
        BitConverter.GetBytes(bitsPerSample).CopyTo(dst, 34);

        Encoding.ASCII.GetBytes("data").CopyTo(dst, 36);
        BitConverter.GetBytes((uint)dataSize).CopyTo(dst, 40);

        Buffer.BlockCopy(src, dataOffset, dst, 44, dataSize);
        File.WriteAllBytes(destPath, dst);
    }

    private async Task<bool> EnsureMicrophoneAccessAsync()
    {
        try
        {
#pragma warning disable CA1416
            var capability = AppCapability.Create("microphone");
            if (capability != null)
            {
                var status = capability.CheckAccess();
                if (status != AppCapabilityAccessStatus.Allowed)
                {
                    status = await capability.RequestAccessAsync();
                }

                if (status != AppCapabilityAccessStatus.Allowed)
                {
                    await ShowMicrophoneAccessDeniedDialogAsync();
                    return false;
                }
            }
#pragma warning restore CA1416
        }
        catch (UnauthorizedAccessException)
        {
            await ShowMicrophoneAccessDeniedDialogAsync();
            return false;
        }

        return true;
    }

    private async Task ShowMicrophoneAccessDeniedDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Microphone access required",
            Content = "Speech recognition needs permission to use the microphone. " +
                "Open Windows Settings, enable \u201CLet apps access your microphone\u201D, " +
                "and allow access for AI Dev Gallery.",
            PrimaryButtonText = "Open Settings",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-microphone"));
        }
    }

    private void OnRecognizing(StreamingRecognition sender, StreamingRecognizingEventArgs args)
    {
        var partial = args.Text ?? string.Empty;
        DispatcherQueue.TryEnqueue(() =>
        {
            InterimTranscriptionTextBlock.Text = partial;
            ScrollToEnd();
        });
    }

    private void OnRecognized(StreamingRecognition sender, StreamingRecognizedEventArgs args)
    {
        var text = args.Text ?? string.Empty;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (_finalText.Length > 0 && !_finalText.EndsWith(' '))
                {
                    _finalText += " ";
                }

                _finalText += text;
                FinalTranscriptionTextBlock.Text = _finalText;
            }

            InterimTranscriptionTextBlock.Text = string.Empty;
            ScrollToEnd();
        });
    }

    private void ScrollToEnd()
    {
        TranscriptionScrollViewer.UpdateLayout();
        TranscriptionScrollViewer.ChangeView(null, TranscriptionScrollViewer.ScrollableHeight, null, disableAnimation: true);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _finalText = string.Empty;
        FinalTranscriptionTextBlock.Text = string.Empty;
        InterimTranscriptionTextBlock.Text = string.Empty;
    }

    private void UpdateUiState(bool running, string? status)
    {
        StartStopButton.Content = running ? "Stop recognition" : "Start recognition";
        FromFileButton.IsEnabled = !running;

        if (string.IsNullOrEmpty(status))
        {
            StatusInfoBar.IsOpen = false;
            StatusInfoBar.Title = string.Empty;
            StatusInfoBar.Message = string.Empty;
        }
        else
        {
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.Title = running ? "Listening" : string.Empty;
            StatusInfoBar.Message = status;
            StatusInfoBar.IsOpen = true;
        }
    }

    private void DetachHandlers(StreamingRecognition session)
    {
        session.Recognizing -= OnRecognizing;
        session.Recognized -= OnRecognized;
    }

    private static string FormatError(Exception ex)
    {
        var hresult = ((uint)ex.HResult).ToString("X8", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(ex.Message) ? $"HRESULT 0x{hresult}" : $"{ex.Message} (HRESULT 0x{hresult})";
    }

    private static async Task TryDeleteAsync(StorageFile? file)
    {
        if (file == null)
        {
            return;
        }

        try
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpeechRecognition] Failed to delete temporary file: {ex.Message}");
        }
    }

    private void CleanUp()
    {
        StopFilePlayback();

        // Cancel any in-flight file-streaming recognition so it doesn't keep running after navigation.
        _fileStreamingCts?.Cancel();

        var streaming = _streamingRecognition;
        var sessionTask = _streamingSessionTask;
        var model = _speechModel;

        _streamingRecognition = null;
        _streamingSessionTask = null;
        _speechModel = null;
        _isRecognizing = false;

        if (streaming == null && model == null)
        {
            return;
        }

        // Tear down off the UI thread (a synchronous wait would deadlock the DispatcherQueue), stopping
        // and awaiting the session before disposal to avoid corrupting the on-disk model cache.
        _ = Task.Run(async () =>
        {
            if (streaming != null)
            {
                try
                {
                    DetachHandlers(streaming);
                    streaming.StopContinuousRecognition();

                    if (sessionTask != null)
                    {
                        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5))
                            .ContinueWith(static _ => { }, TaskScheduler.Default)
                            .ConfigureAwait(false);
                    }

                    streaming.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SpeechRecognition] Streaming cleanup threw: {ex.Message}");
                }
            }

            try
            {
                model?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeechRecognition] Model cleanup threw: {ex.Message}");
            }
        });
    }
}