// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Management.Deployment;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "ImageScaler",
    Model1Types = [ModelType.ImageScaler],
    Scenario = ScenarioType.ImageIncreaseFidelity,
    Id = "f1e235d1-f1c9-41c7-b489-7e4f95e54668",
    NugetPackageReferences = ["CommunityToolkit.Mvvm"],
    Icon = "\uEE6F")]
internal sealed partial class IncreaseFidelity : BaseSamplePage, INotifyPropertyChanged
{
    private ImageScaler? _imageScaler;

    public event PropertyChangedEventHandler? PropertyChanged;

    private double ImageScale
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageScale)));
        }
    }

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
        ImageScale = 2;
        ScaleSlider.Maximum = _imageScaler.MaxSupportedScaleFactor;
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
            await GetBitmapAndScaleImageFromStream(stream);
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
            await GetBitmapAndScaleImageFromStream(stream);
        }
        else if (package.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await package.GetStorageItemsAsync();
            if (SharedCode.Utils.IsImageFile(storageItems[0].Path))
            {
                try
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(storageItems[0].Path);
                    using var stream = await storageFile.OpenReadAsync();
                    await GetBitmapAndScaleImageFromStream(stream);
                }
                catch
                {
                    Console.WriteLine("Invalid Image File");
                }
            }
        }
    }

    private async Task GetBitmapAndScaleImageFromStream(IRandomAccessStream stream)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();

        await SetImageSource(ImageSrc, bitmap);

        var scaledImage = ScaleImage(bitmap);
        if (scaledImage != null)
        {
            await SetImageSource(ImageDst, scaledImage);
        }
    }

    private SoftwareBitmap? ScaleImage(SoftwareBitmap softwareBitmap)
    {
        if (_imageScaler == null)
        {
            return null;
        }

        var width = (int)(softwareBitmap.PixelWidth * ImageScale);
        var height = (int)(softwareBitmap.PixelHeight * ImageScale);

        var bitmap = _imageScaler.ScaleSoftwareBitmap(softwareBitmap, width, height);
        return bitmap;
    }
}