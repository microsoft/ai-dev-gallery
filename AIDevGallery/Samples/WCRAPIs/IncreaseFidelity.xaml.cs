// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Enhance Image WCR",
    Model1Types = [ModelType.ImageScaler],
    Scenario = ScenarioType.ImageIncreaseFidelity,
    Id = "f1e235d1-f1c9-41c7-b489-7e4f95e54668",
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
        if (!ImageScaler.IsAvailable())
        {
            sampleParams.ShowWcrModelLoadingMessage = true;
            var loadResult = await ImageScaler.MakeAvailableAsync();
            if (loadResult.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                throw new InvalidOperationException(loadResult.ExtendedError.Message);
            }
        }

        _imageScaler = await ImageScaler.CreateAsync();
        ScaleSlider.Maximum = _imageScaler.MaxSupportedScaleFactor;
        if (_imageScaler.MaxSupportedScaleFactor >= 2)
        {
            ScaleSlider.Value = 2;
        }

        sampleParams.NotifyCompletion();
    }

    private async void LoadImage_Click(object sender, RoutedEventArgs e)
    {
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
            SetImage(stream);
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();

            IRandomAccessStream stream = await streamRef.OpenReadAsync();
            SetImage(stream);
        }
    }

    private async void SetImage(IRandomAccessStream stream)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        _originalImage = await decoder.GetSoftwareBitmapAsync();
        OptionsPanel.Visibility = Visibility.Visible;
        OriginalPanel.Visibility = Visibility.Visible;
        await SetImageSource(OriginalImage, _originalImage, OriginalDimensionsTxt);
        ScaleImage();
    }

    private async void ScaleImage()
    {
        if (_imageScaler != null && _originalImage != null)
        {
            ScaledPanel.Visibility = Visibility.Collapsed;
            Loader.Visibility = Visibility.Visible;
            var newWidth = (int)(_originalImage.PixelWidth * ScaleSlider.Value);
            var newHeight = (int)(_originalImage.PixelHeight * ScaleSlider.Value);
            var bitmap = _imageScaler.ScaleSoftwareBitmap(_originalImage, newWidth, newHeight);
            Loader.Visibility = Visibility.Collapsed;
            ScaledPanel.Visibility = Visibility.Visible;
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