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

namespace AIDevGallery.Samples.OpenSourceModels.ESRGAN;

[GallerySample(
      Model1Types = [ModelType.ESRGAN],
      Scenario = ScenarioType.ImageIncreaseFidelity,
      SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.NarratorHelper,
        SharedCodeEnum.DeviceUtils,
      ],
      AssetFilenames = [
        "Enhance.png"
      ],
      NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.Windows.AI.MachineLearning",
        "Microsoft.ML.OnnxRuntime.Extensions"
      ],
      Name = "Enhance Image",
      Id = "9b74cdc1-f5f7-430f-bed0-712ffc063508",
      Icon = "\uE8B3")]
internal sealed partial class SuperResolution : BaseSamplePage
{
    private InferenceSession? _inferenceSession;

    public SuperResolution()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();

        this.Loaded += (s, e) => Page_Loaded();
        this.InitializeComponent();
    }

    private void Page_Loaded()
    {
        UploadButton.Focus(FocusState.Programmatic);
    }

    /// <inheritdoc/>
    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        await InitModel(sampleParams.ModelPath, sampleParams.PreferedEP);

        sampleParams.NotifyCompletion();

        await EnhanceImage(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "Enhance.png"));
    }

    private Task InitModel(string modelPath, string preferedEp)
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
            sessionOptions.AppendExecutionProviderFromEpName(preferedEp);

            var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{preferedEp}.onnx";

            if (!File.Exists(compiledModelPath))
            {
                OrtModelCompilationOptions compilationOptions = new(sessionOptions);
                compilationOptions.SetInputModelPath(modelPath);
                compilationOptions.SetOutputModelPath(compiledModelPath);
                compilationOptions.CompileModel();
                modelPath = compiledModelPath;
            }

            if (File.Exists(compiledModelPath))
            {
                modelPath = compiledModelPath;
            }

            _inferenceSession = new InferenceSession(modelPath, sessionOptions);
        });
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // Create a FileOpenPicker
        var picker = new FileOpenPicker();

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        // Set the file type filter
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".bmp");

        picker.ViewMode = PickerViewMode.Thumbnail;

        // Pick a file
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            // Call function to run inference and classify image
            UploadButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            await EnhanceImage(file.Path);
        }
    }

    private async Task EnhanceImage(string filePath)
    {
        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.Visibility = Visibility.Collapsed;
        UpscaledPanel.Visibility = Visibility.Collapsed;
        OriginalPanel.Visibility = Visibility.Visible;

        DefaultImage.Source = new BitmapImage(new Uri(filePath));
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: new upload."); // <exclude-line>

        using Bitmap image = new(filePath);

        var originalImageWidth = image.Width;
        var originalImageHeight = image.Height;

        DefaultImageDimensions.Text = $"{originalImageWidth} x {originalImageHeight}";

        int modelInputWidth = 128;
        int modelInputHeight = 128;

        // Resize Bitmap
        using Bitmap resizedImage = BitmapFunctions.ResizeWithPadding(image, modelInputWidth, modelInputHeight);

        var bitmapOutput = await Task.Run(() =>
        {
            // Preprocessing
            Tensor<float> input = new DenseTensor<float>([1, 3, modelInputWidth, modelInputHeight]);
            input = BitmapFunctions.PreprocessBitmapWithoutStandardization(resizedImage, input);

            // Setup inputs
            var inputMetadataName = _inferenceSession!.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName ?? "image", input)
            };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

            // Postprocessing
            using Bitmap outputBitmap = BitmapFunctions.TensorToBitmap(results);

            // 4 is the model scaling factor for ESRGAN
            Bitmap finalOutputBitmap = BitmapFunctions.CropAndScale(outputBitmap, originalImageWidth, originalImageHeight, 4);

            return finalOutputBitmap;
        });

        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(bitmapOutput);
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image enhancement complete.");  // <exclude-line>

        bitmapOutput.Dispose();

        DispatcherQueue.TryEnqueue(() =>
        {
            UpscaledPanel.Visibility = Visibility.Visible;
            ScaledImage.Source = outputImage;
            ScaledImageDimensions.Text = $"{outputImage.PixelWidth} x {outputImage.PixelHeight}";
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });
    }
}