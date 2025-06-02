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
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels;

[GallerySample(
    Model1Types = [ModelType.MobileNet, ModelType.ResNet, ModelType.SqueezeNet],
    Scenario = ScenarioType.ImageClassifyImage,
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.ImageNetLabels,
        SharedCodeEnum.ImageNet,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils
    ],
    Name = "ImageNet Image Classification",
    Id = "09d73ba7-b877-45f9-9de6-41898ab4d339",
    Icon = "\uE8B9")]
internal sealed partial class ImageClassification : BaseSamplePage
{
    private InferenceSession? _inferenceSession;

    public ImageClassification()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        try
        {
            string modelPath = sampleParams.ModelPath;
            ExecutionProviderDevicePolicy? policy = sampleParams.WinMlSampleOptions.Policy;
            string? epName = sampleParams.WinMlSampleOptions.EpName;
            bool compileModel = sampleParams.WinMlSampleOptions.CompileModel;

            await InitModel(modelPath, policy, epName, compileModel);
            sampleParams.NotifyCompletion();
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load model.");
            return;
        }

        await ClassifyImage(App.AppData.GetSampleData("ImageClassification", "last-photo-path") ?? Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "team.jpg")); // <exclude-line>
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadImageButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private Task InitModel(string modelPath, ExecutionProviderDevicePolicy? policy, string? epName, bool compileModel)
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
            else if (epName != null)
            {
                sessionOptions.AppendExecutionProviderFromEpName(epName);

                if (compileModel)
                {
                    modelPath = sessionOptions.GetCompiledModel(modelPath, epName) ?? modelPath;
                }
            }

            _inferenceSession = new InferenceSession(modelPath, sessionOptions);
        });
    }

    private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
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
            UploadImageButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            _ = App.AppData.SetSampleDataAsync("ImageClassification", "last-photo-path", file.Path); // <exclude-line>
            await ClassifyImage(file.Path);
        }
    }

    private async Task ClassifyImage(string filePath)
    {
        if (!Path.Exists(filePath) || _inferenceSession == null)
        {
            return;
        }

        SendSampleInteractedEvent("ClassifyImage"); // <exclude-line>

        // Grab model metadata
        var inputName = _inferenceSession.InputNames[0];
        var inputMetadata = _inferenceSession.InputMetadata[inputName];
        var dimensions = inputMetadata.Dimensions;

        // Set batch size to 1
        int batchSize = 1;
        dimensions[0] = batchSize;

        int inputWidth = dimensions[2];
        int inputHeight = dimensions[3];

        BitmapImage bitmapImage = new(new Uri(filePath));
        UploadedImage.Source = bitmapImage;
        NarratorHelper.AnnounceImageChanged(UploadedImage, "Image changed: new upload."); // <exclude-line>

        var predictions = await Task.Run(() =>
        {
            Bitmap image = new(filePath);

            // Resize image
            var resizedImage = BitmapFunctions.ResizeBitmap(image, inputWidth, inputHeight);
            image.Dispose();
            image = resizedImage;

            // Preprocess image
            Tensor<float> input = new DenseTensor<float>(dimensions);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(image, input);
            image.Dispose();

            // Setup inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

            // Postprocess to get softmax vector
            IEnumerable<float> output = results[0].AsEnumerable<float>();
            return ImageNet.GetSoftmax(output);
        });

        // Populates table of results
        ImageNet.DisplayPredictions(predictions, PredictionsStackPanel);
    }
}