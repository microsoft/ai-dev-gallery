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
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Name = "Tool Calling",
    Model1Types = [ModelType.LanguageModels],
    Scenario = ScenarioType.TextToolCalling,
    NugetPackageReferences = [
    ],
    Id = "25bb4e58-d909-4377-b59c-975cd6baff19",
    Icon = "\uEC7A")]
internal sealed partial class ToolCalling : BaseSamplePage
{
    private const int _maxTokenLength = 1024;
    private IChatClient? chatClient;
    private CancellationTokenSource? cts;
    private ChatOptions chatOptions;
    private List<ChatMessage> _history = new List<ChatMessage>();
    private bool isProgressVisible;
    private bool isImeActive = true;

    public ToolCalling()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>

        [Description("Get the weather for a specific city.")]
        string GetWeatherForCity(string city)
        {
            if(city == "New York")
            {
                return "40 degrees and rainy";
            }
            else if(city == "Durham")
            {
                return "85 degrees and sunny";
            }

            return "No city entered.";
        }

        [Description("Increase the output font size.")]
        void IncreaseFontSize()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                GenerateTextBlock.FontSize = GenerateTextBlock.FontSize + 8;
            });
        }

        chatOptions = new ChatOptions()
        {
            Tools = [
                AIFunctionFactory.Create(GetWeatherForCity),
                AIFunctionFactory.Create(IncreaseFontSize)
            ]
        };

        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            chatClient = sampleParams.GetFunctionInvokingIChatClientAsync();
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
                string systemPrompt = "You are a helpful assistant with some tools.";
                string userPrompt = topic;

                cts = new CancellationTokenSource();

                IsProgressVisible = true;

                _history.Add(new ChatMessage(ChatRole.User, userPrompt));
                await foreach (var messagePart in chatClient.GetStreamingResponseAsync(
                    _history,
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