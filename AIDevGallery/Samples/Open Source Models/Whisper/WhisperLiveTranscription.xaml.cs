// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.Whisper;

[GallerySample(
    Name = "Whisper Live Transcription",
    Model1Types = [ModelType.Whisper],
    Scenario = ScenarioType.AudioAndVideoTranscribeLiveAudio,
    SharedCode = [
        SharedCodeEnum.WhisperWrapper,
        SharedCodeEnum.AudioRecorder
    ],
    NugetPackageReferences = [
        "NAudio.WinMM",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Id = "2b13b8ef-a75c-4982-9f7e-eae1a11c87a2",
    Icon = "\uE8D4")]
internal sealed partial class WhisperLiveTranscription : BaseSamplePage
{
    private readonly AudioRecorder audioRecorder;
    private bool isRecording;
    private WhisperWrapper whisper = null!;
    private CancellationTokenSource cts = new();

    public WhisperLiveTranscription()
    {
        this.Unloaded += (s, e) => DisposeMemory();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
        PopulateLanguageComboBoxes();
        audioRecorder = new AudioRecorder(UpdateTranscription);
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        whisper = await WhisperWrapper.CreateAsync(sampleParams.ModelPath);
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
        // Populate SourceLanguageComboBox
        foreach (var language in WhisperWrapper.LanguageCodes.Keys)
        {
            SourceLanguageComboBox.Items.Add(language);
        }

        // Select default source language
        SourceLanguageComboBox.SelectedIndex = 0;
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (isRecording)
        {
            audioRecorder.StopRecording();
            StartStopButton.Content = "Start Recording";
            SendSampleInteractedEvent("StartRecording"); // <exclude-line>
        }
        else
        {
            TranscriptionTextBlock.Text = string.Empty;
            cts = new CancellationTokenSource();
            audioRecorder.StartRecording(cts.Token);
            StartStopButton.Content = "Stop Recording";
        }

        isRecording = !isRecording;
    }

    private void UpdateTranscription(byte[] audioData)
    {
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            var sourceLanguage = SourceLanguageComboBox.SelectedItem.ToString();
            if (sourceLanguage == null || !WhisperWrapper.LanguageCodes.ContainsKey(sourceLanguage) || cts.IsCancellationRequested)
            {
                return;
            }

            var transcription = await whisper.TranscribeAsync(audioData, sourceLanguage, WhisperWrapper.TaskType.Transcribe, false, cts.Token);
            TranscriptionTextBlock.Text += transcription;
        });
    }

    private void DisposeMemory()
    {
        audioRecorder.StopRecording();
        cts?.Cancel();
        cts?.Dispose();
        whisper.Dispose();
        audioRecorder.Dispose();
    }
}