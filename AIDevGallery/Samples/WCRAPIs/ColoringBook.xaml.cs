// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
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
    Name = "Coloring Book",
    Model1Types = [ModelType.ColoringBook],
    Scenario = ScenarioType.ImageColoringBook,
    Id = "ab877fc9-b183-47be-aac5-fac2c4ab8940",
    AssetFilenames = [
        "WinDev.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class ColoringBook : BaseSamplePage
{
    private const int MaxLength = 1000;
    private SoftwareBitmap? _inputBitmap;
    private ImageGenerator? _generator;
    private ImageObjectExtractor? _extractor;
    private Stack<SoftwareBitmap> _bitmaps = new();
    private List<PointInt32> _selectionPoints = new();

    public ColoringBook()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageObjectExtractor.GetReadyState();
        if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser)
        {
            ShowException(null, "Extractor not available in this system");
            return;
        }

        if (readyState == AIFeatureReadyState.NotReady)
        {
            var operation = await ImageObjectExtractor.EnsureReadyAsync();

            if (operation.Status != AIFeatureReadyResultState.Success)
            {
                ShowException(null, "Image Extractor is not available.");
            }
        }

        readyState = ImageGenerator.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await ImageGenerator.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    ShowException(null, "Image Generator is not available.");
                }
            }

            _generator = await ImageGenerator.CreateAsync();

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
        var file = await StorageFile.GetFileFromPathAsync(System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "ColoringBook.png"));
        using var stream = await file.OpenReadAsync();
        await SetImage(stream);
        DrawPoint(880, 435);
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
        ClearSelectionPoints();
        SwitchInputOutputView();
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        CanvasImage.Source = bitmapSource;
    }

    private async void ChangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_inputBitmap == null)
        {
            return;
        }

        var outputBitmap = await ExtractBackground(_inputBitmap, _selectionPoints);
        if (outputBitmap != null)
        {
            await SetImageSource(CanvasImage, outputBitmap);
        }
    }

    private void CanvasImage_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_inputBitmap == null || _selectionPoints.Count >= 32)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(DrawCanvas);
        double canvasX = currentPoint.Position.X;
        double canvasY = currentPoint.Position.Y;

        var ratioX = CanvasImage.ActualWidth / _inputBitmap.PixelWidth;
        var ratioY = CanvasImage.ActualHeight / _inputBitmap.PixelHeight;

        var x = (int)(canvasX / ratioX);
        var y = (int)(canvasY / ratioY);
        DrawPoint(x, y);
    }

    private void DrawPoint(int x, int y, bool doAdd = true)
    {
        if (doAdd)
        {
            _selectionPoints.Add(new PointInt32(x, y));
        }

        var canvasX = x * CanvasImage.ActualWidth / _inputBitmap!.PixelWidth;
        var canvasY = y * CanvasImage.ActualHeight / _inputBitmap!.PixelHeight;
        ClearSelectionButton.IsEnabled = true;
        ChangeButton.IsEnabled = InputTextBox.Text.Length > 0;
        var ellipse = new Ellipse() { Width = 8, Height = 8, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red) };
        Canvas.SetLeft(ellipse, canvasX - 4);
        Canvas.SetTop(ellipse, canvasY - 4);
        DrawCanvas.Children.Add(ellipse);
    }

    private SoftwareBitmap CreateMaskFromRect(int width, int height, RectInt32 rect)
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

    private void SwitchInputOutputView()
    {
        RevertButton.Visibility = _bitmaps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmaps.Count > 0)
        {
            _inputBitmap = _bitmaps.Pop();
            await SetImageSource(CanvasImage, _inputBitmap);
        }

        SwitchInputOutputView();
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionPoints();
    }

    private void ClearSelectionPoints()
    {
        _selectionPoints.Clear();
        DrawCanvas.Children.Clear();
        ClearSelectionButton.IsEnabled = false;
        ChangeButton.IsEnabled = false;
    }

    private void CanvasImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCanvas.Children.Clear();
        foreach (var point in _selectionPoints)
        {
            DrawPoint(point.X, point.Y, false);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0)
            {
                // _ = GenerateImage(InputTextBox.Text);
            }
        }
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            InputTextBox.Description = inputLength >= MaxLength ?
                $"{inputLength} of {MaxLength}. Max characters reached." :
                $"{inputLength} of {MaxLength}";

            ChangeButton.IsEnabled = inputLength <= MaxLength && _selectionPoints.Count > 0;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            ChangeButton.IsEnabled = false;
        }
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

    private SoftwareBitmap? ApplyMask(SoftwareBitmap inputBitmap, SoftwareBitmap grayMask)
    {
        if (_generator == null)
        {
            ShowException(new InvalidOperationException("ImageGenerator is not initialized"));
        }

        if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || grayMask.BitmapPixelFormat != BitmapPixelFormat.Gray8)
        {
            throw new ArgumentException("Input bitmap must be Bgra8 and gray mask must be Gray8");
        }

        using var inputBuffer = ImageBuffer.CreateForSoftwareBitmap(inputBitmap);
        using var mask = ImageBuffer.CreateForSoftwareBitmap(grayMask);

        var result = _generator!.GenerateImageFromImageBufferAndMask(inputBuffer, mask, InputTextBox.Text, new Microsoft.Windows.AI.Imaging.ImageGenerationOptions());
        if (result.Status != ImageGeneratorResultStatus.Success)
        {
            if (result.Status == ImageGeneratorResultStatus.TextBlockedByContentModeration)
            {
                ShowException(null, "Image generation blocked by content moderation");
            }
            else
            {
                ShowException(null, $"Image generation failed: {result.ExtendedError.Message}");
            }

            return null;
        }

        var softwareBitmap = result.Image.CopyToSoftwareBitmap();
        return softwareBitmap;
    }
}