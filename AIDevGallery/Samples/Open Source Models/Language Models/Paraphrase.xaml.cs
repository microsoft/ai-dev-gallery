// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Paraphrase",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextParaphraseText,
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ],
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    Id = "9e006e82-8e3f-4401-8a83-d4c4c59cc20c",
    Icon = "\uE8D4")]
internal sealed partial class Paraphrase : BaseSamplePage
{
    private IChatClient? model;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;

    public Paraphrase()
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
        CancelParaphrase();
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

    public void ParaphraseText(string text)
    {
        if (model == null)
        {
            return;
        }

        ParaphrasedTextBlock.Text = string.Empty;
        ParaphraseButton.Visibility = Visibility.Collapsed;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Paraphrasing text, please wait.", "ParaphraseWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("ParaphraseText"); // <exclude-line>

        Task.Run(
            async () =>
            {
                string systemPrompt = "You paraphrase user-provided text. " +
                "Respond with only the paraphrased content and no extraneous text.";
                string userPrompt = "Paraphrase this text: " + text;

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
                        if (IsProgressVisible)
                        {
                            StopBtn.Visibility = Visibility.Visible;
                            IsProgressVisible = false;
                        }

                        ParaphrasedTextBlock.Text += messagePart;

                        // <exclude>
                        if (!contentStartedBeingGenerated)
                        {
                            NarratorHelper.Announce(InputTextBox, "Paraphrased content has started generating.", "ParaphraseGeneratedAnnouncementActivityId");
                            contentStartedBeingGenerated = true;
                        }

                        // </exclude>
                    });
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "ParaphraseDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    ParaphraseButton.Visibility = Visibility.Visible;
                });

                cts?.Dispose();
                cts = null;
            });
    }

    private void ParaphraseButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            IsProgressVisible = true;
            StopBtn.Visibility = Visibility.Visible;
            ParaphraseButton.Visibility = Visibility.Collapsed;
            ParaphraseText(InputTextBox.Text);
        }
    }

    private void CancelParaphrase()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        ParaphraseButton.Visibility = Visibility.Visible;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelParaphrase();
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

            ParaphraseButton.IsEnabled = inputLength <= GenAIModel.DefaultMaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            ParaphraseButton.IsEnabled = false;
        }
    }
}