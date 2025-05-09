// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels.FacialAttributeDetection;

[GallerySample(
    Model1Types = [ModelType.FacialAttributeDetection],
    Scenario = ScenarioType.ImageDetectFeaturesStill,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils,
        SharedCodeEnum.FaceHelpers
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Name = "Face Detction (Image mode)",
    Id = "10d73ba7-b117-45f9-9de6-41898ab4d339",
    Icon = "\uE8B3")]
internal sealed partial class DetectFeaturesStill : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    private Dictionary<string, bool>? predictions;

    public DetectFeaturesStill()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();
        this.Loaded += (s, e) => Page_Loaded();
        this.InitializeComponent();
    }

    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var hardwareAccelerator = sampleParams.HardwareAccelerator;
        await InitModel(sampleParams.ModelPath, hardwareAccelerator);
        sampleParams.NotifyCompletion();

        await AnalyzeImage(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "detection_default.png"));
    }

    private void Page_Loaded()
    {
        UploadImageButton.Focus(FocusState.Programmatic);
    }

    private Task InitModel(string modelPath, HardwareAccelerator hardwareAccelerator)
    {
        return Task.Run(() =>
        {
            if (_inferenceSession != null) return;

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
            SendSampleInteractedEvent("FileSelected");
            await AnalyzeImage(file.Path);
        }
    }

    private async Task AnalyzeImage(string filePath)
    {
        if (!Path.Exists(filePath) || _inferenceSession == null) return;

        var inputName = _inferenceSession.InputNames[0];
        var inputMetadata = _inferenceSession.InputMetadata[inputName];
        var dimensions = inputMetadata.Dimensions;
        dimensions[0] = 1;

        int inputWidth = dimensions[2];
        int inputHeight = dimensions[3];

        BitmapImage bitmapImage = new(new Uri(filePath));
        UploadedImage.Source = bitmapImage;

        await Task.Run(() =>
        {
            using Bitmap image = new(filePath);
            using Bitmap resized = BitmapFunctions.ResizeBitmap(image, inputWidth, inputHeight);

            Tensor<float> input = new DenseTensor<float>(dimensions);
            input = Preprocess(resized, input);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);
            predictions = Postprocess(results);
        });

        DisplayPredictions();
    }

    private Dictionary<string, bool> Postprocess(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        Dictionary<string, bool> attributes = new();

        foreach (var result in results)
        {
            var name = result.Name;

            if (name == "id_feature")
            {
                continue;
            }

            var tensor = result.AsTensor<float>();

            if (tensor.Dimensions.SequenceEqual([1, 2]))
            {
                // Binary classification: choose the more likely class (index 1 = true)
                attributes[name] = tensor[0, 1] > tensor[0, 0];
            }
            else if (name == "liveness_feature" && tensor.Dimensions.SequenceEqual([1, 32]))
            {
                // Use vector magnitude as a naive confidence of “liveness”
                float sumSq = 0;
                foreach (var v in tensor)
                {
                    sumSq += v * v;
                }

                float magnitude = (float)Math.Sqrt(sumSq);

                attributes["liveness"] = magnitude > 30000;
            }
        }

        return attributes;
    }

    public static Tensor<float> Preprocess(Bitmap bitmap, Tensor<float> input)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        BitmapData bmpData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        int stride = bmpData.Stride;
        IntPtr ptr = bmpData.Scan0;
        int bytes = Math.Abs(stride) * height;
        byte[] rgbValues = new byte[bytes];

        Marshal.Copy(ptr, rgbValues, 0, bytes);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 3;
                byte blue = rgbValues[index];
                byte green = rgbValues[index + 1];
                byte red = rgbValues[index + 2];

                input[0, 0, y, x] = red / 255f;
                input[0, 1, y, x] = green / 255f;
                input[0, 2, y, x] = blue / 255f;
            }
        }

        bitmap.UnlockBits(bmpData);
        return input;
    }

    private void DisplayPredictions()
    {
        PredictionsStackPanel.Children.Clear();
        if (predictions == null || predictions.Count == 0) return;

        foreach (var kvp in predictions)
        {
            var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = $"{kvp.Key}: {(kvp.Value ? "True" : "False")}",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(kvp.Value ? Colors.Green : Colors.Red),
                FontSize = 18
            };
            PredictionsStackPanel.Children.Add(textBlock);
        }
    }
}