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
    AssetFilenames = [
        "team.jpg"
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
        await InitModel(sampleParams.ModelPath, sampleParams.PreferedEP);
        sampleParams.NotifyCompletion();

        await ClassifyImage(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "team.jpg"));
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadImageButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    private Task InitModel(string modelPath, string device)
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
                Debug.WriteLine("Downloading packages ...");
                await infrastructure.DownloadPackagesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to download packages: {ex.Message}");
            }

            await infrastructure.RegisterExecutionProviderLibrariesAsync();

            SessionOptions sessionOptions = new();
            sessionOptions.RegisterOrtExtensions();

            if (device == "NPU")
            {
                AppendNPUExecutionProvider(sessionOptions);
            }
            else if (device == "GPU")
            {
                sessionOptions.AppendExecutionProvider_DML(DeviceUtils.GetBestDeviceId());
            }

            var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{device}.onnx";

            if (!File.Exists(compiledModelPath))
            {
                OrtModelCompilationOptions compilationOptions = new(sessionOptions);
                compilationOptions.SetInputModelPath(modelPath);
                compilationOptions.SetOutputModelPath(compiledModelPath);
                compilationOptions.CompileModel();
            }

            if (File.Exists(compiledModelPath))
            {
                modelPath = compiledModelPath;
            }

            _inferenceSession = new InferenceSession(modelPath, sessionOptions);
        });
    }

    private string? AppendNPUExecutionProvider(SessionOptions sessionOptions, OrtEnv? environment = null)
    {
        environment ??= OrtEnv.Instance();
        IReadOnlyList<OrtEpDevice> epDevices = environment.GetEpDevices();
        Dictionary<string, List<OrtEpDevice>> epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (OrtEpDevice device in epDevices)
        {
            string epName = device.EpName;

            if (!epDeviceMap.TryGetValue(epName, out List<OrtEpDevice>? value))
            {
                value = [];
                epDeviceMap[epName] = value;
            }

            value.Add(device);
        }

        // Configure execution providers
        foreach (KeyValuePair<string, List<OrtEpDevice>> epGroup in epDeviceMap)
        {
            string epName = epGroup.Key;
            List<OrtEpDevice> devices = epGroup.Value;

            // Configure EP with all its devices
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);

            switch (epName)
            {
                case "VitisAIExecutionProvider":
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "Vitis";

                case "OpenVINOExecutionProvider":
                    // Configure threading for OpenVINO EP
                    epOptions["num_of_threads"] = "4";
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "OpenVINO";

                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "QNN";

                case "NvTensorRTRTXExecutionProvider":
                    // Configure performance mode for TensorRT RTX EP
                    sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
                    return "NvTensorRTRTX";

                default:
                    break;
            }
        }

        return null;
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
            await ClassifyImage(file.Path);
        }
    }

    private async Task ClassifyImage(string filePath)
    {
        if (!Path.Exists(filePath) || _inferenceSession == null)
        {
            return;
        }

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