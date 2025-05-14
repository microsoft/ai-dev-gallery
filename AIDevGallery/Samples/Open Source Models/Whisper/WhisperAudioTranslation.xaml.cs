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
    Name = "Whisper Audio Translation",
    Model1Types = [ModelType.Whisper],
    Scenario = ScenarioType.AudioAndVideoTranslateAudio,
    SharedCode = [
        SharedCodeEnum.WhisperWrapper
    ],
    NugetPackageReferences = [
        "NAudio.WinMM",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Id = "a969cb7a-67c3-4675-9ab1-7c5f9f0f8dd6",
    Icon = "\uE8D4")]
internal sealed partial class WhisperAudioTranslation : BaseSamplePage
{
    private WaveInEvent? waveIn;
    private MemoryStream? audioStream;
    private WhisperWrapper whisper = null!;
    private System.Timers.Timer? recordingTimer;
    private CancellationTokenSource? cts;
    private bool isRecording;

    public WhisperAudioTranslation()
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

        SourceLanguageComboBox.SelectedIndex = 1;
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        StartStopButton.IsEnabled = false;
        isRecording = !isRecording;

        if (isRecording)
        {
            TranslationTextBlock.Text = "Recording...";
            StartRecording();
            StartStopButton.Content = "Stop Recording";
        }
        else
        {
            StartStopButton.Content = "Processing...";
            StopRecording();
            TranslationTextBlock.Text = "Processing...";
            await TranslateAudioAsync();
            StartStopButton.Content = "Start Recording";
        }

        StartStopButton.IsEnabled = true;
    }

    private async Task TranslateAudioAsync()
    {
        if (cts == null || cts.IsCancellationRequested)
        {
            return;
        }

        if (audioStream == null)
        {
            TranslationTextBlock.Text = "Please record audio first.";
            return;
        }

        var sourceLanguage = SourceLanguageComboBox.SelectedItem.ToString();

        if (sourceLanguage == null || !WhisperWrapper.LanguageCodes.ContainsKey(sourceLanguage))
        {
            TranslationTextBlock.Text = "Please select a source language.";
            return;
        }

        SendSampleInteractedEvent("TranslateAudio"); // <exclude-line>
        try
        {
            var audioData = audioStream.ToArray();
            var transcribedChunks = await whisper.TranscribeAsync(audioData, sourceLanguage, WhisperWrapper.TaskType.Translate, (bool)TimeStampsToggle.IsChecked!, cts.Token);

            TranslationTextBlock.Text = transcribedChunks;
        }
        catch
        {
            TranslationTextBlock.Text = "Error processing audio!";
        }
    }

    private void StartRecording()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        cts = new CancellationTokenSource();

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
            TranslationTextBlock.Text = "Processing...";
            await TranslateAudioAsync();
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