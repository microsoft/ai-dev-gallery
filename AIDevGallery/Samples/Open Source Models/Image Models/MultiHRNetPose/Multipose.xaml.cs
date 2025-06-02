// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.System;

namespace AIDevGallery.Samples.OpenSourceModels.MultiHRNetPose;

[GallerySample(
    Model1Types = [ModelType.HRNetPose],
    Model2Types = [ModelType.YOLO],
    Scenario = ScenarioType.ImageDetectPoses,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils,
        SharedCodeEnum.PoseHelper,
        SharedCodeEnum.YOLOHelpers,
        SharedCodeEnum.RCNNLabelMap
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    AssetFilenames = [
        "team.jpg"
    ],
    Name = "Multiple Pose Detection",
    Id = "9b74ccc0-f5f7-430f-bed0-71211c063508",
    Icon = "\uE8B3")]
internal sealed partial class Multipose : BaseSamplePage
{
    private InferenceSession? _detectionSession;
    private InferenceSession? _poseSession;

    public Multipose()
    {
        this.Unloaded += (s, e) =>
        {
            _detectionSession?.Dispose();
            _poseSession?.Dispose();
        };

        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override async Task LoadModelAsync(MultiModelSampleNavigationParameters sampleParams)
    {
        await InitModels(sampleParams.ModelPaths[0], sampleParams.ModelPaths[1], sampleParams.WinMlSampleOptions.Policy, sampleParams.WinMlSampleOptions.EpName, sampleParams.WinMlSampleOptions.CompileModel);
        sampleParams.NotifyCompletion();

        await RunPipeline(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "team.jpg"));
    }

    private Task InitModels(string poseModelPath, string detectionModelPath, ExecutionProviderDevicePolicy? policy, string? device, bool compileModel)
    {
        return Task.Run(async () =>
        {
            Microsoft.Windows.AI.MachineLearning.Infrastructure infrastructure = new();

            try
            {
                await infrastructure.DownloadPackagesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to download packages: {ex.Message}");
            }

            await infrastructure.RegisterExecutionProviderLibrariesAsync();

            _poseSession = await GetInferenceSession(poseModelPath, policy, device, compileModel);
            _detectionSession = await GetInferenceSession(detectionModelPath, ExecutionProviderDevicePolicy.PREFER_CPU, device, compileModel);
        });
    }

