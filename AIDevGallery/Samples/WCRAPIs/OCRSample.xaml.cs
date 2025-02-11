// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using Microsoft.Graphics.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Management.Deployment;
using Microsoft.Windows.Vision;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Text Recognition (OCR)",
    Model1Types = [ModelType.TextRecognitionOCR],
    Scenario = ScenarioType.ImageDetectText,
    Id = "8f072b64-74fc-4511-b84f-e09d56394f07",
    Icon = "\uEE6F")]
internal sealed partial class OCRSample : BaseSamplePage
{
    private TextRecognizer? _textRecognizer;

    public OCRSample()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (!TextRecognizer.IsAvailable())
        {
            sampleParams.ShowWcrModelLoadingMessage = true;
            var loadResult = await TextRecognizer.MakeAvailableAsync();
            if (loadResult.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                throw new InvalidOperationException(loadResult.ExtendedError.Message);
            }
        }

        _textRecognizer = await TextRecognizer.CreateAsync();

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
            await GetBitmapAndExtractTextFromStream(stream);
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();

            IRandomAccessStream stream = await streamRef.OpenReadAsync();
            await GetBitmapAndExtractTextFromStream(stream);
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
                    await GetBitmapAndExtractTextFromStream(stream);
                }
                catch
                {
                    Console.WriteLine("Invalid Image File");
                }
            }
        }
    }

    private async Task GetBitmapAndExtractTextFromStream(IRandomAccessStream stream)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        var source = new SoftwareBitmapSource();

        // This conversion is a workaround for an error if we get the raw image from the clipboard
        SoftwareBitmap displayableImage = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await source.SetBitmapAsync(displayableImage);
        ImageSrc.Source = source;
        await RecognizeAndAddTextAsync(displayableImage);
    }

    public async Task RecognizeAndAddTextAsync(SoftwareBitmap bitmap)
    {
        if (_textRecognizer == null)
        {
            return;
        }

        OcrTextBlock.Inlines.Clear();

        using ImageBuffer imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);
        RecognizedText result = await _textRecognizer.RecognizeTextFromImageAsync(imageBuffer, new TextRecognizerOptions());

        SolidColorBrush greenBrush = new(Microsoft.UI.Colors.Green);
        SolidColorBrush yellowBrush = new(Microsoft.UI.Colors.Orange);
        SolidColorBrush redBrush = new(Microsoft.UI.Colors.Red);

        if (result.Lines == null || result.Lines.Length == 0)
        {
            OcrTextBlock.Inlines.Add(new Run { Text = "No text found." });
            return;
        }

        foreach (var line in result.Lines)
        {
            foreach (var word in line.Words)
            {
                var text = new Run
                {
                    Text = word.Text + ' ',
                    Foreground = word.Confidence < 0.33 ? redBrush : word.Confidence < 0.67 ? yellowBrush : greenBrush,
                };

                OcrTextBlock.Inlines.Add(text);
            }

            OcrTextBlock.Inlines.Add(new Run { Text = "\n" });
        }
    }
}