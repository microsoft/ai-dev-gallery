// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/*
using Windows.ApplicationModel;
*/

namespace AIDevGallery.Samples.WCRAPIs;
[GallerySample(
    Name = "Windows AI Text To Table Converter",
    Model1Types = [ModelType.TextToTableConverter],
    Id = "ff611809-aa53-47e1-a5d9-5210f01b2e3d",
    Scenario = ScenarioType.TextWinAiTextToTable,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE56")]
internal sealed partial class TextToTable : BaseSamplePage
{
    private const int MaxLength = 5000;
    private bool _isProgressVisible;
    private LanguageModel? _languageModel;
    private TextToTableConverter? _textToTableConverter;
    private CancellationTokenSource? _cts;

    public TextToTable()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        // const string featureId = "com.microsoft.windows.ai.languagemodel";

        // IMPORTANT!!
        // This is a demo LAF Token and PublisherId cannot be used for production code and won't be accepted in the Store
        // Please go to https://aka.ms/laffeatures to learn more and request a token for your app
        // var demoToken = "Zv6LUQWEwhJTahzvwSGjHQ=="
        var demoToken = LimitedAccessFeaturesHelper.GetAiLanguageModelToken();

        // var demoPublisherId = "z0sq19pdabnaj";
        var demoPublisherId = LimitedAccessFeaturesHelper.GetAiLanguageModelPublisherId();

        /*
        var limitedAccessFeatureResult = LimitedAccessFeatures.TryUnlockFeature(
            featureId,
            demoToken,
            $"{demoPublisherId} has registered their use of {featureId} with Microsoft and agrees to the terms of use.");

        if ((limitedAccessFeatureResult.Status != LimitedAccessFeatureStatus.Available) && (limitedAccessFeatureResult.Status != LimitedAccessFeatureStatus.AvailableWithoutToken))
        {
            ShowException(null, $"Phi-Silica is not available: Limited Access Feature not available (Status: {limitedAccessFeatureResult.Status})");
            sampleParams.NotifyCompletion();
            return;
        }
        */

        var readyState = LanguageModel.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await LanguageModel.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, $"Phi-Silica is not available");
                }
            }

            _languageModel = await LanguageModel.CreateAsync();
            if (_languageModel == null)
            {
                ShowException(null, "Phi-Silica is not available.");
                return;
            }

            _textToTableConverter = new TextToTableConverter(_languageModel);
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"Phi-Silica is not available: {msg}");
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
        _languageModel?.Dispose();
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set
        {
            _isProgressVisible = value;
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopIcon.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    public async Task ConvertText(string prompt)
    {
        if (_textToTableConverter == null)
        {
            return;
        }

        TableRepeater.ItemsSource = null;
        Header.ItemsSource = null;
        ConvertButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        IsProgressVisible = true;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var result = await _textToTableConverter.ConvertAsync(prompt).AsTask(_cts.Token);

        if (result.Status == LanguageModelResponseStatus.Complete)
        {
            var rows = result.GetRows().Select(r => new ObservableCollection<string>(r.GetColumns()));
            Header.ItemsSource = rows.FirstOrDefault();
            TableRepeater.ItemsSource = rows.Skip(1).ToList();
        }

        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
        StopBtn.Visibility = Visibility.Collapsed;
        ConvertButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Dispose();
        _cts = null;
    }

    private void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            _ = ConvertText(InputTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0)
            {
                _ = ConvertText(InputTextBox.Text);
            }
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        ConvertButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Cancel();
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
            if (inputLength >= MaxLength)
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}";
            }

            ConvertButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            ConvertButton.IsEnabled = false;
        }
    }
}