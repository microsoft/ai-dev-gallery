// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using ColorCode;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels
{
    [GallerySample(
        Model1Types = [ModelType.LanguageModels],
        Scenario = ScenarioType.CodeGenerateCode,
        SharedCode = [
            SharedCodeEnum.GenAIModel
        ],
        NugetPackageReferences = [
            "ColorCode.WinUI",
            "Microsoft.ML.OnnxRuntimeGenAI.DirectML",
            "Microsoft.Extensions.AI.Abstractions"
        ],
        Name = "Generate Code",
        Id = "2270c051-a91c-4af9-8975-a99fda6b024b",
        Icon = "\uE8D4")]
    internal sealed partial class GenerateCode : BaseSamplePage
    {
        private readonly RichTextBlockFormatter formatter = new();
        private IChatClient? model;
        private CancellationTokenSource? cts;

        public ObservableCollection<string> LanguageStrings { get; } = ["C#", "C++", "Java", "Python", "JavaScript", "TypeScript"];

        public GenerateCode()
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
            CancelGenerate();
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

        public void GenerateSolution(string problem, string currentLanguage)
        {
            if (model == null)
            {
                return;
            }

            string generatedCode = string.Empty;
            this.GenerateRichTextBlock.Blocks.Clear();
            GenerateButton.Visibility = Visibility.Collapsed;

            Task.Run(
                async () =>
                {
                    string systemPrompt = "You generate code in " + currentLanguage + ". Respond with only the code in " + currentLanguage + " and no extraneous text.";
                    cts = new CancellationTokenSource();

                    IsProgressVisible = true;
                    await foreach (var messagePart in model.CompleteStreamingAsync(
                        [
                            new ChatMessage(ChatRole.System, systemPrompt),
                            new ChatMessage(ChatRole.User, problem)
                        ],
                        null,
                        cts.Token))
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (isProgressVisible)
                            {
                                StopBtn.Visibility = Visibility.Visible;
                                IsProgressVisible = false;
                            }

                            generatedCode += messagePart;

                            this.GenerateRichTextBlock.Blocks.Clear();
                            formatter.FormatRichTextBlock(generatedCode, languageDict[currentLanguage], this.GenerateRichTextBlock);
                        });
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateCodeDoneAnnouncementActivityId"); // <exclude-line>
                        StopBtn.Visibility = Visibility.Collapsed;
                        GenerateButton.IsEnabled = true;
                        GenerateButton.Visibility = Visibility.Visible;
                    });

                    cts?.Dispose();
                    cts = null;
                });
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (InputTextBox.Text.Length > 0 && LanguageComboBox.SelectedItem is string currentLanguage)
            {
                IsProgressVisible = true;
                StopBtn.Visibility = Visibility.Visible;
                GenerateSolution(InputTextBox.Text, currentLanguage);
            }
        }

        private void CancelGenerate()
        {
            IsProgressVisible = false;
            StopBtn.Visibility = Visibility.Collapsed;
            GenerateButton.Visibility = Visibility.Visible;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelGenerate();
        }

        private readonly Dictionary<string, ILanguage> languageDict = new()
        {
            { "C#", Languages.CSharp },
            { "C++", Languages.Cpp },
            { "Java", Languages.Java },
            { "Python", Languages.Python },
            { "JavaScript", Languages.JavaScript },
            { "TypeScript", Languages.Typescript },
        };
        private bool isProgressVisible;

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

                GenerateButton.IsEnabled = inputLength <= GenAIModel.DefaultMaxLength;
            }
            else
            {
                InputTextBox.Description = string.Empty;
                GenerateButton.IsEnabled = false;
            }
        }
    }
}