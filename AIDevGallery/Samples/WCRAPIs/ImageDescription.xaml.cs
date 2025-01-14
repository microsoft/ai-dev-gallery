// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Management.Deployment;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "ImageScaler",
    Model1Types = [ModelType.ImageDescription],
    Scenario = ScenarioType.ImageDescribeImageWcr,
    Id = "a1b1f64f-bc57-41a3-8fb3-ac8f1536d757",
    NugetPackageReferences = ["CommunityToolkit.Mvvm"],
    Icon = "\uEE6F")]
[ObservableObject]
internal sealed partial class ImageDescription : BaseSamplePage
{
    private SoftwareBitmap? _inputBitmap;
    private ImageDescriptionGenerator? _imageDescriptor;

    public ImageDescription()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        sampleParams.ShowWcrModelLoadingMessage = true;
        if (!ImageDescriptionGenerator.IsAvailable())
        {
            var loadResult = await ImageDescriptionGenerator.MakeAvailableAsync();
            if (loadResult.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                throw new InvalidOperationException(loadResult.ExtendedError.Message);
            }
        }

        _imageDescriptor = await ImageDescriptionGenerator.CreateAsync();
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
            using var randomAccessStream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            _inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            await SetImageSource(ImageSrc, _inputBitmap);
            ResponseTxt.Text = string.Empty;
            DescribeImage(_inputBitmap);
        }
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        image.Source = bitmapSource;
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();

            IRandomAccessStream stream = await streamRef.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            _inputBitmap = await decoder.GetSoftwareBitmapAsync();

            await SetImageSource(ImageSrc, _inputBitmap);
            ResponseTxt.Text = string.Empty;
            DescribeImage(_inputBitmap);
        }
    }

    private async void DescribeImage(SoftwareBitmap bitmap)
    {
        if (_inputBitmap == null)
        {
            return;
        }

        DispatcherQueue?.TryEnqueue(() => LoadImageProgressRing.Visibility = Visibility.Visible);
        var isFirstWord = true;
        using var bitmapBuffer = ImageBuffer.CreateCopyFromBitmap(bitmap);
        var describeTask = _imageDescriptor.DescribeAsync(bitmapBuffer);
        describeTask.Progress += (asyncInfo, delta) =>
        {
            var result = asyncInfo.GetResults().Response;

            DispatcherQueue?.TryEnqueue(() =>
            {
                if (isFirstWord)
                {
                    LoadImageProgressRing.Visibility = Visibility.Collapsed;
                    isFirstWord = false;
                }

                ResponseTxt.Text = result;
            });
        };
        await describeTask;
        LoadImageProgressRing.Visibility = Visibility.Collapsed;
    }
}