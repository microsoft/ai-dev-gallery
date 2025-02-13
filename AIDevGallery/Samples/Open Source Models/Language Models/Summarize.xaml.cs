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
    Name = "Summarize",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextSummarizeText,
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ],
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    Id = "21bf3574-aaa5-42fd-9f6c-3bfbbca00876",
    Icon = "\uE8D4")]
internal sealed partial class Summarize : BaseSamplePage
{
    private IChatClient? model;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;

    public Summarize()
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

    private void Page_Loaded()
    {
        InputTextBox.Focus(FocusState.Programmatic);
    }

    private void CleanUp()
    {
        CancelSummary();
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

    public void SummarizeText(string text)
    {
        if (model == null)
        {
            return;
        }

        SummaryTextBlock.Text = string.Empty;
        SummarizeButton.Visibility = Visibility.Collapsed;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Summarizing content, please wait.", "SummarizeTextWaitAnnouncementActivityId"); // <exclude-line>

        Task.Run(
            async () =>
            {
                string systemPrompt = "You summarize user-provided text. " +
                "Respond with only the summary itself and no extraneous text.";
                string userPrompt = "Summarize this text: " + text;

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

                        SummaryTextBlock.Text += messagePart;

                        // <exclude>
                        if (!contentStartedBeingGenerated)
                        {
                            NarratorHelper.Announce(InputTextBox, "Summary has started generating.", "SummaryGeneratedAnnouncementActivityId");
                            contentStartedBeingGenerated = true;
                        }

                        // </exclude>
                    });
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "SummaryDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    SummarizeButton.Visibility = Visibility.Visible;
                });

                cts?.Dispose();
                cts = null;
            });
    }

    private void SummarizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            IsProgressVisible = true;
            StopBtn.Visibility = Visibility.Visible;
            SummarizeText(InputTextBox.Text);
        }
    }

    private void CancelSummary()
    {
        IsProgressVisible = false;
        StopBtn.Visibility = Visibility.Collapsed;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelSummary();
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

            SummarizeButton.IsEnabled = inputLength <= GenAIModel.DefaultMaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            SummarizeButton.IsEnabled = false;
        }
    }
}