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
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels.FacialAttributeDetection;

[GallerySample(
    Model1Types = [ModelType.FacialAttributeDetection],
    Scenario = ScenarioType.ImageCompareFace,
    SharedCode = [
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils,
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.DirectML",
        "Microsoft.ML.OnnxRuntime.Extensions"
    ],
    Name = "Compare Face",
    Id = "9b74ccc0-f5f7-417f-bff0-712ffc063008",
    Icon = "\uE8B3")]
internal sealed partial class CompareFaces : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    private string leftPath = string.Empty;
    private string rightPath = string.Empty;

    public CompareFaces()
    {
        this.Unloaded += (s, e) => _inferenceSession?.Dispose();
        this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
        this.InitializeComponent();
    }

    // <exclude>
    private void Page_Loaded()
    {
        UploadLeftButton.Focus(FocusState.Programmatic);
    }

    // </exclude>
    protected override async Task LoadModelAsync(SampleNavigationParameters sampleParams)
    {
        var hardwareAccelerator = sampleParams.HardwareAccelerator;
        await InitModel(sampleParams.ModelPath, hardwareAccelerator);
        sampleParams.NotifyCompletion();

        // todo, pass two images
        // await Compare(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "streetscape.png"));
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

    private async void UploadLeftButton_Click(object sender, RoutedEventArgs e)
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
            UploadLeftButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            leftPath = file.Path;
            LeftImage.Source = new BitmapImage(new Uri(leftPath));
            GenerateComparisonButton.IsEnabled = leftPath.Length > 0 && rightPath.Length > 0;
        }
    }

    private async void UploadRightButton_Click(object sender, RoutedEventArgs e)
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
            UploadRightButton.Focus(FocusState.Programmatic);
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            rightPath = file.Path;
            RightImage.Source = new BitmapImage(new Uri(rightPath));
            GenerateComparisonButton.IsEnabled = leftPath.Length > 0 && rightPath.Length > 0;
        }
    }

    private async void GenerateComparisonButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Path.Exists(leftPath) || !Path.Exists(rightPath))
        {
            return;
        }

        await Compare(leftPath, rightPath);
    }

    private async Task Compare(string leftPath, string rightPath)
    {
        if (_inferenceSession == null)
        {
            return;
        }

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadLeftButton.Visibility = Visibility.Collapsed;
        UploadRightButton.Visibility = Visibility.Collapsed;

        using Bitmap leftBitmap = new(leftPath);
        using Bitmap rightBitmap = new(rightPath);

        var inputMetadataName = _inferenceSession.InputNames[0];
        var inputDimensions = _inferenceSession.InputMetadata[inputMetadataName].Dimensions;

        int modelInputHeight = inputDimensions[2];
        int modelInputWidth = inputDimensions[3];

        // Resize original image to match model input
        using Bitmap leftResized = BitmapFunctions.ResizeWithPadding(leftBitmap, modelInputWidth, modelInputHeight);
        using Bitmap rightResized = BitmapFunctions.ResizeWithPadding(rightBitmap, modelInputWidth, modelInputHeight);

        // Remove using statement for processedImage; we'll dispose of it manually later
        int score = await Task.Run(() =>
        {
            // Preprocess image and prepare input tensor
            Tensor<float> leftInput = new DenseTensor<float>([.. inputDimensions]);
            leftInput = BitmapFunctions.PreprocessBitmapWithStdDev(leftResized, leftInput);

            Tensor<float> rightInput = new DenseTensor<float>([.. inputDimensions]);
            rightInput = BitmapFunctions.PreprocessBitmapWithStdDev(rightResized, rightInput);

            var inputMetadataName = _inferenceSession!.InputNames[0];

            var leftInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputMetadataName ?? "image", leftInput) };
            var rightInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputMetadataName ?? "image", rightInput) };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> leftResults = _inferenceSession!.Run(leftInputs);
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> rightResults = _inferenceSession!.Run(rightInputs);

            float[] leftEmbedding = leftResults[0].AsTensor<float>().ToArray();
            float[] rightEmbedding = rightResults[0].AsTensor<float>().ToArray();

            return GetSimilarityScore(leftEmbedding, rightEmbedding);
        });

        DispatcherQueue.TryEnqueue(() =>
        {
            LikenessResult.Text = "Similarity " + score + "%";
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadLeftButton.Visibility = Visibility.Visible;
            UploadRightButton.Visibility = Visibility.Visible;
        });
    }

    public static Tensor<float> Preprocess(Bitmap bitmap, Tensor<float> input)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        int stride = bmpData.Stride;
        IntPtr ptr = bmpData.Scan0;
        int bytes = Math.Abs(stride) * height;
        byte[] rgbValues = new byte[bytes];

        Marshal.Copy(ptr, rgbValues, 0, bytes);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * stride) + (x * 3);
                byte blue = rgbValues[index];
                byte green = rgbValues[index + 1];
                byte red = rgbValues[index + 2];

                // Convert to float and normalize to [0,1]
                input[0, 0, y, x] = red / 255f;
                input[0, 1, y, x] = green / 255f;
                input[0, 2, y, x] = blue / 255f;
            }
        }

        bitmap.UnlockBits(bmpData);

        return input;
    }

    public int GetSimilarityScore(float[] emb1, float[] emb2)
    {
        if (emb1.Length != emb2.Length || emb1.Length != 512)
            throw new ArgumentException("Arrays must be length 512");

        Debug.WriteLine(string.Join(", ", emb1.Take(5)));
        Debug.WriteLine(string.Join(", ", emb2.Take(5)));


        double dot = 0, mag1 = 0, mag2 = 0;

        for (int i = 0; i < 512; i++)
        {
            dot += emb1[i] * emb2[i];
            mag1 += Math.Pow(emb1[i], 2);
            mag2 += Math.Pow(emb2[i], 2);
        }

        double cosineSimilarity = dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        Debug.WriteLine($"Cosine Similarity: {cosineSimilarity}");
        double score = (cosineSimilarity + 1) / 2 * 100; // Convert [-1,1] to [0,100]
        return (int)Math.Round(score); // Round to nearest integer
    }

}