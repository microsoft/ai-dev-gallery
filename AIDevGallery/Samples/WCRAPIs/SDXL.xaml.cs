// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

/*
using Windows.ApplicationModel;
*/

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Generate image with SDXL",
    Model1Types = [ModelType.SDXL],
    Id = "01b9a242-05bf-415a-ac25-9f74544a9c91",
    Scenario = ScenarioType.ImageGenerateImage,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]
internal sealed partial class SDXL : BaseSamplePage
{
    private const int MaxLength = 1000;
    private bool _isProgressVisible;
    private ImageGenerator? _imageModel;
    private CancellationTokenSource? _cts;
    private Task<ImageGeneratorResult?>? _generationTask;

    public SDXL()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageGenerator.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await ImageGenerator.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, $"SDXL is not available");
                }
            }

            _imageModel ??= await ImageGenerator.CreateAsync();
            _ = GenerateImage(InputTextBox.Text);
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"SDXL is not available: {msg}");
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
        _imageModel?.Dispose();
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

    public async Task GenerateImage(string prompt)
    {
        GeneratedImage.Source = null;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        var contentStartedBeingGenerated = false; // <exclude-line>
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        SendSampleInteractedEvent("GenerateText"); // <exclude-line>

        IsProgressVisible = true;
        _cts = new CancellationTokenSource();
        _generationTask = Task.Run(() =>
        {
            var result = _imageModel?.GenerateImageFromTextPrompt(prompt, new ImageGenerationOptions());
            return result;
        });

        var result = await _generationTask;
        if (result?.Status != ImageGeneratorResultStatus.Success)
        {
            if (result?.Status == ImageGeneratorResultStatus.TextBlockedByContentModeration)
            {
                ShowException(null, "Image generation blocked by content moderation");
            }
            else
            {
                ShowException(null, "Image generation failed");
            }

            return;
        }

        var softwareBitmap = result.Image.CopyToSoftwareBitmap();
        var convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        if (softwareBitmap != null)
        {
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(convertedImage);
            GeneratedImage.Source = source;
        }
        else
        {
            ShowException(null, "Failed to convert the image.");
        }

        // </exclude>
        NarratorHelper.Announce(InputTextBox, "Content has finished generating.", "GenerateDoneAnnouncementActivityId"); // <exclude-line>
        StopBtn.Visibility = Visibility.Collapsed;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Dispose();
        _cts = null;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            _ = GenerateImage(InputTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0)
            {
                _ = GenerateImage(InputTextBox.Text);
            }
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        GenerateButton.Visibility = Visibility.Visible;
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

            GenerateButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }
}