    private Task<InferenceSession> GetInferenceSession(string modelPath, ExecutionProviderDevicePolicy? policy, string? device, bool compileOption)
    {
        return Task.Run(async () =>
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("Model file not found.", modelPath);
            }

            Microsoft.Windows.AI.MachineLearning.Infrastructure infrastructure = new();

            try
            {
                await infrastructure.DownloadPackagesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to download packages: {ex.Message}");
            }

            await infrastructure.RegisterExecutionProviderLibrariesAsync();

            SessionOptions sessionOptions = new();
            sessionOptions.RegisterOrtExtensions();

            if (policy != null)
            {
                sessionOptions.SetEpSelectionPolicy(policy.Value);
            }
            else if (device != null)
            {
                sessionOptions.AppendExecutionProviderFromEpName(device);

                if (compileOption)
                {
                    modelPath = sessionOptions.GetCompiledModel(modelPath, device) ?? modelPath;
                }
            }

            InferenceSession inferenceSession = new(modelPath, sessionOptions);
            return inferenceSession;
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
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            await RunPipeline(file.Path);
        }
    }

    private async Task RunPipeline(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = new BitmapImage(new Uri(filePath));
            Loader.IsActive = true;
            Loader.Visibility = Visibility.Visible;
            UploadButton.Visibility = Visibility.Collapsed;
        });

        Bitmap originalImage = new(filePath);

        // Step 1: Detect where the "person" tag is found in the image
        List<Prediction> predictions = await FindPeople(originalImage);
        predictions = predictions.Where(x => x.Label == "person").ToList();

        // Step 2: For each person detected, crop the region and run pose
        foreach (var prediction in predictions)
        {
            if (prediction.Box != null)
            {
                using Bitmap croppedImage = BitmapFunctions.CropImage(originalImage, prediction.Box);

                using Bitmap poseOverlay = await DetectPose(croppedImage, originalImage);

                originalImage = BitmapFunctions.OverlayImage(originalImage, poseOverlay, prediction.Box);
            }
        }

        // Step 3: Convert the processed image back to BitmapImage
        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(originalImage);

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });

        originalImage.Dispose();
    }

    private async Task<List<Prediction>> FindPeople(Bitmap image)
    {
        if (_detectionSession == null)
        {
            return [];
        }

        int originalWidth = image.Width;
        int originalHeight = image.Height;

        var predictions = await Task.Run(() =>
        {
            // Set up
            var inputName = _detectionSession.InputNames[0];
            var inputDimensions = _detectionSession.InputMetadata[inputName].Dimensions;

            // Set batch size
            int batchSize = 1;
            inputDimensions[0] = batchSize;

            // I know the input dimensions to be [batchSize, 416, 416, 3]
            int inputWidth = inputDimensions[1];
            int inputHeight = inputDimensions[2];

            using var resizedImage = BitmapFunctions.ResizeWithPadding(image, inputWidth, inputHeight);

            // Preprocessing
            Tensor<float> input = new DenseTensor<float>(inputDimensions);
            input = BitmapFunctions.PreprocessBitmapForYOLO(resizedImage, input);

            // Setup inputs and outputs
            var inputMetadataName = _detectionSession!.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _detectionSession!.Run(inputs);

            // Extract tensors from inference results
            var outputTensor1 = results[0].AsTensor<float>();
            var outputTensor2 = results[1].AsTensor<float>();
            var outputTensor3 = results[2].AsTensor<float>();

            // Define anchors (as per your model)
            var anchors = new List<(float Width, float Height)>
            {
                (12, 16), (19, 36), (40, 28),   // Small grid (52x52)
                (36, 75), (76, 55), (72, 146),  // Medium grid (26x26)
                (142, 110), (192, 243), (459, 401) // Large grid (13x13)
            };

            // Combine tensors into a list for processing
            var gridTensors = new List<Tensor<float>> { outputTensor1, outputTensor2, outputTensor3 };

            // Postprocessing steps
            var extractedPredictions = YOLOHelpers.ExtractPredictions(gridTensors, anchors, inputWidth, inputHeight, originalWidth, originalHeight);

            // Extra step for filtering overlapping predictions
            var filteredPredictions = YOLOHelpers.ApplyNms(extractedPredictions, .4f);

            // Return the final predictions
            return filteredPredictions;
        });

        return predictions;
    }

    private async Task<Bitmap> DetectPose(Bitmap image, Bitmap baseImage)
    {
        if (image == null)
        {
            return new Bitmap(0, 0);
        }

        var inputName = _poseSession!.InputNames[0];
        var inputDimensions = _poseSession.InputMetadata[inputName].Dimensions;

        var originalImageWidth = image.Width;
        var originalImageHeight = image.Height;

        int modelInputWidth = inputDimensions[2];
        int modelInputHeight = inputDimensions[3];

        // Resize Bitmap
        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(image, modelInputWidth, modelInputHeight);

        var predictions = await Task.Run(() =>
        {
            // Preprocessing
            Tensor<float> input = new DenseTensor<float>(inputDimensions);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

            // Setup inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _poseSession!.Run(inputs);
            var heatmaps = results[0].AsTensor<float>();

            var outputName = _poseSession!.OutputNames[0];
            var outputDimensions = _poseSession!.OutputMetadata[outputName].Dimensions;

            float outputWidth = outputDimensions[2];
            float outputHeight = outputDimensions[3];

            List<(float X, float Y)> keypointCoordinates = PoseHelper.PostProcessResults(heatmaps, originalImageWidth, originalImageHeight, outputWidth, outputHeight);
            return keypointCoordinates;
        });

        // Render predictions and create output bitmap
        return PoseHelper.RenderPredictions(image, predictions, .015f, baseImage);
    }
}