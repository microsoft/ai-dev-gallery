// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Semantic Search and Knowledge Retrieval",
    Model1Types = [ModelType.TextRecognitionOCR],
    Scenario = ScenarioType.ImageRecognizeText,
    Id = "F8465A45-8E23-4485-8C16-9909E96EACF6",
    AssetFilenames = [
        "OCR.png"
    ],
    Icon = "\uEE6F")]
internal sealed partial class SemanticSearch : BaseSamplePage
{
    private TextRecognizer? _textRecognizer;
    private string _recognizedTextString = string.Empty;

    public SemanticSearch()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var result = AppContentIndexer.GetOrCreateIndexerAsync("myIndex");

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to open index. Status = '{result.Status}', Error = '{result.ExtendedError}'");
        }

        // If result.Succeeded is true, result.Status will either be CreatedNew or OpenedExisting
        if (result.Status == GetOrCreateIndexStatus.CreatedNew)
        {
            Console.WriteLine("Created a new index");
        }
        else if (result.Status == GetOrCreateIndexStatus.OpenedExisting)
        {
            Console.WriteLine("Opened an existing index");
        }

        using AppContentIndexer indexer = result.Indexer;

        sampleParams.NotifyCompletion();
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
            RectCanvas.Visibility = Visibility.Collapsed;
            var streamRef = await package.GetBitmapAsync();

            IRandomAccessStream stream = await streamRef.OpenReadAsync();
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

    private async Task SetImage(string filePath)
    {
        if (File.Exists(filePath))
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
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
        ViewToggle.Visibility = Visibility.Collapsed;
        RectCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        RectCanvas.Arrange(new Rect(new Point(0, 0), RectCanvas.DesiredSize));

        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        RectCanvas.Children.Clear();
        ImageSrc.Source = bitmapSource;
        await RecognizeAndAddTextAsync(convertedImage);
    }

    private async Task RecognizeAndAddTextAsync(SoftwareBitmap bitmap)
    {
        CopyTextButton.Visibility = Visibility.Collapsed;
        RectCanvas.Visibility = Visibility.Collapsed;
        using var imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);
        _textRecognizer ??= await TextRecognizer.CreateAsync();
        RecognizedText? result = _textRecognizer?.RecognizeTextFromImage(imageBuffer);

        if (result?.Lines == null)
        {
            return;
        }

        RenderRecognizedText(result);

        CopyTextButton.Visibility = Visibility.Visible;
        RectCanvas.Visibility = Visibility.Visible;
    }

    private void CopyTextToClipboard(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        CopyTextToClipboard(_recognizedTextString);
    }

    private void RenderRecognizedText(RecognizedText recognizedText)
    {
        RectCanvas.Visibility = Visibility.Visible;
        ViewToggle.Visibility = Visibility.Visible;

        List<string> lines = new List
    <string>();

        foreach (var line in recognizedText.Lines)
        {
            lines.Add(line.Text);

            SolidColorBrush backgroundBrush = new SolidColorBrush
            {
                Color = Colors.Black,
                Opacity = .6
            };

            Grid grid = new Grid
            {
                Background = backgroundBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4, 3, 4, 4)
            };

            try
            {
                var height = Math.Abs((int)line.BoundingBox.TopRight.Y - (int)line.BoundingBox.BottomRight.Y) * .85;
                TextBlock block = new TextBlock
                {
                    IsTextSelectionEnabled = true,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = line.Text,
                    FontSize = height > 0 ? height : 1,
                };

                grid.Children.Add(block);
                RectCanvas.Children.Add(grid);
                Canvas.SetLeft(grid, line.BoundingBox.TopLeft.X);
                Canvas.SetTop(grid, line.BoundingBox.TopLeft.Y);
            }
            catch
            {
                continue;
            }
        }

        _recognizedTextString = string.Join('\n', lines);
    }

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        RectCanvas.Visibility = RectCanvas.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }
}