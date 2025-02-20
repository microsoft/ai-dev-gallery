// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextAnalyzeSentimentText,
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ],
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    Name = "Sentiment Analysis",
    Id = "9cc84d1e-6b02-4bd2-a350-6e38c3a92ced",
    Icon = "\uE8D4")]
internal sealed partial class SentimentAnalysis : BaseSamplePage
{
    private readonly ChatOptions chatOptions = GenAIModel.GetDefaultChatOptions();
    private IChatClient? model;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;

    public SentimentAnalysis()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        model = await sampleParams.GetIChatClientAsync();
        InputTextBox.MaxLength = chatOptions.MaxOutputTokens ?? 0;
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
        CancelSentiment();
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

    public void AnalyzeSentiment(string text)
    {
        if (model == null)
        {
            return;
        }

        SentimentTextBlock.Text = string.Empty;
        SentimentButton.Visibility = Visibility.Collapsed;
        NarratorHelper.Announce(InputTextBox, "Checking sentiment, please wait.", "SentimentCheckWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("AnalyzeSentiment"); // <exclude-line>

        Task.Run(
            async () =>
            {
                var systemPrompt = "You analyze the sentiment of user provided text. " +
                    "Respond in JSON with the following fields:" +
                    "1. Sentiment: An integer between -2 and 2 that maps to the sentiment string values." +
                    "2. SentimentString: which can have a value of either Negative, Slightly Negative, Neutral, Slightly Positive, or Positive." +
                    "Do not reply with anything besides the JSON itself.";

                var userPrompt = "Analyze the sentiment of the following text: " + text;

                cts = new CancellationTokenSource();

                var response = string.Empty;

                var matchFound = false;

                await foreach (var messagePart in model.CompleteStreamingAsync(
                    [
                        new ChatMessage(ChatRole.System, systemPrompt),
                        new ChatMessage(ChatRole.User, userPrompt)
                    ],
                    chatOptions,
                    cts.Token))
                {
                    response += messagePart;
                    Match match = SentimentRegex().Match(response);
                    if (match.Success)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            StopBtn.Visibility = Visibility.Visible;
                            IsProgressVisible = false;
                            SentimentTextBlock.Text = $"Sentiment: {match.Groups[1].Value}\nSentiment String: {match.Groups[2].Value}";
                            matchFound = true;
                            cts.Cancel();
                        });
                        break;
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "SentimentAnalysisDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    SentimentButton.Visibility = Visibility.Visible;
                    if (!matchFound)
                    {
                        SentimentTextBlock.Text = "No sentiment found";
                    }
                });

                NarratorHelper.Announce(InputTextBox, "Sentiment Analyzed.", "SentimentAnalysisDoneAnnouncementActivityId"); // <exclude-line>

                cts?.Dispose();
                cts = null;
            });
    }

    private void SentimentButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            StopBtn.Visibility = Visibility.Visible;
            IsProgressVisible = true;
            AnalyzeSentiment(InputTextBox.Text);
        }
    }

    private void CancelSentiment()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        SentimentButton.Visibility = Visibility.Visible;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelSentiment();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            if (inputLength >= chatOptions.MaxOutputTokens)
            {
                InputTextBox.Description = $"{inputLength} of {chatOptions.MaxOutputTokens}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {chatOptions.MaxOutputTokens}";
            }

            SentimentButton.IsEnabled = inputLength <= chatOptions.MaxOutputTokens;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            SentimentButton.IsEnabled = false;
        }
    }

    [GeneratedRegex("{\\s*\"Sentiment\": (.*),\\s*\"SentimentString\": \"(.*)\"\\s*}", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SentimentRegex();
}