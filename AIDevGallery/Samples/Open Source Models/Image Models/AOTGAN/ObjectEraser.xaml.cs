// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace AIDevGallery.Samples.OpenSourceModels.AOTGAN;

[GallerySample(
    Model1Types = [ModelType.AOTGAN],
    Scenario = ScenarioType.ImageEraseObject,
    SharedCode = [
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions",
        "Microsoft.Graphics.Win2D"
    ],
    Name = "Object Eraser",
    Id = "9b74cac1-f5f7-417f-bed0-712ffc063508",
    Icon = "\uE8B3")]

internal sealed partial class ObjectEraser : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    private Bitmap? originalImage;
    private List<List<Windows.Foundation.Point>> strokes = new();
    private List<Windows.Foundation.Point>? currentStroke;

    public ObjectEraser()
    {
        this.Unloaded += ObejectEraserUnloaded;
        this.InitializeComponent();
    }

    private void ObejectEraserUnloaded(object sender, RoutedEventArgs e)
    {
        _inferenceSession?.Dispose();
        originalImage?.Dispose();
    }

    // </exclude>
    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await InitModel(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
        sampleParams.NotifyCompletion();

        SetImage(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "eraser_default.png"));
    }

    private Task InitModel(string modelPath, HardwareAccelerator hardwareAccelerator)
    {
        return Task.Run(() =>
        {
            if (_inferenceSession != null)
            {
                return;
            }

            SessionOptions sessionOptions = new();
            sessionOptions.RegisterOrtExtensions();
            if (hardwareAccelerator == HardwareAccelerator.DML)
            {
                sessionOptions.AppendExecutionProvider_DML(DeviceUtils.GetBestDeviceId());
            }
            else if (hardwareAccelerator == HardwareAccelerator.QNN)
            {
                Dictionary<string, string> options = new()
                {
                    { "backend_path", "QnnHtp.dll" },
                    { "htp_performance_mode", "high_performance" },
                    { "htp_graph_finalization_optimization_mode", "3" }
                };
                sessionOptions.AppendExecutionProvider("QNN", options);
            }

            _inferenceSession = new InferenceSession(modelPath, sessionOptions);
        });
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
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
            UploadButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>

            SetImage(file.Path);
        }
    }

    private void SetImage(string filePath)
    {
        if (!Path.Exists(filePath))
        {
            return;
        }

        DefaultImage.Source = new BitmapImage(new Uri(filePath));
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload."); // <exclude-line>
        originalImage = new Bitmap(filePath);
    }

    private async void EraseButton_Click(object sender, RoutedEventArgs e)
    {
        if(originalImage == null)
        {
            return;
        }

        int width = originalImage.Width;
        int height = originalImage.Height;

        // Create an off-screen render target
        using var renderTarget = new CanvasRenderTarget(DrawCanvas, width, height, 96);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.Black); // Background is black (no mask)

            foreach (var stroke in strokes)
            {
                if (stroke.Count < 2) continue;

                using var builder = new CanvasPathBuilder(DrawCanvas);
                builder.BeginFigure(ToVector2(stroke[0]));

                for (int i = 1; i < stroke.Count; i++)
                {
                    builder.AddLine(ToVector2(stroke[i]));
                }

                builder.EndFigure(CanvasFigureLoop.Open);
                using var geometry = CanvasGeometry.CreatePath(builder);

                using var strokeStyle = new CanvasStrokeStyle
                {
                    StartCap = CanvasCapStyle.Round,
                    EndCap = CanvasCapStyle.Round,
                    LineJoin = CanvasLineJoin.Round
                };

                ds.DrawGeometry(geometry, Colors.White, 8f, strokeStyle); // White mask
            }
        }

        // Copy pixels from CanvasRenderTarget to byte[] buffer
        byte[] pixelData = renderTarget.GetPixelBytes();

        // Create System.Drawing.Bitmap from pixel data
        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var bmpData = bmp.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
        bmp.UnlockBits(bmpData);

        await EraseObject(bmp);
    }

    private async Task EraseObject(Bitmap mask)
    {
        if (mask == null || originalImage == null || _inferenceSession == null)
        {
            return;
        }

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.IsEnabled = false;
        EraseObjectButton.IsEnabled = false;
        ClearDrawingButton.IsEnabled = false;

        int originalImageWidth = originalImage.Width;
        int originalImageHeight = originalImage.Height;

        var imageInputMetaDeta = _inferenceSession.InputNames[0];
        var imageInputDimensions = _inferenceSession.InputMetadata[imageInputMetaDeta].Dimensions;

        int imageInputHeight = imageInputDimensions[2];
        int imageInputWidth = imageInputDimensions[3];

        var maskInputMetaDeta = _inferenceSession.InputNames[1];
        var maskInputDimensions = _inferenceSession.InputMetadata[maskInputMetaDeta].Dimensions;

        int maskInputHeight = maskInputDimensions[2];
        int maskInputWidth = maskInputDimensions[3];

        var finalImage = await Task.Run(() =>
        {
            using var resizedImage = BitmapFunctions.ResizeWithPadding(originalImage, imageInputWidth, imageInputHeight);
            using var resizedMask = BitmapFunctions.ResizeWithPadding(mask, maskInputWidth, maskInputHeight);

            Tensor<float> imageTensor = new DenseTensor<float>(imageInputDimensions);
            imageTensor = BitmapFunctions.PreprocessBitmapWithoutStandardization(resizedImage, imageTensor);

            Tensor<float> maskTensor = new DenseTensor<float>(maskInputDimensions);
            maskTensor = BitmapFunctions.PreprocessSingularChannelWithoutStandardization(resizedMask, maskTensor);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(imageInputMetaDeta, imageTensor),
                NamedOnnxValue.CreateFromTensor(maskInputMetaDeta, maskTensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

            using Bitmap outputBitmap = BitmapFunctions.TensorToBitmapv2(results);
            Bitmap finalOutputBitmap = BitmapFunctions.CropAndScaleToOriginalSize(outputBitmap, originalImageWidth, originalImageHeight);
            return finalOutputBitmap;
        });

        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(finalImage);
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image enhancement complete.");  // <exclude-line>

        finalImage.Dispose();
        strokes.Clear();

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            DrawCanvas.Invalidate();
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.IsEnabled = true;
            EraseObjectButton.IsEnabled = true;
            ClearDrawingButton.IsEnabled = true;
        });
    }

    private async void ClearDrawing_Click(object sender, RoutedEventArgs e)
    {
        strokes.Clear();
        DrawCanvas.Invalidate();
    }

    private void DrawCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(DrawCanvas).Position;
        currentStroke = new List<Windows.Foundation.Point> { point };
        strokes.Add(currentStroke);
        DrawCanvas.Invalidate();
    }

    private void DrawCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (currentStroke == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(DrawCanvas);
        if (point.IsInContact)
        {
            currentStroke.Add(point.Position);
            DrawCanvas.Invalidate();
        }
    }

    private void DrawCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        currentStroke = null;
    }

    private void DrawCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        using var strokeStyle = new CanvasStrokeStyle
        {
            StartCap = CanvasCapStyle.Round,
            EndCap = CanvasCapStyle.Round,
            LineJoin = CanvasLineJoin.Round
        };

        foreach (var stroke in strokes)
        {
            if (stroke.Count < 2)
            {
                continue;
            }

            using var builder = new CanvasPathBuilder(sender);
            builder.BeginFigure(ToVector2(stroke[0]));

            for (int i = 1; i < stroke.Count; i++)
            {
                builder.AddLine(ToVector2(stroke[i]));
            }

            builder.EndFigure(CanvasFigureLoop.Open);
            using var geometry = CanvasGeometry.CreatePath(builder);

            args.DrawingSession.DrawGeometry(
                geometry,
                Windows.UI.Color.FromArgb(128, 255, 0, 0),
                12f,
                strokeStyle);
        }
    }

    private Vector2 ToVector2(Windows.Foundation.Point p) => new Vector2((float)p.X, (float)p.Y);
}