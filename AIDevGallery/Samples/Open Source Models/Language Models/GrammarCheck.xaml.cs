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
    Model1Types = [ModelType.LanguageModels],
    Scenario = ScenarioType.TextGrammarCheckText,
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ],
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    Name = "Grammar Check",
    Id = "9e1b5ac5-3521-4e88-a2ce-60152a6cb44f",
    Icon = "\uE8D4")]
internal sealed partial class GrammarCheck : BaseSamplePage
{
    private readonly ChatOptions chatOptions = GenAIModel.GetDefaultChatOptions();
    private IChatClient? model;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;

    public GrammarCheck()
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
        CancelGrammarCheck();
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

    public void GrammarCheckText(string text)
    {
        if (model == null)
        {
            return;
        }

        CheckedTextBlock.Text = string.Empty;
        CheckGrammarButton.Visibility = Visibility.Collapsed;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Checking grammar, please wait.", "GrammarCheckWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GrammarCheckText"); // <exclude-line>

        Task.Run(
            async () =>
            {
                string systemPrompt = "You grammar check user-provided text. " +
                "Respond with only the grammar corrected version of the user text and no extraneous text.";

                string userPrompt = "Grammar check this text: " + text;

                cts = new CancellationTokenSource();

                IsProgressVisible = true;

                await foreach (var messagePart in model.GetStreamingResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, systemPrompt),
                        new ChatMessage(ChatRole.User, userPrompt)
                    ],
                    chatOptions,
                    cts.Token))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (isProgressVisible)
                        {
                            StopBtn.Visibility = Visibility.Visible;
                            IsProgressVisible = false;
                        }

                        CheckedTextBlock.Text += messagePart;

                        // <exclude>
                        if (!contentStartedBeingGenerated)
                        {
                            NarratorHelper.Announce(InputTextBox, "Content has started generating.", "GrammarGeneratedAnnouncementActivityId");
                            contentStartedBeingGenerated = true;
                        }

                        // </exclude>
                    });
                }

                cts?.Dispose();
                cts = null;

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GrammarDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    CheckGrammarButton.Visibility = Visibility.Visible;
                });
            });
    }

    private void GrammarCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            StopBtn.Visibility = Visibility.Visible;
            IsProgressVisible = true;
            GrammarCheckText(InputTextBox.Text);
        }
    }

    private void CancelGrammarCheck()
    {
        IsProgressVisible = false;
        StopBtn.Visibility = Visibility.Collapsed;
        CheckGrammarButton.Visibility = Visibility.Visible;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGrammarCheck();
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

            CheckGrammarButton.IsEnabled = inputLength <= chatOptions.MaxOutputTokens;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            CheckGrammarButton.IsEnabled = false;
        }
    }
}