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
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "Detect Text",
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
            await SetImage(stream);
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.Bitmap))
        {
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
        SoftwareBitmap inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        if (inputBitmap == null)
        {
            return;
        }

        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        ImageSrc.Source = bitmapSource;
        await RecognizeAndAddTextAsync(convertedImage);
    }

    public async Task RecognizeAndAddTextAsync(SoftwareBitmap bitmap)
    {
        if (_textRecognizer == null)
        {
            return;
        }

        OutputPanel.Visibility = Visibility.Collapsed;
        Loader.Visibility = Visibility.Visible;
        OcrTextBlock.Inlines.Clear();

        using ImageBuffer imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);
        RecognizedText result = await _textRecognizer.RecognizeTextFromImageAsync(imageBuffer, new TextRecognizerOptions());

        SolidColorBrush greenBrush = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        SolidColorBrush yellowBrush = (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
        SolidColorBrush redBrush = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];

        if (result.Lines == null || result.Lines.Length == 0)
        {
            OcrTextBlock.Inlines.Add(new Run { Text = "No text found." });
            OutputPanel.Visibility = Visibility.Visible;
            Loader.Visibility = Visibility.Collapsed;
            return;
        }

        InstructionTxt.Visibility = Visibility.Visible;

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

        OutputPanel.Visibility = Visibility.Visible;
        Loader.Visibility = Visibility.Collapsed;
    }
}