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

namespace AIDevGallery.Samples.OpenSourceModels.ObjectDetection.FasterRCNN;

[GallerySample(
    Model1Types = [ModelType.FasterRCNN],
    Scenario = ScenarioType.ImageDetectObjects,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.RCNNLabelMap
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    AssetFilenames = [
        "pose_default.png"
    ],
    Name = "Faster RCNN Object Detection",
    Id = "9b74ccc0-f5f7-430f-bed0-758ffc063508",
    Icon = "\uE8B3")]
internal sealed partial class ObjectDetection : BaseSamplePage
{
    private InferenceSession? _inferenceSession;

    public ObjectDetection()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            await InitModel(sampleParams.ModelPath, sampleParams.WinMlSampleOptions.Policy, sampleParams.WinMlSampleOptions.EpName, sampleParams.WinMlSampleOptions.CompileModel);
            sampleParams.NotifyCompletion();
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load model.");
            return;
        }

        // Loads inference on default image
        await DetectObjects(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "pose_default.png"));
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private Task InitModel(string modelPath, ExecutionProviderDevicePolicy? policy, string? device, bool compileModel)
    {
        return Task.Run(async () =>
        {
            if (_inferenceSession != null)
            {
                return;
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

                if (compileModel)
                {
                    modelPath = sessionOptions.GetCompiledModel(modelPath, device) ?? modelPath;
                }
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
            await DetectObjects(file.Path);
        }
    }

    private async Task DetectObjects(string filePath)
    {
        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.Visibility = Visibility.Collapsed;

        DefaultImage.Source = new BitmapImage(new Uri(filePath));
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload."); // <exclude-line>

        Bitmap image = new(filePath);

        var predictions = await Task.Run(() =>
        {
            // Resizing image ==> Suggested that height and width are in range of [800, 1333].
            float ratio = 800f / Math.Max(image.Width, image.Height);
            int width = (int)(ratio * image.Width);
            int height = (int)(ratio * image.Height);

            var paddedHeight = (int)(Math.Ceiling(image.Height / 32f) * 32f);
            var paddedWidth = (int)(Math.Ceiling(image.Width / 32f) * 32f);

            var resizedImage = BitmapFunctions.ResizeBitmap(image, paddedWidth, paddedHeight);
            image.Dispose();
            image = resizedImage;

            // Preprocessing
            Tensor<float> input = new DenseTensor<float>([3, paddedHeight, paddedWidth]);
            input = BitmapFunctions.PreprocessBitmapForObjectDetection(image, paddedHeight, paddedWidth);

            // Setup inputs and outputs
            var inputMetadataName = _inferenceSession!.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

            // Postprocess to get predictions
            var resultsArray = results.ToArray();
            float[] boxes = resultsArray[0].AsEnumerable<float>().ToArray();
            long[] labels = resultsArray[1].AsEnumerable<long>().ToArray();
            float[] confidences = resultsArray[2].AsEnumerable<float>().ToArray();
            var predictions = new List<Prediction>();
            var minConfidence = 0.7f;
            for (int i = 0; i < boxes.Length - 4; i += 4)
            {
                var index = i / 4;
                if (confidences[index] >= minConfidence)
                {
                    predictions.Add(new Prediction
                    {
                        Box = new Box(boxes[i], boxes[i + 1], boxes[i + 2], boxes[i + 3]),
                        Label = RCNNLabelMap.Labels[labels[index]],
                        Confidence = confidences[index]
                    });
                }
            }

            return predictions;
        });

        BitmapImage outputImage = BitmapFunctions.RenderPredictions(image, predictions);

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });

        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: objects detected."); // <exclude-line>
        image.Dispose();
    }
}