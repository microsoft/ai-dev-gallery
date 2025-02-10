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
using Microsoft.Windows.Management.Deployment;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.WCRAPIs;

[GallerySample(
    Name = "BackgroundRemover",
    Model1Types = [ModelType.BackgroundRemover],
    Scenario = ScenarioType.ImageBackgroundRemover,
    Id = "79eca6f0-3092-4b6f-9a81-94a2aff22559",
    Icon = "\uEE6F")]
internal sealed partial class BackgroundRemover : BaseSamplePage
{
    private readonly List<PointInt32> _selectionPoints = [];
    private SoftwareBitmap? _inputBitmap;

    public BackgroundRemover()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        if (!ImageObjectExtractor.IsAvailable())
        {
            sampleParams.ShowWcrModelLoadingMessage = true;
            var loadResult = await ImageObjectExtractor.MakeAvailableAsync();
            if (loadResult.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                throw new InvalidOperationException(loadResult.ExtendedError.Message);
            }
        }

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

            using IRandomAccessStream stream = await streamRef.OpenReadAsync();
            await SetImage(stream);
        }
    }

    private async Task SetImage(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        _inputBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        if (_inputBitmap == null)
        {
            return;
        }

        await SetImageSource(ImageSrc, _inputBitmap);

        InstructionTxt.Visibility = Visibility.Visible;
        ActionsButtonPanel.Visibility = Visibility.Visible;
        ClearSelectionPoints();
    }

    private async Task SetImageSource(Image image, SoftwareBitmap softwareBitmap)
    {
        var bitmapSource = new SoftwareBitmapSource();

        // This conversion ensures that the image is Bgra8 and Premultiplied
        SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        await bitmapSource.SetBitmapAsync(convertedImage);
        image.Source = bitmapSource;
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

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_inputBitmap == null || _selectionPoints.Count >= 32)
        {
            return;
        }

        CleanSelectionBtn.IsEnabled = true;
        var currentPoint = e.GetCurrentPoint(InputImageCanvas);
        var ratioX = ImageSrc.ActualWidth / _inputBitmap.PixelWidth;
        var ratioY = ImageSrc.ActualHeight / _inputBitmap.PixelHeight;

        // Get the offset between the canvas and the image
        var offSetX = InputImageCanvas.ActualWidth > ImageSrc.ActualWidth ? (InputImageCanvas.ActualWidth - ImageSrc.ActualWidth) / 2 : 0;
        var offSetY = InputImageCanvas.ActualHeight > ImageSrc.ActualHeight ? (InputImageCanvas.ActualHeight - ImageSrc.ActualHeight) / 2 : 0;
        var x = (int)((currentPoint.Position.X - offSetX) / ratioX);
        var y = (int)((currentPoint.Position.Y - offSetY) / ratioY);
        _selectionPoints.Add(new PointInt32(x, y));
        var ellipse = new Ellipse() { Width = 8, Height = 8, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red) };
        Canvas.SetLeft(ellipse, currentPoint.Position.X - 4);
        Canvas.SetTop(ellipse, currentPoint.Position.Y - 4);
        InputImageCanvas.Children.Add(ellipse);
    }

    private void ClearSelectionPoints()
    {
        _selectionPoints.Clear();
        InputImageCanvas.Children.Clear();
        CleanSelectionBtn.IsEnabled = false;
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClearSelectionPoints();
    }

    private void CleanSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionPoints();
    }

    private async void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        if (_inputBitmap == null)
        {
            return;
        }

        Loader.Visibility = Visibility.Visible;
        ImageDst.Visibility = Visibility.Collapsed;

        var outputBitmap = await ExtractBackground(_inputBitmap, _selectionPoints);
        if (outputBitmap != null)
        {
            await SetImageSource(ImageDst, outputBitmap);
        }

        Loader.Visibility = Visibility.Collapsed;
        ImageDst.Visibility = Visibility.Visible;
    }
}