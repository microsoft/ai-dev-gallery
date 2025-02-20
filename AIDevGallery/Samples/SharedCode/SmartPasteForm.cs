// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Samples.SharedCode;

internal sealed partial class SmartPasteForm : Control
{
    private IChatClient? model;
    private List<string>? fieldLabels;
    private ProgressRing pasteProgressRing;
    public ObservableCollection<FormField> Fields { get; } = [];
    public List<string>? FieldLabels
    {
        get => (List<string>)GetValue(FieldLabelsProperty);
        set => SetValue(FieldLabelsProperty, value);
    }

    public IChatClient Model
    {
        get => (IChatClient)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private readonly string _systemPrompt = @"
You parse clumped text content to matching labels.
You will receive input in the following format:
{ ""labels"": [list of label strings], ""text"": ""arbitrary text content"" }.
You must try match subsets of the text content to the appropriate label and return it in this format: 
{ ""label1"": ""matching text"", ""label2"": ""matching text"", ""label3"": ""matching text"" }
An example input would be {""labels"": [""Name"", ""Zip"", ""Email""], ""text"": ""John Smith 94108 j.smith@gmail.com""}
And the corresponding output would be {""Name"": ""John Smith"", ""Zip"": ""94108"", ""Email"": ""j.smith@gmail.com""}
Rules: 
1. Don't make any variations on the output format. 
2. Respond with the requested output format and nothing else.
3. If you can't find a proper match for a label, exclude the label from the final results but include any other matches.
4. Always add the opening and closing braces ({ and }).
5. DO NOT PROVIDE ANY EXPLANATION OF YOUR ANSWER NO MATTER WHAT.";

    public SmartPasteForm()
    {
        this.DefaultStyleKey = typeof(SmartPasteForm);
        pasteProgressRing = new ProgressRing();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        pasteProgressRing = (ProgressRing)GetTemplateChild("PasteProgressRing");
        if (GetTemplateChild("SmartPasteButton") is Button smartPasteButton)
        {
            smartPasteButton.Click += SmartPasteButton_Click;
        }
    }

    private static void OnFieldLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        List<string> labels = (List<string>)e.NewValue;
        if (labels != null)
        {
            SmartPasteForm form = (SmartPasteForm)d;
            form.fieldLabels = labels;
            form.Fields.Clear();
            foreach (string label in labels)
            {
                form.Fields.Add(new FormField { Label = label, Value = string.Empty });
            }
        }
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        IChatClient model = (IChatClient)e.NewValue;
        if (model != null)
        {
            SmartPasteForm form = (SmartPasteForm)d;
            form.model = model;
        }
    }

    private async Task<Dictionary<string, string>> InferPasteValues(string clipboardText)
    {
        if (model == null)
        {
            return [];
        }

        SampleInteractionEvent.SendSampleInteractedEvent(model, Models.ScenarioType.SmartControlsSmartPaste, "InferPasteValues"); // <exclude-line>
        string outputMessage = string.Empty;
        PromptInput input = new()
        {
            Labels = fieldLabels,
            Text = clipboardText.Length > GenAIModel.DefaultMaxLength ?
                clipboardText[..GenAIModel.DefaultMaxLength] :
                clipboardText
        };

        CancellationTokenSource cts = new();
        string output = string.Empty;

        await foreach (var messagePart in model.GetStreamingResponseAsync(
            [
                new ChatMessage(ChatRole.System, _systemPrompt),
                new ChatMessage(ChatRole.User, JsonSerializer.Serialize(input, SmartPasteSourceGenerationContext.Default.PromptInput))
            ],
            null,
            cts.Token))
        {
            outputMessage += messagePart;

            Match match = Regex.Match(outputMessage, "{([^}]*)}", RegexOptions.Multiline);
            if (match.Success)
            {
                output = match.Value;
                cts.Cancel();
                break;
            }
        }

        cts.Dispose();

        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(output, SmartPasteSourceGenerationContext.Default.DictionaryStringString) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string> GetTextFromClipboard()
    {
        DataPackageView clipboardContent = Clipboard.GetContent();
        string textClipboardContent = string.Empty;

        if (clipboardContent.Contains(StandardDataFormats.Text))
        {
            textClipboardContent = await clipboardContent.GetTextAsync();
        }

        return textClipboardContent;
    }

    private async void SmartPasteButton_Click(object sender, RoutedEventArgs e)
    {
        ((Button)sender).IsEnabled = false;
        pasteProgressRing.IsActive = true;
        string clipboardText = await GetTextFromClipboard();
        _ = Task.Run(async () =>
        {
            Dictionary<string, string> pasteValues = await InferPasteValues(clipboardText);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                PasteValuesToForm(pasteValues);
                pasteProgressRing.IsActive = false;
                ((Button)sender).IsEnabled = true;
            });
        });
    }

    private void PasteValuesToForm(Dictionary<string, string> values)
    {
        foreach (FormField field in Fields)
        {
            field.Value = string.Empty;

            if (field.Label != null && values.TryGetValue(field.Label, out string? value))
            {
                field.Value = value;
            }
        }
    }

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model),
        typeof(IChatClient),
        typeof(SmartPasteForm),
        new PropertyMetadata(default(IChatClient), new PropertyChangedCallback(OnModelChanged)));

    public static readonly DependencyProperty FieldLabelsProperty = DependencyProperty.Register(
        nameof(FieldLabels),
        typeof(List<string>),
        typeof(SmartPasteForm),
        new PropertyMetadata(default(List<string>), new PropertyChangedCallback(OnFieldLabelsChanged)));
}

internal class FormField : ObservableObject
{
    private string? label;
    private string? value;
    public string? Label
    {
        get => label;
        set => SetProperty(ref label, value);
    }

    public string? Value
    {
        get => this.value;
        set => SetProperty(ref this.value, value);
    }
}

internal class PromptInput
{
    public List<string>? Labels { get; set; }
    public string? Text { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(PromptInput))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SmartPasteSourceGenerationContext : JsonSerializerContext
{
}