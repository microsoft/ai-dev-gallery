// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Translate",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextTranslateText,
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ],
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    Id = "f045fca2-c657-4894-99f2-d0a1115176bc",
    Icon = "\uE8D4")]
internal sealed partial class Translate : BaseSamplePage
{
    private IChatClient? model;
    private CancellationTokenSource? cts;

    public Translate()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        model = await sampleParams.GetIChatClientAsync();
        InputTextBox.MaxLength = GenAIModel.DefaultMaxLength;
        sampleParams.NotifyCompletion();
    }

    // <exclude>
    private void Page_Loaded()
    {
        InputTextBox.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private void CleanUp()
    {
        CancelTranslation();
        model?.Dispose();
    }

    public bool IsProgressVisible
    {
        get => isProgressVisible;
        set
        {
            isProgressVisible = value;
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopIcon.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    public void TranslateText(string text)
    {
        if (model == null || LanguageBox.SelectedItem == null)
        {
            return;
        }

        // <exclude>
        var contentStartedBeingGenerated = false;
        NarratorHelper.Announce(InputTextBox, "Translating content, please wait.", "TranslateTextWaitAnnouncementActivityId");
        SendSampleInteractedEvent("TranslateText"); // <exclude-line>

        // </exclude>
        if (LanguageBox.SelectedItem is string language)
        {
            TranslatedTextBlock.Text = string.Empty;
            Task.Run(
                async () =>
                {
                    string targetLanguage = language.ToString();
                    string systemPrompt = "You translate user provided text. Do not reply with any extraneous content besides the translated text itself.";
                    string userPrompt = $@"Translate '{text}' to {targetLanguage}.";

                    cts = new CancellationTokenSource();

                    IsProgressVisible = true;

                    await foreach (var messagePart in model.CompleteStreamingAsync(
                        [
                            new ChatMessage(ChatRole.System, systemPrompt),
                            new ChatMessage(ChatRole.User, userPrompt)
                        ],
                        null,
                        cts.Token))
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (isProgressVisible)
                            {
                                IsProgressVisible = false;
                            }

                            TranslatedTextBlock.Text += messagePart;

                            // <exclude>
                            if (!contentStartedBeingGenerated)
                            {
                                NarratorHelper.Announce(InputTextBox, "Translated text has started generating.", "TranslationGeneratedAnnouncementActivityId");
                                contentStartedBeingGenerated = true;
                            }

                            // </exclude>
                        });
                    }

                    cts?.Dispose();
                    cts = null;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NarratorHelper.Announce(InputTextBox, "Translation has finished generating.", "TranslationDoneAnnouncementActivityId"); // <exclude-line>
                        StopBtn.Visibility = Visibility.Collapsed;
                        TranslateButton.Visibility = Visibility.Visible;
                    });
                });
        }
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            TranslateButton.Visibility = Visibility.Collapsed;
            IsProgressVisible = true;
            StopBtn.Visibility = Visibility.Visible;
            TranslateText(InputTextBox.Text);
        }
    }

    private void CancelTranslation()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        TranslateButton.Visibility = Visibility.Visible;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private readonly List<string> languages =
    [
        "Afrikaans",
        "Arabic",
        "Czech",
        "Danish",
        "Dutch",
        "English",
        "Filipino",
        "Finnish",
        "French",
        "German",
        "Greek",
        "Hindi",
        "Indonesian",
        "Italian",
        "Japanese",
        "Korean",
        "Mandarin",
        "Polish",
        "Portuguese",
        "Romanian",
        "Russian",
        "Serbian",
        "Slovak",
        "Spanish",
        "Thai",
        "Turkish",
        "Vietnamese"
    ];
    private bool isProgressVisible;

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelTranslation();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            if (inputLength >= GenAIModel.DefaultMaxLength)
            {
                InputTextBox.Description = $"{inputLength} of {GenAIModel.DefaultMaxLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {GenAIModel.DefaultMaxLength}";
            }

            TranslateButton.IsEnabled = inputLength <= GenAIModel.DefaultMaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            TranslateButton.Visibility = Visibility.Visible;
        }
    }
}