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
        await InitModel(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
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

        using Bitmap originalImage = new(filePath);

        int modelInputWidth = 256;
        int modelInputHeight = 192;

        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(originalImage, modelInputWidth, modelInputHeight);

        var predictions = await Task.Run(() =>
        {
            Tensor<float> input = new DenseTensor<float>([1, 3, modelInputWidth, modelInputHeight]);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

            var inputMetadataName = _inferenceSession!.InputNames[0];

            var onnxInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(onnxInputs);
            var heatmaps = results[0].AsTensor<float>();

            var outputName = _inferenceSession!.OutputNames[0];
            var outputDimensions = _inferenceSession!.OutputMetadata[outputName].Dimensions;

            float outputWidth = outputDimensions[2];
            float outputHeight = outputDimensions[3];

            List<(float X, float Y)> keypointCoordinates = PoseHelper.PostProcessResults(heatmaps, originalImage.Width, originalImage.Height, outputWidth, outputHeight);
            return keypointCoordinates;
        });

        using Bitmap output = PoseHelper.RenderPredictions(originalImage, predictions, .02f);
        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(output);
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: key points rendered."); // <exclude-line>

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });
    }
}