// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels
{
    [GallerySample(
        Model1Types = [ModelType.LanguageModels],
        Scenario = ScenarioType.CodeExplainCode,
        SharedCode = [
            SharedCodeEnum.GenAIModel
        ],
        NugetPackageReferences = [
            "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
            "Microsoft.Extensions.AI.Abstractions"
        ],
        Name = "Explain Code",
        Id = "ad763407-6a97-4916-ab05-30fd22f54252",
        Icon = "\uE8D4")]
    internal sealed partial class ExplainCode : BaseSamplePage
    {
        private IChatClient? model;
        private CancellationTokenSource? cts;
        private bool isProgressVisible;
        private int maxTextLength;

        public ExplainCode()
        {
            this.Unloaded += (s, e) => CleanUp();
            this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
            this.InitializeComponent();
        }

        protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
        {
            model = await sampleParams.GetIChatClientAsync();

            // Increase the default max length to allow larger pieces of code
            // More than 7K will crash
            maxTextLength = 4096;
            InputTextBox.MaxLength = maxTextLength;
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
            CancelExplain();
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

        public void Explain(string code)
        {
            if (model == null)
            {
                return;
            }

            ExplanationTextBlock.Text = string.Empty;
            ExplainButton.Visibility = Visibility.Collapsed;
            var contentStartedBeingGenerated = false; // <exclude-line>
            NarratorHelper.Announce(InputTextBox, "Analyzing code, please wait.", "ExplainCodeWaitAnnouncementActivityId"); // <exclude-line>

            Task.Run(
                async () =>
                {
                    string systemPrompt = "You explain user provided code. Provide an explanation of code and no extraneous text. If you can't find code in the user prompt, reply with \"No Code Found.\"";
                    string userPrompt = "Explain this code: " + code;

                    cts = new CancellationTokenSource();

                    var isProgressVisible = true;

                    try
                    {
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

                                ExplanationTextBlock.Text += messagePart;

                                // <exclude>
                                if (!contentStartedBeingGenerated)
                                {
                                    NarratorHelper.Announce(InputTextBox, "Code explanation has started generating.", "CodeExplanationGeneratedAnnouncementActivityId");
                                }

                                // </exclude>
                            });
                        }
                    }
                    catch (OnnxRuntimeGenAIException)
                    {
                        CancelExplain();
                    }

                    cts?.Dispose();
                    cts = null;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "CodeExplanationDoneAnnouncementActivityId"); // <exclude-line>
                        StopBtn.Visibility = Visibility.Collapsed;
                        ExplainButton.IsEnabled = true;
                        ExplainButton.Visibility = Visibility.Visible;
                    });
                });
        }

        private void ExplainButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.InputTextBox.Text.Length > 0)
            {
                StopBtn.Visibility = Visibility.Visible;
                ExplainButton.Visibility = Visibility.Collapsed;
                ExplainButton.IsEnabled = false;
                IsProgressVisible = true;
                Explain(InputTextBox.Text);
            }
        }

        private void CancelExplain()
        {
            IsProgressVisible = false;
            StopBtn.Visibility = Visibility.Collapsed;
            ExplainButton.Visibility = Visibility.Visible;
            ExplainButton.IsEnabled = true;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelExplain();
        }

        private void InputBox_Changed(object sender, TextChangedEventArgs e)
        {
            var inputLength = InputTextBox.Text.Length;
            if (inputLength > 0)
            {
                if (inputLength >= maxTextLength)
                {
                    InputTextBox.Description = $"{inputLength} of {maxTextLength}. Max characters reached.";
                }
                else
                {
                    InputTextBox.Description = $"{inputLength} of {maxTextLength}";
                }

                ExplainButton.IsEnabled = inputLength <= maxTextLength;
            }
            else
            {
                InputTextBox.Description = string.Empty;
                ExplainButton.IsEnabled = false;
            }
        }
    }
}