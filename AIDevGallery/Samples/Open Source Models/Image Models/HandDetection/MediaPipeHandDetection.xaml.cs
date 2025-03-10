// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

extern alias Feed;

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using CommunityToolkit.WinUI.Helpers;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage.Streams;
//using FrameEventArgs1 = CommunityToolkit.WinUI.Helpers.FrameEventArgs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace AIDevGallery.Samples.OpenSourceModels.HandDetection;

[GallerySample(
    Model1Types = [ModelType.MediaPipeHandLandmarkDetector],
    Scenario = ScenarioType.ImageDetectPose,
    Name = "Detect Hand Landmarks",
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    AssetFilenames = [
       "hand.png",
    ],
    Id = "9b74acc0-a111-430f-bed0-958ffc063598",
    Icon = "\uE8B3")]
internal sealed partial class MediaPipeHandDetection : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    List<(float X, float Y)>? predictions;

    public MediaPipeHandDetection()
    {
        this.Unloaded += PoseDetection_Unloaded;
        this.InitializeComponent();
        UpdateSize();
    }

    private void PoseDetection_Unloaded(object sender, RoutedEventArgs e)
    {
        lock (this)
        {
            if (_inferenceSession != null)
            {
                _inferenceSession.Dispose();
                _inferenceSession = null;
            }
        }
    }

    // </exclude>
    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await InitModel(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
        sampleParams.NotifyCompletion();

        var cameraHelper = CameraPreviewControl.CameraHelper;

        CameraPreviewControl.PreviewFailed += CameraPreviewControl_PreviewFailed!;
        await CameraPreviewControl.StartAsync(cameraHelper!);

        CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived!;
    }

    private void CameraPreviewControl_PreviewFailed(object sender, PreviewFailedEventArgs e)
    {
        Debug.WriteLine($"Preview failed: {e.Error}");
    }

    // use semaphore to prevent multiple frames from being processed at the same time
    private readonly SemaphoreSlim _frameProcessingLock = new SemaphoreSlim(1);

    private async void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs e)
    {
        try
        {
            // Use an async-friendly semaphore wait
            if (!await _frameProcessingLock.WaitAsync(0))
            {
                return;
            }

            await DetectPose(e.VideoFrame);
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

    int originalImageWidth = 1280;
    int originalImageHeight = 720;

    DateTimeOffset lastPoseDetectionCount = DateTimeOffset.Now;
    int poseDetectionsCount = 0;
    int poseDetectionsPerSecond = 0;

    private async Task DetectPose(VideoFrame videoFrame)
    {
        if(_inferenceSession == null)
        {
            return;
        }

        originalImageWidth = videoFrame.SoftwareBitmap.PixelWidth;
        originalImageHeight = videoFrame.SoftwareBitmap.PixelHeight;

        var inputMetadataName = _inferenceSession!.InputNames[0];
        var inputDimensions = _inferenceSession!.InputMetadata[inputMetadataName].Dimensions;

        uint modelInputWidth = (uint)inputDimensions[2];
        uint modelInputHeight = (uint)inputDimensions[3];

        if (_inferenceSession == null)
        {
            return;
        }

        // Resize Bitmap
        using (IRandomAccessStream stream = new InMemoryRandomAccessStream())
        {
            // Create an encoder with the desired format
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

            var softwareBitmap = SoftwareBitmap.Convert(videoFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8);

            // Set the software bitmap
            encoder.SetSoftwareBitmap(softwareBitmap);

            // Set additional encoding parameters, if needed
            encoder.BitmapTransform.ScaledWidth = modelInputWidth;
            encoder.BitmapTransform.ScaledHeight = modelInputHeight;
            encoder.BitmapTransform.Rotation = BitmapRotation.None;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception err)
            {
                const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                switch (err.HResult)
                {
                    case WINCODEC_ERR_UNSUPPORTEDOPERATION:
                        // If the encoder does not support writing a thumbnail, then try again
                        // but disable thumbnail generation.
                        encoder.IsThumbnailGenerated = false;
                        break;
                    default:
                        throw;
                }
            }

            stream.Seek(0);

            using var image = new System.Drawing.Bitmap(stream.AsStream());

            // Preprocessing
            Tensor<float> input = new DenseTensor<float>([..inputDimensions]);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(image, input);

            // Setup inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            try
            {
                // Run inference
                lock (this)
                {
                    if (_inferenceSession == null)
                    {
                        return;
                    }

                    using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(inputs))
                    {
                        var heatmaps = results[0].AsTensor<float>();

                        var score = results[0].AsTensor<float>()[0];

                        // closer to 1 = R
                        var lr = results[1].AsTensor<float>()[0] > .5 ? "R" : "L";
                        var landmarks = results[2].AsTensor<float>();

                        List<(float X, float Y)> keypointCoordinates = PoseHelper.PostProcessLandmarks(landmarks, originalImageWidth, originalImageHeight, modelInputWidth, modelInputHeight);

                        if (score > .0001)
                        {
                            predictions = keypointCoordinates;
                        }
                        else
                        {
                            predictions = [];
                        }
                    }

                    poseDetectionsCount++;
                    var currentTime = DateTimeOffset.Now;
                    if (currentTime - lastPoseDetectionCount > TimeSpan.FromSeconds(1))
                    {
                        poseDetectionsPerSecond = poseDetectionsCount;
                        poseDetectionsCount = 0;
                        lastPoseDetectionCount = currentTime;
                    }
                }
            }
            catch
            {
                lock (this)
                {
                    predictions = [];
                }
            }
        }
    }

    DateTimeOffset lastRenderTime = DateTimeOffset.Now;
    int framesRenderedSinceLastSecond = 0;
    int fps = 0;
    string modelPath = string.Empty;

    private void canvasControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        var ratio = originalImageWidth / (float)originalImageHeight;
        if (sender.Size.Width / sender.Size.Height > ratio)
        {
            args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale((float)sender.Size.Height / originalImageHeight);
        }
        else
        {
            args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale((float)sender.Size.Width / originalImageWidth);
        }

        args.DrawingSession.Clear(Colors.Transparent);

        List<(float X, float Y)> currentPredictions;
        lock (this)
        {
            if (predictions == null || predictions.Count == 0)
            {
                return;
            }

            currentPredictions = predictions;
        }

        int markerSize = 4;

        List<(int StartIdx, int EndIdx)> connections = new List<(int, int)>
        {
            // Thumb: Only connect thumb landmarks (excluding a connection from the wrist)
            (1, 2), (2, 3), (3, 4),

            // Index finger: Connect the finger joints
            (5, 6), (6, 7), (7, 8),

            // Middle finger: Connect the finger joints
            (9, 10), (10, 11), (11, 12),

            // Ring finger: Connect the finger joints
            (13, 14), (14, 15), (15, 16),

            // Pinky finger: Connect the finger joints
            (17, 18), (18, 19), (19, 20),

            // Circle connecting the lowest (base) points of the four fingers (index, middle, ring, pinky)
            (0, 1), (1, 5), (5, 9), (9, 13), (13, 17), (17, 0)
        };

        foreach (var (startIdx, endIdx) in connections)
        {
            var startPoint = currentPredictions[startIdx];
            var endPoint = currentPredictions[endIdx];

            args.DrawingSession.DrawLine(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y, Colors.Blue, 5);
        }

        foreach (var (x, y) in currentPredictions)
        {
            args.DrawingSession.FillEllipse(x - markerSize / 2, y - markerSize / 2, markerSize, markerSize, Colors.Red);
        }

        var currentTime = DateTimeOffset.Now;
        framesRenderedSinceLastSecond++;


        if (currentTime - lastRenderTime > TimeSpan.FromSeconds(1))
        {
            lastRenderTime = currentTime;
            fps = framesRenderedSinceLastSecond;
            framesRenderedSinceLastSecond = 0;
        }

        args.DrawingSession.DrawText($"FPS: {fps}", 10, 10, Colors.Blue);
        args.DrawingSession.DrawText($"Pose detections per second: {poseDetectionsPerSecond}", 10, 30, Colors.Blue);
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