// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using NAudio.Wave;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace AIDevGallery.Samples.OpenSourceModels.Whisper;

[GallerySample(
    Name = "Whisper Audio Transcription",
    Model1Types = [ModelType.Whisper],
    Scenario = ScenarioType.AudioAndVideoTranscribeAudio,
    SharedCode = [
        SharedCodeEnum.WhisperWrapper
    ],
    NugetPackageReferences = [
        "NAudio.WinMM",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Id = "c7e248af-86e8-49ba-9f44-022230963261",
    Icon = "\uE8D4")]
internal sealed partial class WhisperAudioTranscription : BaseSamplePage
{
    private WaveInEvent? waveIn;
    private MemoryStream? audioStream;
    private WhisperWrapper whisper = null!;
    private System.Timers.Timer? recordingTimer;
    private bool isRecording;

    private CancellationTokenSource cts = new();

    public WhisperAudioTranscription()
    {
        this.Unloaded += (s, e) => DisposeMemory();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();

        PopulateLanguageComboBoxes();
        DataContext = this;
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        whisper = await WhisperWrapper.CreateAsync(sampleParams.ModelPath, sampleParams.PreferedEP);
        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        StartStopButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private void PopulateLanguageComboBoxes()
    {
        foreach (var language in WhisperWrapper.LanguageCodes.Keys)
        {
            SourceLanguageComboBox.Items.Add(language);
        }

        SourceLanguageComboBox.SelectedIndex = 0;
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        StartStopButton.IsEnabled = false;
        isRecording = !isRecording;

        if (isRecording)
        {
            TranscriptionTextBlock.Text = "Recording...";
            StartRecording();
            StartStopButton.Content = "Stop Recording";
        }
        else
        {
            StartStopButton.Content = "Processing...";
            StopRecording();
            TranscriptionTextBlock.Text = "Processing...";
            await ProcessAudio();
            StartStopButton.Content = "Start Recording";
        }

        StartStopButton.IsEnabled = true;
    }

    private void StartRecording()
    {
        audioStream = new MemoryStream();
        waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1)
        };
        waveIn.DataAvailable += (s, a) =>
        {
            audioStream.Write(a.Buffer, 0, a.BytesRecorded);
        };
        waveIn.StartRecording();

        recordingTimer = new System.Timers.Timer(29999);
        recordingTimer.Elapsed += OnRecordingTimerElapsed;
        recordingTimer.AutoReset = false;
        recordingTimer.Start();
    }

    private void OnRecordingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            StartStopButton.IsEnabled = false;
            isRecording = false;
            StartStopButton.Content = "Start Recording";
            StopRecording();
            TranscriptionTextBlock.Text = "Processing...";
            await ProcessAudio();
            StartStopButton.IsEnabled = true;
        });
    }

    private void StopRecording()
    {
        recordingTimer?.Stop();
        recordingTimer?.Dispose();

        if (waveIn != null)
        {
            waveIn.StopRecording();
            waveIn.Dispose();
        }
    }

    private async Task ProcessAudio()
    {
        try
        {
            var transcription = await TranscribeAudio();
            TranscriptionTextBlock.Text = transcription;
        }
        catch
        {
            TranscriptionTextBlock.Text = "Error Processing Audio!";
        }
    }

    private async Task<string> TranscribeAudio()
    {
        if (audioStream == null)
        {
            return "No audio recorded";
        }

        var audioData = audioStream.ToArray();
        var sourceLanguage = SourceLanguageComboBox.SelectedItem.ToString();
        if (sourceLanguage == null || !WhisperWrapper.LanguageCodes.ContainsKey(sourceLanguage))
        {
            return "Invalid language selected";
        }

        SendSampleInteractedEvent("TranscribeAudio"); // <exclude-line>
        cts = new CancellationTokenSource();

        var transcribedChunks = await whisper.TranscribeAsync(audioData, sourceLanguage, WhisperWrapper.TaskType.Transcribe, (bool)TimeStampsToggle.IsChecked!, cts.Token);

        return transcribedChunks;
    }

    private void DisposeMemory()
    {
        StopRecording();
        cts?.Cancel();
        cts?.Dispose();
        whisper.Dispose();
        waveIn?.StopRecording();
        waveIn?.Dispose();
        audioStream?.Dispose();
        recordingTimer?.Stop();
        recordingTimer?.Dispose();
    }
}