// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AI.Generative;
using System;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.WCRAPIs;
[GallerySample(
    Name = "Generate with Phi Silica",
    Model1Types = [ModelType.PhiSilica],
    Id = "21f2c4a5-3d8e-4b7a-9c0f-6d2e5f3b1c8d",
    Scenario = ScenarioType.TextGenerateText,
    SharedCode = [SharedCodeEnum.WcrModelDownloaderCs, SharedCodeEnum.WcrModelDownloaderXaml],
    Icon = "\uEE6F")]
internal sealed partial class PhiSilicaBasic : BaseSamplePage
{
    private const int MaxLength = 1000;
    private bool _isProgressVisible;
    private LanguageModel? _languageModel;

    public PhiSilicaBasic()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        sampleParams.ShowWcrModelLoadingMessage = true;
        if (LanguageModel.IsAvailable())
        {
            WcrModelDownloader.State = WcrApiDownloadState.Downloaded;
            _ = GenerateText(InputTextBox.Text);
        }

        sampleParams.NotifyCompletion();
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        var operation = ImageDescriptionGenerator.MakeAvailableAsync();

        if (await WcrModelDownloader.SetDownloadOperation(operation))
        {
            _ = GenerateText(InputTextBox.Text);
        }
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

    public async Task GenerateText(string prompt)
    {
        GenerateTextBlock.Text = string.Empty;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        IsProgressVisible = true;

        _languageModel ??= await LanguageModel.CreateAsync();

        var operation = _languageModel.GenerateResponseWithProgressAsync(prompt);
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

                // </exclude>
                if (_isProgressVisible)
                {
                    StopBtn.Visibility = Visibility.Visible;
                    IsProgressVisible = false;
                }

                GenerateTextBlock.Text = asyncInfo.GetResults().Response;
            });
        };

        var result = await operation;

        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
        StopBtn.Visibility = Visibility.Collapsed;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            _ = GenerateText(InputTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0)
            {
                _ = GenerateText(InputTextBox.Text);
            }
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
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

            GenerateButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }
}