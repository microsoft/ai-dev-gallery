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
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

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
    private ImageGenerator? _generator;
    private CancellationTokenSource? _cts;

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

            _generator ??= await ImageGenerator.CreateAsync();
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
        _generator?.Dispose();
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
        CopyButton.Visibility = Visibility.Collapsed;
        SaveButton.Visibility = Visibility.Collapsed;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        ImageFromTextGenerationOptions imageFromTextGenerationOption = new()
        {
            Style = ColoringBookCheck.IsChecked == true ? ImageFromTextGenerationStyle.ColoringBook : ImageFromTextGenerationStyle.Default
        };

        SendSampleInteractedEvent("GenerateImage"); // <exclude-line>

        _cts = new CancellationTokenSource();
        ImageGeneratorResult? result = null;

        result = await Task.Run(
            () => _generator?.GenerateImageFromTextPrompt(prompt, new ImageGenerationOptions(), imageFromTextGenerationOption),
            _cts.Token);

        if (_cts?.IsCancellationRequested == true)
        {
            CancelGeneration();
            _cts?.Dispose();
            _cts = null;
            return;
        }

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

            CancelGeneration();
            _cts?.Dispose();
            _cts = null;
            return;
        }

        var softwareBitmap = result.Image.CopyToSoftwareBitmap();
        var convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        if (convertedImage != null)
        {
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(convertedImage);
            GeneratedImage.Source = source;
            NarratorHelper.AnnounceImageChanged(GeneratedImage, "Generated image has been updated."); // <exclude-line>
            CopyButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
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
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox && InputTextBox.Text.Length > 0)
        {
            _ = GenerateImage(InputTextBox.Text);
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
            InputTextBox.Description = inputLength >= MaxLength ?
                $"{inputLength} of {MaxLength}. Max characters reached." :
                $"{inputLength} of {MaxLength}";

            GenerateButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (GeneratedImage.Source == null)
        {
            return;
        }

        SendSampleInteractedEvent("CopyImage"); // <exclude-line>

        try
        {
            RenderTargetBitmap renderTargetBitmap = new();
            await renderTargetBitmap.RenderAsync(GeneratedImage);

            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();

            using InMemoryRandomAccessStream stream = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)renderTargetBitmap.PixelWidth, (uint)renderTargetBitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
            stream.Seek(0);

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to copy image to clipboard.");
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(new Window());
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedFileName = "image.png";
        picker.FileTypeChoices.Add("PNG", new List<string> { ".png" });

        StorageFile file = await picker.PickSaveFileAsync();

        if (file != null && GeneratedImage.Source != null)
        {
            SendSampleInteractedEvent("SaveFile"); // <exclude-line>
            RenderTargetBitmap renderTargetBitmap = new();
            await renderTargetBitmap.RenderAsync(GeneratedImage);

            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();

            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, fileStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)renderTargetBitmap.PixelWidth, (uint)renderTargetBitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
        }
    }
}