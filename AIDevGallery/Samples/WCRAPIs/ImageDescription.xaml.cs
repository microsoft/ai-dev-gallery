// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI.Generative;
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
    Name = "Describe Image WCR",
    Model1Types = [ModelType.ImageDescription],
    Scenario = ScenarioType.ImageDescribeImage,
    Id = "a1b1f64f-bc57-41a3-8fb3-ac8f1536d757",
    SharedCode = [SharedCodeEnum.WcrModelDownloaderCs, SharedCodeEnum.WcrModelDownloaderXaml],
    Icon = "\uEE6F")]

internal sealed partial class ImageDescription : BaseSamplePage
{
    private ImageDescriptionGenerator? _imageDescriptor;

    public ImageDescription()
    {
        this.InitializeComponent();
    }

    protected override Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (!ImageDescriptionGenerator.IsAvailable())
        {
            WcrModelDownloader.State = WcrApiDownloadState.NotStarted;
            _ = WcrModelDownloader.SetDownloadOperation(ModelType.ImageDescription, sampleParams.SampleId, ImageDescriptionGenerator.MakeAvailableAsync); // <exclude-line>
        }

        // <exclude>
        else
        {
            _ = LoadDefaultImage();
        }

        // </exclude>
        sampleParams.NotifyCompletion();
        return Task.CompletedTask;
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        var operation = ImageDescriptionGenerator.MakeAvailableAsync();

        var success = await WcrModelDownloader.SetDownloadOperation(operation);

        // <exclude>
        if (success)
        {
            await LoadDefaultImage();
        }
    }

    private async Task LoadDefaultImage()
    {
        var file = await StorageFile.GetFileFromPathAsync(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + "\\Assets\\team.jpg");
        using var stream = await file.OpenReadAsync();
        await SetImage(stream);

        // </exclude>
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

            using var stream = await streamRef.OpenReadAsync();
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
                catch
                {
                    Console.WriteLine("Invalid Image File");
                }
            }
        }
    }

    private static bool IsImageFile(string fileName)
    {
        string[] imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        return imageExtensions.Contains(Path.GetExtension(fileName)?.ToLowerInvariant());
    }

    private async Task SetImage(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        SoftwareBitmap inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        if (inputBitmap == null)
        {
            return;
        }

        ResponseTxt.Text = string.Empty;
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        ImageSrc.Source = bitmapSource;
        DescribeImage(inputBitmap);
    }

    private async void DescribeImage(SoftwareBitmap bitmap)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            Loader.Visibility = Visibility.Visible;
            OutputTxt.Visibility = Visibility.Collapsed;
        });

        var isFirstWord = true;
        try
        {
            using var bitmapBuffer = ImageBuffer.CreateCopyFromBitmap(bitmap);
            _imageDescriptor ??= await ImageDescriptionGenerator.CreateAsync();
            var describeTask = _imageDescriptor.DescribeAsync(bitmapBuffer);
            if (describeTask != null)
            {
                describeTask.Progress += (asyncInfo, delta) =>
                {
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (isFirstWord)
                        {
                            Loader.Visibility = Visibility.Collapsed;
                            OutputTxt.Visibility = Visibility.Visible;
                            isFirstWord = false;
                        }

                        ResponseTxt.Text = delta;
                    });
                };

                await describeTask;
            }
        }
        catch (Exception ex)
        {
            ResponseTxt.Text = ex.Message;
        }

        Loader.Visibility = Visibility.Collapsed;
    }
}