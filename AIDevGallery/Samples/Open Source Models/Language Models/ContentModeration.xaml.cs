// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Content Moderation",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextContentModeration,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI.Abstractions"
    ],
    SharedCode = [],
    Id = "language-content-moderation",
    Icon = "\uE8D4")]
internal sealed partial class ContentModeration : BaseSamplePage
{
    private IChatClient? model;
    private CancellationTokenSource? cts;

    private bool isImeActive = true;

    public ContentModeration()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            model = await sampleParams.GetIChatClientAsync();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }

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
        CancelGeneration();
        model?.Dispose();
    }

    public void GenerateText(string prompt)
    {
        if (model == null)
        {
            return;
        }

        GenerateTextBlock.Text = string.Empty;
        ContentModerationStatus.Text = string.Empty;
        GenerateButton.IsEnabled = false;
        StopBtn.Visibility = Visibility.Visible;
        OutputProgressBar.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Moderating content, please wait.", "ContentModerationWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        Task.Run(
            async () =>
            {
                string systemPrompt = "You are a helpful assistant.";

                cts = new CancellationTokenSource();

                var isProgressVisible = true;

                DispatcherQueue.TryEnqueue(() =>
                {
                    ContentModerationStatus.Text = "Validating...";
                });

                if (await ValidatePromptSafety(prompt, cts.Token))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ContentModerationStatus.Text = "Prompt is safe, generating response...";
                        NarratorHelper.Announce(InputTextBox, "Prompt is safe, generating response", "ContentModerationWaitAnnouncementActivityId"); // <exclude-line>
                    });

                    await foreach (var messagePart in model.GetStreamingResponseAsync(
                        [
                            new ChatMessage(ChatRole.System, systemPrompt),
                            new ChatMessage(ChatRole.User, prompt)
                        ],
                        null,
                        cts.Token))
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (isProgressVisible)
                            {
                                StopBtn.Visibility = Visibility.Visible;
                                OutputProgressBar.Visibility = Visibility.Collapsed;
                                isProgressVisible = false;
                            }

                            GenerateTextBlock.Text += messagePart;

                            // <exclude>
                            if (!contentStartedBeingGenerated)
                            {
                                NarratorHelper.Announce(InputTextBox, "Content has started generating.", "ContentModerationGeneratedAnnouncementActivityId");
                                contentStartedBeingGenerated = true;
                            }

                            // </exclude>
                        });
                    }
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (isProgressVisible)
                        {
                            OutputProgressBar.Visibility = Visibility.Collapsed;
                            isProgressVisible = false;
                        }

                        GenerateTextBlock.Text += string.Empty;
                        ContentModerationStatus.Text = "This prompt contains an unsafe request and will not be generated.";
                        NarratorHelper.Announce(InputTextBox, "This prompt contains an unsafe request and will not be generated.", "ContentModerationWaitAnnouncementActivityId"); // <exclude-line>
                    });
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "ContentModerationDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    GenerateButton.IsEnabled = true;
                    InputTextBox.IsEnabled = true;
                });

                cts?.Dispose();
                cts = null;
            });
    }

    private async Task<bool> ValidatePromptSafety(string prompt, CancellationToken ct)
    {
        if (model == null)
        {
            return false;
        }

        string promptSafetyPromptSystemInstructions = $@"You are tasked with determining whether an input prompt is safe to proceed or not.
                If the prompt has the direct purpose of creating content in these categories: [hate, racism, sexism, stereotyping, violence, sexual content, self-harm content, illegal activity, and malicious code], 
                then it is not safe. If it doesn't directly relate to these categories or has the purpose of creating content for these categories then reply 'safe' and NOTHING ELSE! Otherwise respond with the category that the prompt is intending to show or is related to.";

        string promptSafetyPromptUserInstructions = $@"Input prompt:
                {prompt}
                Output safety decision (If safe, respond 'safe' and NOTHING ELSE).";

        string safetyDecision = string.Empty;

        await foreach (var messagePart in model.GetStreamingResponseAsync(
            [
                new ChatMessage(ChatRole.System, promptSafetyPromptSystemInstructions),
                new ChatMessage(ChatRole.User, promptSafetyPromptUserInstructions)
            ],
            null,
            ct))
        {
            safetyDecision += messagePart;
        }

        return safetyDecision.Replace(" ", string.Empty).StartsWith("safe", System.StringComparison.OrdinalIgnoreCase);
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            GenerateText(InputTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && isImeActive == false)
        {
            if (InputTextBox.Text.Length > 0)
            {
                GenerateText(InputTextBox.Text);
            }
        }

        isImeActive = true;
    }

    private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        isImeActive = false;
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        OutputProgressBar.Visibility = Visibility.Collapsed;
        InputTextBox.IsEnabled = true;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGeneration();
    }
}