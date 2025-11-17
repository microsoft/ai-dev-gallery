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
using System.Threading;
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
    private const int FloodFillTolerance = 25;
    private readonly Stack<SoftwareBitmap> _bitmaps = new();
    private SoftwareBitmap? _inputBitmap;
    private ImageGenerator? _generator;
    private PointInt32? _selectionPoint;
    private CancellationTokenSource? _cts;
    private bool _isProgressVisible;

    public ColoringBook()
    {
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var readyState = ImageGenerator.GetReadyState();
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
            ShowException(null, $"Image Generator is not available: {msg}");
        }

        sampleParams.NotifyCompletion();
    }

    private async Task LoadDefaultImage()
    {
        var file = await StorageFile.GetFileFromPathAsync(System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "ColoringBook.png"));
        using var stream = await file.OpenReadAsync();
        await SetImage(stream);
        DrawPoint(880, 435);
        await ChangeImage();
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

        SetImageSource(_inputBitmap);
        ClearSelectionPoint();
        SwitchInputOutputView();
    }

    private void SetImageSource(SoftwareBitmap softwareBitmap)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var bitmapSource = new SoftwareBitmapSource();

            // This conversion ensures that the image is Bgra8 and Premultiplied
            SoftwareBitmap convertedImage = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            await bitmapSource.SetBitmapAsync(convertedImage);
            CanvasImage.Source = bitmapSource;
        });
    }

    private async void ChangeButton_Click(object sender, RoutedEventArgs e)
    {
        await ChangeImage();
    }

    private async Task ChangeImage()
    {
        if (_inputBitmap == null || _selectionPoint == null)
        {
            return;
        }

        ChangeButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;
        _cts = new CancellationTokenSource();
        var prompt = InputTextBox.Text;

        await Task.Run(
            () =>
            {
                SoftwareBitmap? mask = CreateFloodFillMask(_inputBitmap!, _selectionPoint!.Value, FloodFillTolerance);
                if (mask == null)
                {
                    ShowException(null, "Failed to create flood fill mask");
                    return;
                }

                var outputBitmap = ApplyMaskWithPrompt(prompt, _inputBitmap, mask);
                if (_cts!.Token.IsCancellationRequested)
                {
                    return;
                }

                if (outputBitmap != null)
                {
                    SetImageSource(outputBitmap);
                    _bitmaps.Push(_inputBitmap);
                    _inputBitmap = outputBitmap;
                    SwitchInputOutputView();
                }
            },
            _cts.Token);

        StopBtn.Visibility = Visibility.Collapsed;
        ChangeButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;

        if (!_cts!.Token.IsCancellationRequested)
        {
            ClearSelectionPoint();
        }

        _cts?.Dispose();
        _cts = null;
    }

    private void CanvasImage_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_inputBitmap == null)
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
        DrawCanvas.Children.Clear();
        DrawPoint(x, y);
    }

    private void DrawPoint(int x, int y)
    {
        _selectionPoint = new PointInt32(x, y);

        var canvasX = x * CanvasImage.ActualWidth / _inputBitmap!.PixelWidth;
        var canvasY = y * CanvasImage.ActualHeight / _inputBitmap!.PixelHeight;
        ClearSelectionButton.IsEnabled = true;
        ChangeButton.IsEnabled = InputTextBox.Text.Length > 0;
        var ellipse = new Ellipse() { Width = 8, Height = 8, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red) };
        Canvas.SetLeft(ellipse, canvasX - 4);
        Canvas.SetTop(ellipse, canvasY - 4);
        DrawCanvas.Children.Add(ellipse);
    }

    private void SwitchInputOutputView()
    {
        DispatcherQueue.TryEnqueue(() => RevertButton.Visibility = _bitmaps.Count == 0 ? Visibility.Collapsed : Visibility.Visible);
    }

    private void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmaps.Count > 0)
        {
            _inputBitmap = _bitmaps.Pop();
            SetImageSource(_inputBitmap);
        }

        SwitchInputOutputView();
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionPoint();
    }

    private void ClearSelectionPoint()
    {
        _selectionPoint = null;
        DrawCanvas.Children.Clear();
        ClearSelectionButton.IsEnabled = false;
        ChangeButton.IsEnabled = false;
    }

    private void CanvasImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_selectionPoint != null)
        {
            DrawPoint(_selectionPoint.Value.X, _selectionPoint.Value.Y);
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

            ChangeButton.IsEnabled = inputLength <= MaxLength && _selectionPoint != null;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            ChangeButton.IsEnabled = false;
        }
    }

    private SoftwareBitmap? ApplyMaskWithPrompt(string prompt, SoftwareBitmap inputBitmap, SoftwareBitmap grayMask)
    {
        if (_generator == null)
        {
            ShowException(new InvalidOperationException("ImageGenerator is not initialized"));
            return null;
        }

        if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || grayMask.BitmapPixelFormat != BitmapPixelFormat.Gray8)
        {
            ShowException(new ArgumentException("Input bitmap must be Bgra8 and gray mask must be Gray8"));
            return null;
        }

        using var inputBuffer = ImageBuffer.CreateForSoftwareBitmap(inputBitmap);
        using var mask = ImageBuffer.CreateForSoftwareBitmap(grayMask);

        var result = _generator!.GenerateImageFromImageBufferAndMask(inputBuffer, mask, prompt, new ImageGenerationOptions() { Creativity = 0 });
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

    private SoftwareBitmap? CreateFloodFillMask(SoftwareBitmap source, PointInt32 seed, int tolerance)
    {
        if (source.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            // Convert if necessary
            source = SoftwareBitmap.Convert(source, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (seed.X < 0 || seed.X >= width || seed.Y < 0 || seed.Y >= height)
        {
            return null;
        }

        byte[] pixelBuffer = new byte[4 * width * height];
        source.CopyToBuffer(pixelBuffer.AsBuffer());

        int seedIndex = (seed.Y * width + seed.X) * 4;
        byte seedB = pixelBuffer[seedIndex + 0];
        byte seedG = pixelBuffer[seedIndex + 1];
        byte seedR = pixelBuffer[seedIndex + 2];

        byte[] mask = new byte[width * height];
        bool[] visited = new bool[width * height];

        Queue<PointInt32> q = new();
        q.Enqueue(seed);
        visited[seed.Y * width + seed.X] = true;
        mask[seed.Y * width + seed.X] = 255;

        int[] dx = [1, -1, 0, 0];
        int[] dy = [0, 0, 1, -1];

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = p.X + dx[i];
                int ny = p.Y + dy[i];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    continue;
                }

                int linear = ny * width + nx;
                if (visited[linear])
                {
                    continue;
                }

                int idx = linear * 4;
                byte b = pixelBuffer[idx + 0];
                byte g = pixelBuffer[idx + 1];
                byte r = pixelBuffer[idx + 2];

                // Per-channel tolerance
                if (Math.Abs(r - seedR) <= tolerance &&
                    Math.Abs(g - seedG) <= tolerance &&
                    Math.Abs(b - seedB) <= tolerance)
                {
                    visited[linear] = true;
                    mask[linear] = 255;
                    q.Enqueue(new PointInt32(nx, ny));
                }
                else
                {
                    visited[linear] = true;
                }
            }
        }

        var grayMask = new SoftwareBitmap(BitmapPixelFormat.Gray8, width, height, BitmapAlphaMode.Ignore);
        grayMask.CopyFromBuffer(mask.AsBuffer());
        return grayMask;
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set
        {
            _isProgressVisible = value;
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopIcon.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        ChangeButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Cancel();
    }
}