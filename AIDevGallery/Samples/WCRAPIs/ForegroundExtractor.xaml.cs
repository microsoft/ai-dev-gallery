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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
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
        "pose_default.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class ForegroundExtractor : BaseSamplePage
{
    private readonly List<PointInt32> _selectionPoints = new();
    private SoftwareBitmap? _inputBitmap;
    private SoftwareBitmap? _originalBitmap;
    private bool _isSelectionEnabled = true;
    private ImageForegroundExtractor? _foregroundExtractor;

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
        var file = await StorageFile.GetFileFromPathAsync(System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "pose_default.png"));
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

        await SetImageSource(CanvasImage, _inputBitmap);
        SwitchInputOutputView(true);
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        CanvasImage.Source = bitmapSource;
    }

    private async Task<SoftwareBitmap?> ExtractBackground(SoftwareBitmap bitmap, IList<PointInt32> includePoints)
    {
        if (_inputBitmap == null)
        {
            return null;
        }

        try
        {
            var extractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(bitmap);
            try
            {
                var mask = extractor.GetSoftwareBitmapObjectMask(new ImageObjectExtractorHint([], includePoints, []));
                return ApplyMask(bitmap, mask);
            }
            catch (Exception ex)
            {
                ShowException(ex, "Failed to create get mask.");
                return null;
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to create ImageObjectExtractor session.");
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

    private async void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        if (_inputBitmap == null || _selectionPoints.Count == 0)
        {
            return;
        }

        var outputBitmap = await ExtractBackground(_inputBitmap, _selectionPoints);
        if (outputBitmap != null)
        {
            _originalBitmap = _inputBitmap;
            await SetImageSource(CanvasImage, outputBitmap);
            SwitchInputOutputView(false);
        }
    }

    private void SwitchInputOutputView(bool isInputEnabled)
    {
        _isSelectionEnabled = isInputEnabled;
        RemoveBackgroundButton.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
    }
}