// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

extern alias Feed;

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;

using FrameEventArgs1 = Feed::CommunityToolkit.WinUI.Helpers.FrameEventArgs;

namespace AIDevGallery.Samples.OpenSourceModels.FaceDetLite;

[GallerySample(
    Model1Types = [ModelType.FaceDetLite],
    Scenario = ScenarioType.ImageDetectFace,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils,
        SharedCodeEnum.PoseHelper
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    AssetFilenames = [
        "pose_default.png"
    ],
    Name = "Pose Detection",
    Id = "9b74ccc0-f5f7-430f-bed0-712ffc063508",
    Icon = "\uE8B3")]

internal sealed partial class FaceDetection : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    private List<Prediction> predictions = [];

    private DispatcherTimer _frameRateTimer;
    private VideoFrame? _latestVideoFrame;

    private bool modelActive = true;

    private DateTimeOffset lastFaceDetectionCount = DateTimeOffset.Now;
    private int faceDetectionsCount;
    private int faceDetectionsPerSecond;

    private int originalImageWidth = 1280;
    private int originalImageHeight = 720;

    public FaceDetection()
    {
        this.Unloaded += FaceDetectionUnloaded;
        this.InitializeComponent();
        InitializeCameraPreviewControl();

        _frameRateTimer = new DispatcherTimer();
        InitializeFrameRateTimer();
    }

    private void InitializeFrameRateTimer()
    {
        _frameRateTimer.Interval = TimeSpan.FromMilliseconds(33);
        _frameRateTimer.Tick += FrameRateTimer_Tick;
        _frameRateTimer.Start();
    }

    private void FrameRateTimer_Tick(object sender, object e)
    {
        if (_latestVideoFrame != null)
        {
            ProcessFrame(_latestVideoFrame);
            _latestVideoFrame = null;
        }
    }

    private void FaceDetectionUnloaded(object sender, RoutedEventArgs e)
    {
        lock (this)
        {
            _inferenceSession?.Dispose();
            _inferenceSession = null;
            _latestVideoFrame?.Dispose();
        }
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await InitModel(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
        sampleParams.NotifyCompletion();

        InitializeCameraPreviewControl();
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

    private async void InitializeCameraPreviewControl()
    {
        var cameraHelper = CameraPreviewControl.CameraHelper;

        CameraPreviewControl.PreviewFailed += CameraPreviewControl_PreviewFailed!;
        await CameraPreviewControl.StartAsync(cameraHelper!);
        CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived!;
    }

    private readonly SemaphoreSlim _frameProcessingLock = new SemaphoreSlim(1);

    private void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs1 e)
    {
        _latestVideoFrame = e.VideoFrame;
    }

    private void CameraPreviewControl_PreviewFailed(object sender, PreviewFailedEventArgs e)
    {
        var errorMessage = e.Error;
    }

    private void ToggleModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clickedButton)
        {
            if (modelActive)
            {
                FaceDetText.Text = "Start face detection";
            }
            else
            {
                FaceDetText.Text = "Stop face detection";
            }

            lock (this)
            {
                predictions.Clear();
                faceDetectionsCount = 0;
                faceDetectionsPerSecond = 0;
            }

            modelActive = !modelActive;
            canvasAnimatedControl.Invalidate(); // Force redraw
        }
    }

    private async void ProcessFrame(VideoFrame videoFrame)
    {
        var softwareBitmap = videoFrame.SoftwareBitmap;
        try
        {
            if (!_frameProcessingLock.Wait(0))
            {
                return;
            }

            if (modelActive)
            {
                await DetectFace(videoFrame);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            _frameProcessingLock.Release();
        }
    }

    private async Task DetectFace(VideoFrame videoFrame)
    {
        if (_inferenceSession == null || videoFrame == null)
        {
            return;
        }

        originalImageWidth = videoFrame.SoftwareBitmap.PixelWidth;
        originalImageHeight = videoFrame.SoftwareBitmap.PixelHeight;

        var inputMetadataName = _inferenceSession.InputNames[0];
        var inputDimensions = _inferenceSession.InputMetadata[inputMetadataName].Dimensions;

        int modelInputHeight = inputDimensions[2];
        int modelInputWidth = inputDimensions[3];

        using Bitmap resizedImage = await BitmapFunctions.ResizeVideoFrameWithPadding(videoFrame, modelInputWidth, modelInputHeight);

        Tensor<float> input = new DenseTensor<float>([.. inputDimensions]);
        input = BitmapFunctions.PreprocessBitmapForFaceDetection(resizedImage, input);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
        };

        try
        {
            lock (this)
            {
                if (_inferenceSession == null)
                {
                    return;
                }

                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(inputs);
                {
                    predictions = FaceHelpers.PostprocessFacialResults(results, originalImageWidth, originalImageHeight);
                }
            }
        }
        catch
        {
            lock (this)
            {
                predictions.Clear();
            }
        }

        canvasAnimatedControl.Invalidate();
    }

    private DateTimeOffset lastRenderTime = DateTimeOffset.Now;
    private int framesRenderedSinceLastSecond;
    private int fps;

    private void canvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        args.DrawingSession.Clear(Colors.Transparent);

        if (predictions.Count > 0 && modelActive)
        {
            DrawFaceDetections(args.DrawingSession);
            UpdateFaceDetectionsPerSecond();
        }

        DrawFPS(args.DrawingSession);
    }

    private void DrawFaceDetections(CanvasDrawingSession drawingSession)
    {
        if (predictions == null || predictions.Count == 0)
        {
            return;
        }

        float canvasWidth = (float)canvasAnimatedControl.Size.Width;
        float canvasHeight = (float)canvasAnimatedControl.Size.Height;

        float scaleX = canvasWidth / originalImageWidth;
        float scaleY = canvasHeight / originalImageHeight;

        using (CanvasTextFormat textFormat = new CanvasTextFormat
        {
            FontSize = 14,
            WordWrapping = CanvasWordWrapping.NoWrap
        })
        {
            foreach (var p in predictions)
            {
                if (p == null || p.Box == null)
                {
                    continue;
                }

                float xMin = p.Box.Xmin * scaleX;
                float yMin = p.Box.Ymin * scaleY;
                float width = (p.Box.Xmax - p.Box.Xmin) * scaleX;
                float height = (p.Box.Ymax - p.Box.Ymin) * scaleY;

                drawingSession.DrawRectangle(xMin, yMin, width, height, Colors.Red, 2);

                faceDetectionsCount++;
            }
        }
    }

    private void UpdateFaceDetectionsPerSecond()
    {
        var currentTime = DateTimeOffset.Now;
        faceDetectionsCount++;

        if (currentTime - lastFaceDetectionCount > TimeSpan.FromSeconds(1))
        {
            lastFaceDetectionCount = currentTime;
            faceDetectionsPerSecond = faceDetectionsCount;
            faceDetectionsCount = 0;
        }
    }

    private void DrawFPS(CanvasDrawingSession drawingSession)
    {
        var currentTime = DateTimeOffset.Now;
        framesRenderedSinceLastSecond++;

        if (currentTime - lastRenderTime > TimeSpan.FromSeconds(1))
        {
            lastRenderTime = currentTime;
            fps = framesRenderedSinceLastSecond;
            framesRenderedSinceLastSecond = 0;
        }

        drawingSession.DrawText($"FPS: {fps}", 10, 10, Colors.Blue);
        drawingSession.DrawText($"Face detections per second: {faceDetectionsPerSecond}", 10, 30, Colors.Blue);
    }

    private void CameraPreviewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (originalImageWidth == 0 || originalImageHeight == 0)
        {
            return;
        }

        UpdateSize();
    }

    private void UpdateSize()
    {
        var ratio = originalImageWidth / (float)originalImageHeight;
        if (CameraPreviewControl.ActualWidth / CameraPreviewControl.ActualHeight > ratio)
        {
            canvasAnimatedControl.Width = CameraPreviewControl.ActualHeight * ratio;
            canvasAnimatedControl.Height = CameraPreviewControl.ActualHeight;
        }
        else
        {
            canvasAnimatedControl.Width = CameraPreviewControl.ActualWidth;
            canvasAnimatedControl.Height = CameraPreviewControl.ActualWidth / ratio;
        }
    }
}