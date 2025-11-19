// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
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
    Name = "Restyle image with SDXL",
    Model1Types = [ModelType.RestyleImage],
    Id = "a335a19a-2f78-4f68-b5e9-982e7da13b72",
    Scenario = ScenarioType.ImageGenerateImage,
    NugetPackageReferences = [
        "Microsoft.Extensions.AI"
    ],
    Icon = "\uEE6F")]
internal sealed partial class RestyleImage : BaseSamplePage
{
    private const int MaxLength = 1000;
    private bool _isProgressVisible;
    private ImageGenerator? _imageModel;
    private CancellationTokenSource? _cts;
    private SoftwareBitmap? _inputBitmap;

    public RestyleImage()
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
            await LoadDefaultImage();
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

    private async Task LoadDefaultImage()
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "GoldenGate.png"));
        using var stream = await file.OpenReadAsync();
        await SetImage(stream);
    }

    public async Task GenerateImage(string prompt)
    {
        if (_inputBitmap == null)
        {
            return;
        }

        GeneratedImage.Source = null;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        CopyButton.Visibility = Visibility.Collapsed;
        SaveButton.Visibility = Visibility.Collapsed;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        NarratorHelper.Announce(InputTextBox, "Generating content, please wait.", "GenerateTextWaitAnnouncementActivityId"); // <exclude-line>
        ImageFromImageGenerationOptions imageFromImageGenerationOption = new()
        {
            Style = ImageFromImageGenerationStyle.Restyle
        };

        using var inputBuffer = ImageBuffer.CreateForSoftwareBitmap(_inputBitmap);
        SendSampleInteractedEvent("GenerateImage"); // <exclude-line>

        _cts = new CancellationTokenSource();

        var result = await Task.Run(
            () => _imageModel?.GenerateImageFromImageBuffer(inputBuffer, prompt, new ImageGenerationOptions(), imageFromImageGenerationOption),
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

    private async void LoadImage_Click(object sender, RoutedEventArgs e)
    {
        SendSampleInteractedEvent("LoadImageClicked");
        var window = new Window();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        var picker = new FileOpenPicker();

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".jpg");

        picker.ViewMode = PickerViewMode.Thumbnail;

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            using var stream = await file.OpenReadAsync();
            await SetImage(stream);
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        SendSampleInteractedEvent("PasteImageClicked");
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();

            using IRandomAccessStream stream = await streamRef.OpenReadAsync();
            await SetImage(stream);
        }
        else if (package.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await package.GetStorageItemsAsync();
            if (IsImageFile(storageItems[0].Path))
            {
                try
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(storageItems[0].Path);
                    using var stream = await storageFile.OpenReadAsync();
                    await SetImage(stream);
                }
                catch (Exception ex)
                {
                    ShowException(ex, "Invalid image file");
                }
            }
        }
    }

    private static bool IsImageFile(string fileName)
    {
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        return imageExtensions.Contains(System.IO.Path.GetExtension(fileName)?.ToLowerInvariant());
    }

    private async Task SetImage(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        _inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        if (_inputBitmap == null)
        {
            return;
        }

        await SetImageSource(InputImage, _inputBitmap);
    }

    private async Task SetImageSource(Microsoft.UI.Xaml.Controls.Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        InputImage.Source = bitmapSource;
    }
}