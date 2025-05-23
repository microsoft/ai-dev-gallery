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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace AIDevGallery.Samples.OpenSourceModels.SINet;

[GallerySample(
    Model1Types = [ModelType.SINet],
    Scenario = ScenarioType.ImageBackgroundDetector,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.BackgroundHelpers,
        SharedCodeEnum.DeviceUtils
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    AssetFilenames = [
        "detection_default.png"
    ],
    Name = "Background Detection",
    Id = "9b74ccc0-15f7-430f-red1-7581fd163509",
    Icon = "\uE8B3")]

internal sealed partial class DetectBackground : BaseSamplePage
{
    private InferenceSession? _inferenceSession;

    public DetectBackground()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();

        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    private void Page_Loaded()
    {
        UploadButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
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

        await Detect(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "detection_default.png"));
    }

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
        picker.FileTypeFilter.Add(".bmp");

        picker.ViewMode = PickerViewMode.Thumbnail;

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            UploadButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            await Detect(file.Path);
        }
    }

    private async Task Detect(string filePath)
    {
        if (_inferenceSession == null)
        {
            return;
        }

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.Visibility = Visibility.Collapsed;

        DefaultImage.Source = new BitmapImage(new Uri(filePath));
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload."); // <exclude-line>

        Bitmap image = new(filePath);
        int originalImageWidth = image.Width;
        int originalImageHeight = image.Height;

        var inputMetadataName = _inferenceSession.InputNames[0];
        var inputDimensions = _inferenceSession.InputMetadata[inputMetadataName].Dimensions;

        int modelInputHeight = inputDimensions[2];
        int modelInputWidth = inputDimensions[3];

        var backgroundMask = await Task.Run(() =>
        {
            using var resizedImage = BitmapFunctions.ResizeWithPadding(image, modelInputWidth, modelInputHeight);

            Tensor<float> input = new DenseTensor<float>(inputDimensions);
            input = BitmapFunctions.PreprocessBitmapWithoutStandardization(resizedImage, input);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);
            IEnumerable<float> output = results[0].AsEnumerable<float>();
            return BackgroundHelpers.GetForegroundMask(output, modelInputWidth, modelInputHeight, originalImageWidth, originalImageHeight);
        });

        BitmapImage? outputImage = BitmapFunctions.RenderBackgroundMask(image, backgroundMask, originalImageWidth, originalImageHeight);

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage!;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });

        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: objects detected."); // <exclude-line>
        image.Dispose();
    }
}