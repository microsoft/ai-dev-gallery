// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Erase Object",
    Model1Types = [ModelType.ObjectRemover],
    Scenario = ScenarioType.ImageMagicEraser,
    Id = "de3d6919-5f2a-431e-ac19-3411d13e7d9b",
    AssetFilenames = [
        "pose_default.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class MagicEraser : BaseSamplePage
{
    private SoftwareBitmap? _inputBitmap;
    private SoftwareBitmap? _maskBitmap;
    private SoftwareBitmap? _originalBitmap;
    private bool _isSelectionEnabled = true;
    private bool _isDragging;

    public MagicEraser()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageObjectRemover.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.EnsureNeeded)
        {
            if (readyState == AIFeatureReadyState.EnsureNeeded)
            {
                var operation = await ImageObjectRemover.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    // TODO: handle error
                }
            }

            _ = LoadDefaultImage();
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            ShowException(null, $"Background Remover is not available: {msg}");
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

    private async Task SetImage(string filePath)
    {
        if (File.Exists(filePath))
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

    private async void EraseObject_Click(object sender, RoutedEventArgs e)
    {
        if (_inputBitmap == null)
        {
            return;
        }

        var eraser = await ImageObjectRemover.CreateAsync();
        var outputBitmap = eraser.RemoveFromSoftwareBitmap(_inputBitmap, _maskBitmap);
        if (outputBitmap != null)
        {
            _originalBitmap = _inputBitmap;
            await SetImageSource(CanvasImage, outputBitmap);
            SwitchInputOutputView(false);
        }
    }

    private void CanvasImage_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        CanvasImage.CapturePointer(e.Pointer);
        if (InputImageRectangle.Visibility == Visibility.Collapsed)
        {
            InputImageRectangle.Visibility = Visibility.Visible;
        }

        Point clickPosition = e.GetCurrentPoint(CanvasImage).Position;
        Canvas.SetTop(InputImageRectangle, clickPosition.Y);
        Canvas.SetLeft(InputImageRectangle, clickPosition.X);
        InputImageRectangle.Width = 0;
        InputImageRectangle.Height = 0;
        _isDragging = true;
    }

    private void CanvasImage_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            Point clickPosition = e.GetCurrentPoint(CanvasImage).Position;
            double offsetX = clickPosition.X - Canvas.GetLeft(InputImageRectangle);
            double offsetY = clickPosition.Y - Canvas.GetTop(InputImageRectangle);
            if (offsetX < 0 || offsetY < 0)
            {
                return;
            }

            InputImageRectangle.Width = offsetX;
            InputImageRectangle.Height = offsetY;
        }
    }

    private async void CanvasImage_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        if (_inputBitmap != null)
        {
            var ratioX = CanvasImage.ActualWidth / _inputBitmap.PixelWidth;
            var ratioY = CanvasImage.ActualHeight / _inputBitmap.PixelHeight;
            var offSetX = DrawCanvas.ActualWidth > CanvasImage.ActualWidth ? (DrawCanvas.ActualWidth - CanvasImage.ActualWidth) / 2 : 0;
            var offSetY = DrawCanvas.ActualHeight > CanvasImage.ActualHeight ? (DrawCanvas.ActualHeight - CanvasImage.ActualHeight) / 2 : 0;
            var x = (int)((Canvas.GetLeft(InputImageRectangle) - offSetX) / ratioX);
            var y = (int)((Canvas.GetTop(InputImageRectangle) - offSetY) / ratioY);
            var width = (int)(InputImageRectangle.Width / ratioX);
            if (x + width > _inputBitmap.PixelWidth)
            {
                width = _inputBitmap.PixelWidth - x;
            }

            var height = (int)(InputImageRectangle.Height / ratioY);
            if (y + height > _inputBitmap.PixelHeight)
            {
                height = _inputBitmap.PixelHeight - y;
            }

            var rect = new RectInt32(x, y, width, height);

            _maskBitmap = CreateMaskFromForegroundRect(_inputBitmap.PixelWidth, _inputBitmap.PixelHeight, rect);
            await SetImageSource(CanvasImage, _maskBitmap);
        }

        DrawCanvas.ReleasePointerCapture(e.Pointer);
    }

    private SoftwareBitmap CreateMaskFromForegroundRect(int width, int height, RectInt32 rect)
    {
        byte[] bitmapBuffer = new byte[width * height]; // Gray image hence 1-Byte per pixel.

        for (var row = rect.Y; row < rect.Y + rect.Height; row++)
        {
            for (var col = rect.X; col < rect.X + rect.Width; col++)
            {
                bitmapBuffer[row * width + col] = 255;
            }
        }

        SoftwareBitmap bitmap = new SoftwareBitmap(BitmapPixelFormat.Gray8, width, height, BitmapAlphaMode.Ignore);
        bitmap.CopyFromBuffer(bitmapBuffer.AsBuffer());
        return bitmap;
    }

    private void SwitchInputOutputView(bool isInputEnabled)
    {
        _isSelectionEnabled = isInputEnabled;
        InputImageRectangle.Visibility = Visibility.Collapsed;
        RevertButton.Visibility = isInputEnabled ? Visibility.Collapsed : Visibility.Visible;
        EraseObjectButton.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
        ClearSelectionButton.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
        InstructionText.Visibility = isInputEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_originalBitmap != null)
        {
            _inputBitmap = _originalBitmap;
            await SetImageSource(CanvasImage, _inputBitmap);
        }

        SwitchInputOutputView(true);
    }

    private void CleanSelection_Click(object sender, RoutedEventArgs e)
    {
        InputImageRectangle.Visibility = Visibility.Collapsed;
    }
}