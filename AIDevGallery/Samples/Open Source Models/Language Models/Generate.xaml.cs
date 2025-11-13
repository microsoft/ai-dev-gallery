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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Generate",
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.TextGenerateText,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Id = "25bb4e58-d909-4377-b59c-975cd6baff19",
    Icon = "\uE8D4")]
internal sealed partial class Generate : BaseSamplePage
{
    private const int _maxTokenLength = 1024;
    private IChatClient? chatClient;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;
    private bool isImeActive = true;

    public Generate()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            chatClient = await sampleParams.GetIChatClientAsync();
            InputTextBox.MaxLength = _maxTokenLength;
        }
        catch (System.Exception ex)
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
        chatClient?.Dispose();
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

    public void GenerateText(string topic)
    {
        if (chatClient == null)
        {
            return;
        }

        GenerateTextBlock.Text = string.Empty;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        Task.Run(
            async () =>
            {
                string systemPrompt = "You generate text based on a user-provided topic. Respond with only the generated content and no extraneous text.";
                string userPrompt = "Generate text based on the topic: " + topic;

                cts = new CancellationTokenSource();

                IsProgressVisible = true;

                try
                {
                    // <exclude>
                    ShowDebugInfo(null); // <exclude-line>
                    var swEnd = Stopwatch.StartNew();
                    var swTtft = Stopwatch.StartNew();
                    int outputTokens = 0;

                    // </exclude>
                    await foreach (var messagePart in chatClient.GetStreamingResponseAsync(
                        [
                            new ChatMessage(ChatRole.System, systemPrompt),
                            new ChatMessage(ChatRole.User, userPrompt)
                        ],
                        null,
                        cts.Token))
                    {
                        // <exclude>
                        if (outputTokens == 0)
                        {
                            swTtft.Stop();
                        }

                        outputTokens++;
                        double currentTps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
                        ShowDebugInfo($"{Math.Round(currentTps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

                        // </exclude>
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (isProgressVisible)
                            {
                                StopBtn.Visibility = Visibility.Visible;
                                IsProgressVisible = false;
                            }

                            GenerateTextBlock.Text += messagePart;

                            // <exclude>
                            if (!contentStartedBeingGenerated)
                            {
                                NarratorHelper.Announce(InputTextBox, "Content has started generating.", "GeneratedAnnouncementActivityId");
                                contentStartedBeingGenerated = true;
                            }

                            // </exclude>
                        });
                    }

                    // <exclude>
                    swEnd.Stop();
                    double tps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
                    ShowDebugInfo($"{Math.Round(tps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

                    // </exclude>
                }
                catch (Exception ex)
                {
                    if (cts != null && !cts.Token.IsCancellationRequested)
                    {
                        ShowException(ex);
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
                    StopBtn.Visibility = Visibility.Collapsed;
                    GenerateButton.Visibility = Visibility.Visible;
                    InputTextBox.IsEnabled = true;
                });

                cts?.Dispose();
                cts = null;
            });
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
        else
        {
            isImeActive = true;
        }
    }

    private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        isImeActive = false;
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGeneration();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            if (inputLength > _maxTokenLength)
            {
                InputTextBox.Description = $"{inputLength} of {_maxTokenLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {_maxTokenLength}";
            }

            GenerateButton.IsEnabled = inputLength <= _maxTokenLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }
}