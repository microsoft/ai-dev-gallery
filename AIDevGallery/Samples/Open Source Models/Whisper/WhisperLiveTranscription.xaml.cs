// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.Whisper;

[GallerySample(
    Name = "Whisper Live Transcription",
    Model1Types = [ModelType.Whisper],
    Scenario = ScenarioType.AudioAndVideoTranscribeLiveAudio,
    SharedCode = [
        SharedCodeEnum.TextWatermarker,
        SharedCodeEnum.WhisperWrapper,
        SharedCodeEnum.AudioRecorder
    ],
    NugetPackageReferences = [
        "NAudio.WinMM",
        "Microsoft.ML.OnnxRuntime.Extensions",
        "System.Numerics.Tensors"
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
        audioRecorder = new AudioRecorder();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            whisper = await WhisperWrapper.CreateAsync(sampleParams.ModelPath, sampleParams.WinMlSampleOptions.Policy, sampleParams.WinMlSampleOptions.EpName, sampleParams.WinMlSampleOptions.CompileModel, sampleParams.WinMlSampleOptions.DeviceType);
            sampleParams.NotifyCompletion();
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load model.");
            return;
        }
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

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (isRecording)
        {
            StartStopButton.IsEnabled = false;
            audioRecorder.StopRecording();
            SendSampleInteractedEvent("StartRecording"); // <exclude-line>
            return;
        }

        isRecording = true;
        StartStopButton.Content = "Stop Recording";
        TranscriptionTextBlock.Text = string.Empty;
        var watermarker = new TextWatermarker();
        var token = cts.Token;
        try
        {
            // Stopping completes the stream after the last audio buffer, not the last button click.
            await foreach (var audioData in audioRecorder.StartRecording(token))
            {
                if (SourceLanguageComboBox.SelectedItem is string language)
                {
                    var text = await whisper.TranscribeAsync(audioData, language, WhisperWrapper.TaskType.Transcribe, false, token);
                    TranscriptionTextBlock.Text += watermarker.Append(text);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The page was unloaded.
        }
        catch (Exception ex)
        {
            audioRecorder.StopRecording();
            ShowException(ex, "Failed to transcribe audio.");
        }
        finally
        {
            TranscriptionTextBlock.Text += watermarker.Finish();
            isRecording = false;
            StartStopButton.Content = "Start Recording";
            StartStopButton.IsEnabled = true;
        }
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