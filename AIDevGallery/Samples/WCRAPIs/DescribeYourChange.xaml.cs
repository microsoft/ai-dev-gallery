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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Windows AI Describe Your Change",
    Model1Types = [ModelType.TextRewriter],
    Id = "a7d4e8f3-2b6c-4f9a-b1e5-3c7d9a8e5f2b",
    Scenario = ScenarioType.TextWinAiDescribeYourChange,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEF15")]
internal sealed partial class DescribeYourChange : BaseSamplePage
{
    private const int MaxLength = 5000;
    private bool _isProgressVisible;
    private LanguageModel? _languageModel;
    private TextRewriter? _textRewriter;
    private CancellationTokenSource? _cts;

    public DescribeYourChange()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        // const string featureId = "com.microsoft.windows.ai.languagemodel";

        // IMPORTANT!!
        // This is a demo LAF Token and PublisherId, and they cannot be used for production code and won't be accepted in the Store
        // Please go to https://aka.ms/laffeatures to learn more and request a token for your app
        var demoToken = LimitedAccessFeaturesHelper.GetAiLanguageModelToken();
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
                    return;
                }
            }

            _languageModel = await LanguageModel.CreateAsync();
            if (_languageModel == null)
            {
                ShowException(null, "Phi-Silica is not available.");
                return;
            }

            _textRewriter = new TextRewriter(_languageModel);
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

    public async Task RewriteTextCustom(string prompt, string customTone)
    {
        if (_textRewriter == null)
        {
            return;
        }

        RewriteTextBlock.Text = string.Empty;
        RewriteButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        CustomToneTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        IsProgressVisible = true;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        // <exclude>
        ShowDebugInfo(null);
        var swEnd = Stopwatch.StartNew();
        var swTtft = Stopwatch.StartNew();
        int outputTokens = 0;

        // </exclude>
        var operation = _textRewriter.RewriteCustomAsync(prompt, customTone);
        operation.Progress = (asyncInfo, delta) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // <exclude>
                if (!contentStartedBeingGenerated)
                {
                    NarratorHelper.Announce(InputTextBox, "Content has started generating.", "GeneratedAnnouncementActivityId");
                    contentStartedBeingGenerated = true;
                }

                if (outputTokens == 0)
                {
                    swTtft.Stop();
                }

                outputTokens++;
                double currentTps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
                ShowDebugInfo($"{Math.Round(currentTps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

                // </exclude>
                if (_isProgressVisible)
                {
                    StopBtn.Visibility = Visibility.Visible;
                    IsProgressVisible = false;
                }

                RewriteTextBlock.Text += delta;
                if (_cts?.IsCancellationRequested == true)
                {
                    operation.Cancel();
                }
            });
        };

        var result = await operation;

        // <exclude>
        swEnd.Stop();
        double tps = outputTokens / Math.Max(swEnd.Elapsed.TotalSeconds - swTtft.Elapsed.TotalSeconds, 1e-6);
        ShowDebugInfo($"{Math.Round(tps)} tokens per second\n{outputTokens} tokens used\n{swTtft.Elapsed.TotalSeconds:0.00}s to first token\n{swEnd.Elapsed.TotalSeconds:0.00}s total");

        // </exclude>
        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
        StopBtn.Visibility = Visibility.Collapsed;
        RewriteButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        CustomToneTextBox.IsEnabled = true;
        _cts?.Dispose();
        _cts = null;
    }

    private void RewriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0 && this.CustomToneTextBox.Text.Length > 0)
        {
            _ = RewriteTextCustom(InputTextBox.Text, CustomToneTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0 && CustomToneTextBox.Text.Length > 0)
            {
                _ = RewriteTextCustom(InputTextBox.Text, CustomToneTextBox.Text);
            }
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        RewriteButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        CustomToneTextBox.IsEnabled = true;
        _cts?.Cancel();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGeneration();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateRewriteButtonState();
    }

    private void CustomToneBox_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateRewriteButtonState();
    }

    private void UpdateRewriteButtonState()
    {
        var inputLength = InputTextBox.Text.Length;
        var toneLength = CustomToneTextBox.Text.Length;

        // Update InputTextBox description
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
        }
        else
        {
            InputTextBox.Description = string.Empty;
        }

        // Enable button only if both fields have valid text
        RewriteButton.IsEnabled = inputLength > 0 && inputLength <= MaxLength &&
                                  toneLength > 0 && toneLength <= MaxLength;
    }
}