// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
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
    Name = "Remove Background",
    Model1Types = [ModelType.BackgroundRemover],
    Scenario = ScenarioType.ImageBackgroundRemover,
    Id = "79eca6f0-3092-4b6f-9a81-94a2aff22559",
    SharedCode = [
        SharedCodeEnum.WcrModelDownloaderCs,
        SharedCodeEnum.WcrModelDownloaderXaml
    ],
    AssetFilenames = [
        "pose_default.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class BackgroundRemover : BaseSamplePage
{
    private readonly List<PointInt32> _selectionPoints = new();
    private SoftwareBitmap? _inputBitmap;
    private SoftwareBitmap? _originalBitmap;
    private bool _isSelectionEnabled = true;

    public BackgroundRemover()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (ImageObjectExtractor.IsAvailable())
        {
            WcrModelDownloader.State = WcrApiDownloadState.Downloaded;
        }

        await SetImage(System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "pose_default.png"));
        sampleParams.NotifyCompletion();
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        var operation = ImageObjectExtractor.MakeAvailableAsync();

        await WcrModelDownloader.SetDownloadOperation(operation);
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

    private async Task SetImage(string filePath)
    {
        if(File.Exists(filePath))
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
            await SetImage(stream);
        }
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        CanvasImage.Source = bitmapSource;
        CanvasImage.Width = _inputBitmap!.PixelWidth;
        CanvasImage.Height = _inputBitmap.PixelHeight;
    }

    private async Task<SoftwareBitmap?> ExtractBackground(SoftwareBitmap bitmap, IList<PointInt32> includePoints)
    {
        if (_inputBitmap == null)
        {
            return null;
        }

        var extractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(bitmap);
        var mask = extractor.GetSoftwareBitmapObjectMask(new ImageObjectExtractorHint([], includePoints, []));
        return ApplyMask(bitmap, mask);
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

    private void ClearSelectionPoints()
    {
        _selectionPoints.Clear();
        InstructionText.Text = "Click on the image to select objects you wish to be included after background removal. 0 of 31 maximum points currently selected.";
        PointsCanvas.Children.Clear();
        ClearSelectionButton.IsEnabled = false;
    }

    private void CleanSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionPoints();
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

    private void CanvasImage_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_inputBitmap == null || _selectionPoints.Count >= 31 || !_isSelectionEnabled)
        {
            RemoveBackgroundButton.IsEnabled = false;
            return;
        }

        RemoveBackgroundButton.IsEnabled = true;
        var pointerPosition = e.GetCurrentPoint(CanvasImage).Position;

        var circle = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(Colors.Red)
        };

        Canvas.SetLeft(circle, pointerPosition.X - circle.Width / 2);
        Canvas.SetTop(circle, pointerPosition.Y - circle.Height / 2);
        PointsCanvas.Children.Add(circle);
        _selectionPoints.Add(new PointInt32((int)pointerPosition.X, (int)pointerPosition.Y));
        InstructionText.Text = $"Click on the image to select objects you wish to be included after background removal. {_selectionPoints.Count} of 31 maximum points currently selected.";
        ClearSelectionButton.IsEnabled = true;
    }

    private void SwitchInputOutputView(bool isInputEnabled)
    {
        _isSelectionEnabled = isInputEnabled;
        ClearSelectionPoints();
        RevertButton.Visibility = isInputEnabled ? Visibility.Collapsed : Visibility.Visible;
        RemoveBackgroundButton.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
        ClearSelectionButton.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
        InstructionText.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        if(_originalBitmap != null)
        {
            _inputBitmap = _originalBitmap;
            await SetImageSource(CanvasImage, _inputBitmap);
        }

        SwitchInputOutputView(true);
    }
}