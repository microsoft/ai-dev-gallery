// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Enhance Image WCR",
    Model1Types = [ModelType.ImageScaler],
    Scenario = ScenarioType.ImageIncreaseFidelity,
    Id = "f1e235d1-f1c9-41c7-b489-7e4f95e54668",
    NugetPackageReferences = [
        "CommunityToolkit.WinUI.Controls.Sizers"
    ],
    AssetFilenames = [
        "Enhance.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class IncreaseFidelity : BaseSamplePage
{
    private ImageScaler? _imageScaler;
    private SoftwareBitmap? _originalImage;
    public IncreaseFidelity()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageScaler.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
        {
            if (readyState == AIFeatureReadyState.EnsureNeeded)
            {
                var operation = await ImageScaler.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, "Image Scaler is not available.");
                }
            }

            if (ImageScaler.GetReadyState() == AIFeatureReadyState.Ready)
            {
                _ = LoadDefaultImage();
            }
            else
            {
                ShowException(null, "Image Scaler is not available.");
            }
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"Image Enhancer is not available: {msg}");
        }

        sampleParams.NotifyCompletion();
    }

    private async Task LoadDefaultImage()
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "Enhance.png"));
        using var stream = await file.OpenReadAsync();
        await SetImage(stream);
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
        SendSampleInteractedEvent("PasteImageClick");
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
        try
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            _originalImage = await decoder.GetSoftwareBitmapAsync();
            OptionsPanel.Visibility = Visibility.Visible;
            OriginalImage.Visibility = Visibility.Visible;
            await SetImageSource(OriginalImage, _originalImage, OriginalDimensionsTxt);
            ScaleImage();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private async void ScaleImage()
    {
        if (_originalImage == null)
        {
            return;
        }

        if (_imageScaler == null)
        {
            try
            {
                _imageScaler = await ImageScaler.CreateAsync();
                ScaleSlider.Maximum = _imageScaler.MaxSupportedScaleFactor;
                if (_imageScaler.MaxSupportedScaleFactor >= 4)
                {
                    ScaleSlider.Value = 4;
                }
            }
            catch (Exception ex)
            {
                ShowException(ex, "Failed to create Image Scaler session.");
                return;
            }
        }

        ScaledDimensionsPanel.Visibility = Visibility.Collapsed;
        ScaledImage.Visibility = Visibility.Collapsed;
        GridSplitter.Visibility = Visibility.Collapsed;
        Loader.Visibility = Visibility.Visible;

        var newWidth = (int)(_originalImage.PixelWidth * ScaleSlider.Value);
        var newHeight = (int)(_originalImage.PixelHeight * ScaleSlider.Value);
        SoftwareBitmap? bitmap;
        try
        {
            bitmap = await Task.Run(() =>
            {
                return _imageScaler.ScaleSoftwareBitmap(_originalImage, newWidth, newHeight);
            });
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to scale image.");
            return;
        }

        Loader.Visibility = Visibility.Collapsed;
        ScaledImage.Visibility = Visibility.Visible;
        GridSplitter.Visibility = Visibility.Visible;
        ScaledDimensionsPanel.Visibility = Visibility.Visible;
        if (bitmap != null)
        {
            await SetImageSource(ScaledImage, bitmap, ScaledDimensionsTxt);
        }
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap, Run textBlock)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        image.Source = bitmapSource;
        textBlock.Text = softwareBitmap.PixelWidth + " x " + softwareBitmap.PixelHeight;
    }

    private void ScaleButton_Click(object sender, RoutedEventArgs e)
    {
        ScaleImage();
    }
}