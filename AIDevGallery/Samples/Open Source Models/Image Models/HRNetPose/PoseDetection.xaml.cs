// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels.HRNetPose;

[GallerySample(
    Model1Types = [ModelType.HRNetPose],
    Scenario = ScenarioType.ImageDetectPose,
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
    Name = "Pose Detection",
    Id = "9b74ccc0-f5f7-430f-bed0-712ffc063508",
    Icon = "\uE8B3")]
internal sealed partial class PoseDetection : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    public PoseDetection()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var hardwareAccelerator = sampleParams.HardwareAccelerator;
        await InitModel(sampleParams.ModelPath, hardwareAccelerator);
        sampleParams.NotifyCompletion();

        await DetectPose(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "pose_default.png"));
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
            // Call function to run inference and classify image
            UploadButton.Focus(FocusState.Programmatic);
            await DetectPose(file.Path);
        }
    }

    private async Task DetectPose(string filePath)
    {
        if (!Path.Exists(filePath))
        {
            return;
        }

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.Visibility = Visibility.Collapsed;

        DefaultImage.Source = new BitmapImage(new Uri(filePath));
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload."); // <exclude-line>

        using Bitmap image = new(filePath);

        var originalImageWidth = image.Width;
        var originalImageHeight = image.Height;

        int modelInputWidth = 256;
        int modelInputHeight = 192;

        // Resize Bitmap
        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(image, modelInputWidth, modelInputHeight);

        var predictions = await Task.Run(() =>
        {
            // Preprocessing
            Tensor<float> input = new DenseTensor<float>([1, 3, modelInputWidth, modelInputHeight]);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

            var inputMetadataName = _inferenceSession!.InputNames[0];

            // Setup inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);
            var heatmaps = results[0].AsTensor<float>();
            List<(float X, float Y)> keypointCoordinates = PostProcessResults(heatmaps, originalImageWidth, originalImageHeight);
            return keypointCoordinates;
        });

        // Render predictions and create output bitmap
        using Bitmap output = RenderPredictions(image, predictions);
        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImageAsync(output);
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: key points rendered."); // <exclude-line>

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });
    }

    private List<(float X, float Y)> PostProcessResults(Tensor<float> heatmaps, float originalWidth, float originalHeight)
    {
        List<(float X, float Y)> keypointCoordinates = [];

        // Scaling factors from heatmap (64x48) directly to original image size
        float scale_x = originalWidth / 64f;
        float scale_y = originalHeight / 48f;

        int numKeypoints = heatmaps.Dimensions[1];
        int heatmapWidth = heatmaps.Dimensions[2];
        int heatmapHeight = heatmaps.Dimensions[3];

        for (int i = 0; i < numKeypoints; i++)
        {
            float maxVal = float.MinValue;
            int maxX = 0, maxY = 0;

            for (int x = 0; x < heatmapWidth; x++)
            {
                for (int y = 0; y < heatmapHeight; y++)
                {
                    float value = heatmaps[0, i, y, x];
                    if (value > maxVal)
                    {
                        maxVal = value;
                        maxX = x;
                        maxY = y;
                    }
                }
            }

            float scaledX = maxX * scale_x;
            float scaledY = maxY * scale_y;

            keypointCoordinates.Add((scaledX, scaledY));
        }

        return keypointCoordinates;
    }

    private Bitmap RenderPredictions(Bitmap originalImage, List<(float X, float Y)> keypoints)
    {
        Bitmap outputImage = new(originalImage);

        using (Graphics g = Graphics.FromImage(outputImage))
        {
            int markerSize = (int)((originalImage.Width + originalImage.Height) * 0.02 / 2);
            Brush brush = Brushes.Red;

            using Pen linePen = new(Color.Blue, 5);
            List<(int StartIdx, int EndIdx)> connections =
            [
                (5, 6),   // Left shoulder to right shoulder
                    (5, 7),   // Left shoulder to left elbow
                    (7, 9),   // Left elbow to left wrist
                    (6, 8),   // Right shoulder to right elbow
                    (8, 10),  // Right elbow to right wrist
                    (11, 12), // Left hip to right hip
                    (5, 11),  // Left shoulder to left hip
                    (6, 12),  // Right shoulder to right hip
                    (11, 13), // Left hip to left knee
                    (13, 15), // Left knee to left ankle
                    (12, 14), // Right hip to right knee
                    (14, 16) // Right knee to right ankle
            ];

            foreach (var (startIdx, endIdx) in connections)
            {
                var (startPointX, startPointY) = keypoints[startIdx];
                var (endPointX, endPointY) = keypoints[endIdx];

                g.DrawLine(linePen, startPointX, startPointY, endPointX, endPointY);
            }

            foreach (var (x, y) in keypoints)
            {
                g.FillEllipse(brush, x - markerSize / 2, y - markerSize / 2, markerSize, markerSize);
            }
        }

        return outputImage;
    }
}