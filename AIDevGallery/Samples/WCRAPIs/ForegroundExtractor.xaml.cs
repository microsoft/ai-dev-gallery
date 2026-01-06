// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Extract Foreground",
    Model1Types = [ModelType.ForegroundExtractor],
    Scenario = ScenarioType.ImageForegroundExtractor,
    Id = "877ff911-19c9-400b-8f60-83fb8e808c20",
    AssetFilenames = [
        "horse.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class ForegroundExtractor : BaseSamplePage
{
    private ImageForegroundExtractor? _foregroundExtractor;
    private SoftwareBitmap? _inputBitmap;
    private SoftwareBitmap? _outputBitmap;

    public ForegroundExtractor()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageForegroundExtractor.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await ImageForegroundExtractor.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, $"Image Foreground Extractor is not available");
                }
            }

            _foregroundExtractor = await ImageForegroundExtractor.CreateAsync();
            _ = LoadDefaultImage();
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"Image Foreground Extractor is not available: {msg}");
        }

        sampleParams.NotifyCompletion();
    }

    private async Task LoadDefaultImage()
    {
        var file = await StorageFile.GetFileFromPathAsync(System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "horse.png"));
        using var stream = await file.OpenReadAsync();
        _inputBitmap = await GetBitmapFromStream(stream);
        await SetInputAndGeneratedOutput();
    }

    private async Task SetInputAndGeneratedOutput()
    {
        if (_inputBitmap == null)
        {
            return;
        }

        CopyButton.Visibility = Visibility.Collapsed;
        SaveButton.Visibility = Visibility.Collapsed;
        await SetImage(InputImage, _inputBitmap);
        GeneratedImage.Source = null;
        _outputBitmap = await Task.Run(() => GetForeground(_inputBitmap));
        if (_outputBitmap != null)
        {
            await SetImage(GeneratedImage, _outputBitmap);
            CopyButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
        }
    }

    private static async Task<SoftwareBitmap> GetBitmapFromStream(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
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
            _inputBitmap = await GetBitmapFromStream(stream);
            await SetInputAndGeneratedOutput();
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
            _inputBitmap = await GetBitmapFromStream(stream);
            await SetImage(InputImage, _inputBitmap);
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
                    _inputBitmap = await GetBitmapFromStream(stream);
                    await SetInputAndGeneratedOutput();
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

    private async Task SetImage(Image image, SoftwareBitmap? bitmap)
    {
        if (bitmap == null)
        {
            return;
        }

        if (image.Source is SoftwareBitmapSource previousSource)
        {
            previousSource.Dispose();
        }

        var convertedBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var bitmapSource = new SoftwareBitmapSource();

        await bitmapSource.SetBitmapAsync(convertedBitmap);
        image.Source = bitmapSource;
    }

    private SoftwareBitmap? GetForeground(SoftwareBitmap bitmap)
    {
        if (bitmap == null || _foregroundExtractor == null)
        {
            return null;
        }

        try
        {
            var mask = _foregroundExtractor.GetMaskFromSoftwareBitmap(bitmap);
            return ApplyMask(bitmap, mask);
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to get mask.");
            return null;
        }
    }

    private static SoftwareBitmap ApplyMask(SoftwareBitmap inputBitmap, SoftwareBitmap grayMask)
    {
        if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || grayMask.BitmapPixelFormat != BitmapPixelFormat.Gray8)
        {
            throw new ArgumentException("Input bitmap must be Bgra8 and gray mask must be Gray8");
        }

        byte[] inputBuffer = new byte[4 * inputBitmap.PixelWidth * inputBitmap.PixelHeight];
        byte[] maskBuffer = new byte[grayMask.PixelWidth * grayMask.PixelHeight];
        inputBitmap.CopyToBuffer(inputBuffer.AsBuffer());
        grayMask.CopyToBuffer(maskBuffer.AsBuffer());

        for (int y = 0; y < inputBitmap.PixelHeight; y++)
        {
            for (int x = 0; x < inputBitmap.PixelWidth; x++)
            {
                int inputIndex = (y * inputBitmap.PixelWidth + x) * 4;
                int maskIndex = y * grayMask.PixelWidth + x;

                if (maskBuffer[maskIndex] == 0)
                {
                    inputBuffer[inputIndex + 3] = 0; // Set alpha to 0 for background
                }
            }
        }

        var segmentedBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, inputBitmap.PixelWidth, inputBitmap.PixelHeight);
        segmentedBitmap.CopyFromBuffer(inputBuffer.AsBuffer());
        return segmentedBitmap;
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_outputBitmap == null)
        {
            return;
        }

        SendSampleInteractedEvent("CopyImage"); // <exclude-line>

        try
        {
            var toCopy = SoftwareBitmap.Convert(_outputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(toCopy);
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
        if (_outputBitmap == null)
        {
            return;
        }

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(new Window());
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedFileName = "image.png";
        picker.FileTypeChoices.Add("PNG", [".png"]);

        StorageFile file = await picker.PickSaveFileAsync();

        if (file != null && GeneratedImage.Source != null)
        {
            SendSampleInteractedEvent("SaveFile"); // <exclude-line>
            try
            {
                using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var toSave = SoftwareBitmap.Convert(_outputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                encoder.SetSoftwareBitmap(toSave);
                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                ShowException(ex, "Failed to save image.");
            }
        }
    }
}