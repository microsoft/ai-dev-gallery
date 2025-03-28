// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using ColorCode;
using ColorCode.Common;
using ColorCode.Styling;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.OpenSourceModels.LanguageModels;

[GallerySample(
    Model1Types = [ModelType.LanguageModels, ModelType.PhiSilica],
    Scenario = ScenarioType.CodeGenerateCode,
    NugetPackageReferences = [
        "ColorCode.WinUI",
        "Microsoft.Extensions.AI"
    ],
    Name = "Generate Code",
    Id = "2270c051-a91c-4af9-8975-a99fda6b024b",
    Icon = "\uE8D4")]
internal sealed partial class GenerateCode : BaseSamplePage
{
    private const int _defaultMaxLength = 1024;
    private RichTextBlockFormatter formatter;
    private IChatClient? chatClient;
    private CancellationTokenSource? cts;

    public ObservableCollection<string> LanguageStrings { get; } = ["C#", "C++", "Java", "Python", "JavaScript", "TypeScript"];
    private string _currentLanguage = "C#";
    private string _currentCode = string.Empty;
    private bool _leadingMarkdownParsed;

    public GenerateCode()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        formatter = new RichTextBlockFormatter(GetCodeHighlightingStyleFromElementTheme(ActualTheme));
        this.ActualThemeChanged += GenerateCode_ActualThemeChanged;
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            chatClient = await sampleParams.GetIChatClientAsync();
            InputTextBox.MaxLength = _defaultMaxLength;
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
        CancelGenerate();
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

    public void GenerateSolution(string problem, string currentLanguage)
    {
        if (chatClient == null)
        {
            return;
        }

        SendSampleInteractedEvent("GenerateSolution"); // <exclude-line>

        string generatedCode = string.Empty;
        _leadingMarkdownParsed = false;
        this.GenerateRichTextBlock.Blocks.Clear();
        GenerateButton.Visibility = Visibility.Collapsed;

        Task.Run(
            async () =>
            {
                string systemPrompt = "You generate code in " + currentLanguage + ". Respond with only the code in " + currentLanguage + " and no extraneous text.";
                cts = new CancellationTokenSource();

                IsProgressVisible = true;
                await foreach (var messagePart in chatClient.GetStreamingResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, systemPrompt),
                        new ChatMessage(ChatRole.User, problem)
                    ],
                    null,
                    cts.Token))
                {
                    generatedCode += messagePart;

                    if (!_leadingMarkdownParsed)
                    {
                        (_leadingMarkdownParsed, generatedCode) = ParseOutLeadingMarkdown(generatedCode);
                    }
                    else if (generatedCode.Contains("```\n"))
                    {
                        break;
                    }
                    else
                    {
                        _currentCode = generatedCode;
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (isProgressVisible)
                            {
                                StopBtn.Visibility = Visibility.Visible;
                                IsProgressVisible = false;
                            }

                            this.GenerateRichTextBlock.Blocks.Clear();
                            formatter.FormatRichTextBlock(_currentCode, languageDict[currentLanguage], this.GenerateRichTextBlock);
                        });
                    }
                }

                _currentCode = ParseOutTrailingMarkdown(generatedCode);

                DispatcherQueue.TryEnqueue(() =>
                {
                    this.GenerateRichTextBlock.Blocks.Clear();
                    formatter.FormatRichTextBlock(_currentCode, languageDict[currentLanguage], this.GenerateRichTextBlock);
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
            _currentLanguage = currentLanguage;
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
            if (inputLength >= _defaultMaxLength)
            {
                InputTextBox.Description = $"{inputLength} of {_defaultMaxLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {_defaultMaxLength}";
            }

            GenerateButton.IsEnabled = inputLength <= _defaultMaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }

    private void GenerateCode_ActualThemeChanged(FrameworkElement sender, object args)
    {
        formatter = new RichTextBlockFormatter(GetCodeHighlightingStyleFromElementTheme(ActualTheme));
        if (_currentCode != null && _currentCode.Length > 0)
        {
            this.GenerateRichTextBlock.Blocks.Clear();
            formatter.FormatRichTextBlock(_currentCode, languageDict[_currentLanguage], this.GenerateRichTextBlock);
        }
    }

    private StyleDictionary GetCodeHighlightingStyleFromElementTheme(ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            StyleDictionary darkStyles = StyleDictionary.DefaultDark;
            darkStyles[ScopeName.Comment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDocComment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDocTag].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlComment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDelimiter].Foreground = StyleDictionary.White;
            darkStyles[ScopeName.Keyword].Foreground = "#FF41D6FF";
            darkStyles[ScopeName.String].Foreground = "#FFFFB100";
            darkStyles[ScopeName.XmlAttributeValue].Foreground = "#FF41D6FF";
            darkStyles[ScopeName.XmlAttributeQuotes].Foreground = "#FF41D6FF";
            return darkStyles;
        }
        else
        {
            StyleDictionary lightStyles = StyleDictionary.DefaultLight;
            lightStyles[ScopeName.XmlDocComment].Foreground = "#FF006828";
            lightStyles[ScopeName.XmlDocTag].Foreground = "#FF006828";
            lightStyles[ScopeName.Comment].Foreground = "#FF006828";
            lightStyles[ScopeName.XmlAttribute].Foreground = "#FFB5004D";
            lightStyles[ScopeName.XmlName].Foreground = "#FF400000";
            return lightStyles;
        }
    }

    private (bool Parsed, string Code) ParseOutLeadingMarkdown(string code)
    {
        bool wasParsed = false;
        string[] lines = code.Split('\n');
        string outputCode = code;

        if (lines.Length > 1)
        {
            wasParsed = true;
        }

        if (wasParsed && lines[0].Contains("```"))
        {
            outputCode = string.Join('\n', lines.Skip(1));
        }

        return (wasParsed, outputCode);
    }

    private string ParseOutTrailingMarkdown(string code)
    {
        string[] lines = code.Split('\n');
        string outputCode = code;

        if (lines[lines.Length - 1].Contains("```"))
        {
            Array.Resize(ref lines, lines.Length - 1);
            outputCode = string.Join('\n', lines);
        }

        return outputCode;
    }
}