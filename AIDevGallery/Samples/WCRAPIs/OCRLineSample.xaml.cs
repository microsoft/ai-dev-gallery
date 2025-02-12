// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.Vision;
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Text;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Locate Detected Text",
    Model1Types = [ModelType.TextRecognitionOCR],
    Scenario = ScenarioType.ImageDetectTextLines,
    Id = "e26ef7bc-d847-4b2e-862a-74d872bb8635",
    SharedCode = [SharedCodeEnum.WcrModelDownloaderCs, SharedCodeEnum.WcrModelDownloaderXaml],
    Icon = "\uEE6F")]
internal sealed partial class OCRLineSample : BaseSamplePage
{
    private TextRecognizer? _textRecognizer;

    public OCRLineSample()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (TextRecognizer.IsAvailable())
        {
            WcrModelDownloader.State = WcrApiDownloadState.Downloaded;
        }

        sampleParams.NotifyCompletion();
    }

    private async void WcrModelDownloader_DownloadClicked(object sender, EventArgs e)
    {
        var operation = TextRecognizer.MakeAvailableAsync();

        await WcrModelDownloader.SetDownloadOperation(operation);
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
            await SetImage(stream);
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
            RectCanvas.Visibility = Visibility.Collapsed;
            var streamRef = await package.GetBitmapAsync();

            IRandomAccessStream stream = await streamRef.OpenReadAsync();
            await SetImage(stream);
        }
    }

    private async Task SetImage(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        SoftwareBitmap inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        if (inputBitmap == null)
        {
            return;
        }

        RectCanvas.Visibility = Visibility.Collapsed;
        RectCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        RectCanvas.Arrange(new Rect(new Point(0, 0), RectCanvas.DesiredSize));

        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        ImageSrc.Source = bitmapSource;
        await RecognizeAndAddTextAsync(convertedImage);
    }

    private async Task RecognizeAndAddTextAsync(SoftwareBitmap bitmap)
    {
        CopyTextButton.Visibility = Visibility.Collapsed;
        Loader.Visibility = Visibility.Visible;
        using var imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);
        _textRecognizer ??= await TextRecognizer.CreateAsync();
        RecognizedText? result = _textRecognizer?.RecognizeTextFromImage(imageBuffer, new TextRecognizerOptions());
        if (result == null)
        {
            return;
        }

        // Get the offset between the canvas and the image
        var offSetX = PaneGrid.ActualWidth > ImageSrc.ActualWidth ? (PaneGrid.ActualWidth - ImageSrc.ActualWidth) / 2 : 0;
        var offSetY = PaneGrid.ActualHeight > ImageSrc.ActualHeight ? (PaneGrid.ActualHeight - ImageSrc.ActualHeight) / 2 : 0;

        RectCanvas.Children.Clear();
        TextPanel.Children.Clear();

        foreach (var line in result.Lines)
        {
            var boundingBox = line.BoundingBox;
            var x = (int)boundingBox.TopLeft.X + offSetX;
            var y = (int)boundingBox.TopLeft.Y + offSetY;
            var xMax = (int)boundingBox.BottomRight.X + offSetX;
            var yMax = (int)boundingBox.BottomRight.Y + offSetY;
            if (xMax < x)
            {
                (xMax, x) = (x, xMax);
            }

            if (yMax < y)
            {
                (yMax, y) = (y, yMax);
            }

            var rect = new Rectangle() { Width = xMax - x, Height = yMax - y, StrokeThickness = 1, Stroke = new SolidColorBrush(Colors.Gray) };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            var xRect = boundingBox.TopLeft.X;
            var yRect = boundingBox.TopLeft.Y;
            var xMaxRect = boundingBox.BottomRight.X;
            var yMaxRect = boundingBox.BottomRight.Y;
            if (xMaxRect < xRect)
            {
                (xMaxRect, xRect) = (xRect, xMaxRect);
            }

            if (yMaxRect < yRect)
            {
                (yMaxRect, yRect) = (yRect, yMaxRect);
            }

            rect.Fill = await CropSoftwareBitmapToImageBrushAsync(bitmap, new Rect(xRect, yRect, xMaxRect - xRect, yMaxRect - yRect));
            var textLine = new TextBlock { Text = line.Text, TextWrapping = TextWrapping.Wrap, Tag = rect };
            textLine.PointerEntered += (s, e) =>
            {
                if (s is TextBlock { Tag: Rectangle rectangle })
                {
                    SelectRect(rectangle);
                }
            };
            textLine.PointerExited += (s, e) =>
            {
                if (s is TextBlock { Tag: Rectangle rectangle })
                {
                    DeselectRect(rectangle);
                }
            };
            TextPanel.Children.Add(textLine);
            rect.Tag = textLine;
            rect.PointerEntered += (s, e) =>
            {
                if (s is Rectangle rectangle)
                {
                    SelectRect(rectangle);
                }
            };
            rect.PointerExited += (s, e) =>
            {
                if (s is Rectangle rectangle)
                {
                    DeselectRect(rectangle);
                }
            };
            rect.PointerPressed += (s, e) => CopyTextToClipboard(((s as Rectangle)?.Tag as TextBlock)?.Text ?? string.Empty);
            RectCanvas.Children.Add(rect);
        }

        CopyTextButton.Visibility = Visibility.Visible;
        Loader.Visibility = Visibility.Collapsed;
        RectCanvas.Visibility = Visibility.Visible;
    }

    private static void DeselectRect(Rectangle rectangle)
    {
        if (rectangle.Tag is TextBlock textLine)
        {
            textLine.FontWeight = new FontWeight(400);
        }

        rectangle.Scale = new System.Numerics.Vector3(1, 1, 1);
        Canvas.SetZIndex(rectangle, 0);
    }

    private static void SelectRect(Rectangle rectangle)
    {
        if (rectangle.Tag is TextBlock textLine)
        {
            textLine.FontWeight = new FontWeight(700);
        }

        rectangle.Scale = new System.Numerics.Vector3(1.5f, 1.5f, 1);
        Canvas.SetZIndex(rectangle, 1);
    }

    private void CopyTextToClipboard(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var rect in RectCanvas.Children.OfType<Rectangle>())
        {
            if (rect.Tag is TextBlock textLine)
            {
                sb.AppendLine(textLine.Text);
            }
        }

        CopyTextToClipboard(sb.ToString());
    }

    public async Task<ImageBrush> CropSoftwareBitmapToImageBrushAsync(SoftwareBitmap softwareBitmap, Rect rect)
    {
        // Create a new SoftwareBitmap with the desired dimensions
        using var croppedBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)rect.Width, (int)rect.Height, BitmapAlphaMode.Premultiplied);

        using var inputStream = new InMemoryRandomAccessStream();

        // Encode the original SoftwareBitmap to a stream
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inputStream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        // Decode the stream to a new SoftwareBitmap with the cropped region
        var decoder = await BitmapDecoder.CreateAsync(inputStream);
        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds
            {
                X = (uint)rect.X,
                Y = (uint)rect.Y,
                Width = (uint)rect.Width,
                Height = (uint)rect.Height
            }
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        croppedBitmap.CopyFromBuffer(pixelData.DetachPixelData().AsBuffer());

        var writeableBitmap = new WriteableBitmap(croppedBitmap.PixelWidth, croppedBitmap.PixelHeight);
        croppedBitmap.CopyToBuffer(writeableBitmap.PixelBuffer);
        return new ImageBrush
        {
            ImageSource = writeableBitmap
        };
    }
}