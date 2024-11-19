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
    Name = "Custom Parameters",
    Model1Types = [ModelType.LanguageModels],
    Id = "0d884b79-26ab-47a3-a752-1b8a7fa5737d",
    Icon = "\uE8D4",
    Scenario = ScenarioType.TextCustomParameters,
    NugetPackageReferences = [
        "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
        "Microsoft.Extensions.AI.Abstractions"
    ],
    SharedCode = [
        SharedCodeEnum.GenAIModel
    ])]
internal sealed partial class CustomSystemPrompt : BaseSamplePage
{
    private readonly ChatOptions chatOptions = GenAIModel.GetDefaultChatOptions();
    private IChatClient? model;
    private CancellationTokenSource? cts;
    private bool isProgressVisible;

    public CustomSystemPrompt()
    {
        Unloaded += (s, e) => CleanUp();
        Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        InitializeComponent();
        DoSampleToggle.Toggled += DoSampleToggle_Toggled;
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
        CancelGeneration();
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

    public void GenerateText(string query, string systemPrompt)
    {
        if (model == null)
        {
            return;
        }

        OutputTextBlock.Text = string.Empty;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "CustomPromptWaitAnnouncementActivityId"); // <exclude-line>

        Task.Run(
            async () =>
            {
                cts = new CancellationTokenSource();

                IsProgressVisible = true;

                await foreach (var messagePart in model.CompleteStreamingAsync(
                    [
                        new ChatMessage(ChatRole.System, systemPrompt),
                        new ChatMessage(ChatRole.User, query)
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

                        OutputTextBlock.Text += messagePart;

                        // <exclude>
                        if (!contentStartedBeingGenerated)
                        {
                            NarratorHelper.Announce(InputTextBox, "Content has started generating.", "CustomPromptAnnouncementActivityId");
                            contentStartedBeingGenerated = true;
                        }

                        // </exclude>
                    });
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "CustomPromptDoneAnnouncementActivityId"); // <exclude-line>
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
        if (InputTextBox.Text.Length > 0 && SystemPromptInputTextBox.Text.Length > 0)
        {
            SetSearchOptions();
            GenerateText(InputTextBox.Text, SystemPromptInputTextBox.Text);
        }
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

    private void SetSearchOptions()
    {
        if (MinLengthSlider.Value > MaxLengthSlider.Value)
        {
            MaxLengthSlider.Value = MinLengthSlider.Value;
        }

        chatOptions.AdditionalProperties!["min_length"] = (int)MinLengthSlider.Value;
        chatOptions.MaxOutputTokens = (int)MaxLengthSlider.Value;
        chatOptions.Temperature = (float)TemperatureSlider.Value;
        chatOptions.TopK = (int)TopKSlider.Value;
        chatOptions.TopP = (float)TopPSlider.Value;
        chatOptions.AdditionalProperties!["do_sample"] = DoSampleToggle.IsOn;
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
            if (inputLength >= chatOptions.MaxOutputTokens)
            {
                InputTextBox.Description = $"{inputLength} of {chatOptions.MaxOutputTokens}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {chatOptions.MaxOutputTokens}";
            }

            GenerateButton.IsEnabled = inputLength <= chatOptions.MaxOutputTokens;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }

    private void DoSampleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch doSampleToggle)
        {
            if (doSampleToggle.IsOn)
            {
                TopPSlider.IsEnabled = true;
                TopKSlider.IsEnabled = true;
                TemperatureSlider.IsEnabled = true;
                doSampleToggle.Header = "Sampling Enabled";
            }
            else
            {
                TopPSlider.IsEnabled = false;
                TopKSlider.IsEnabled = false;
                TemperatureSlider.IsEnabled = false;
                doSampleToggle.Header = "Sampling Disabled";
            }
        }
    }
}