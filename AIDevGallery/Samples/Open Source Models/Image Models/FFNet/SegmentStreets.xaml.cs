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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels.FFNet;

[GallerySample(
    Model1Types = [ModelType.FFNet],
    Scenario = ScenarioType.ImageSegmentStreet,
    Name = "Segment Streetscapes",
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
    AssetFilenames = [
       "streetscape.png",
    ],
    Id = "9b74acc0-a5f7-430f-bed0-958ffc063598",
    Icon = "\uE8B3")]
internal sealed partial class SegmentStreets : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    public SegmentStreets()
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

        await Segment(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "streetscape.png"));
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
            await Segment(file.Path);
        }
    }

    private async Task Segment(string filePath)
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
        int originalImageWidth = originalImage.Width;
        int originalImageHeight = originalImage.Height;

        int modelInputWidth = 2048;
        int modelInputHeight = 1024;

        int scaledWidth = 256;
        int scaledHeight = 128;

        // Resize original image to match model input
        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(originalImage, modelInputWidth, modelInputHeight);

        // Remove using statement for processedImage; we'll dispose of it manually later
        Bitmap processedImage = await Task.Run(() =>
        {
            // Preprocess image and prepare input tensor
            Tensor<float> input = new DenseTensor<float>([1, 3, modelInputHeight, modelInputWidth]);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

            var inputMetadataName = _inferenceSession!.InputNames[0];

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputMetadataName ?? "image", input) };

            // Run inference
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

            // Extract the output tensor (shape: [1, 19, 128, 256])
            var output = results[0].AsTensor<float>();
            int num_classes = output.Dimensions[1];

            // Convert tensor output to a Bitmap of the scaled dimensions
            using Bitmap scaledBitmap = new(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb);
            BitmapData scaledData = scaledBitmap.LockBits(
                new Rectangle(0, 0, scaledBitmap.Width, scaledBitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            IntPtr scaledPtr = scaledData.Scan0;
            int scaledBytes = Math.Abs(scaledData.Stride) * scaledBitmap.Height;
            byte[] scaledRgbValues = new byte[scaledBytes];

            // Assign colors to each class
            Dictionary<int, Color> classColors = new()
            {
                { 0, Color.FromArgb(128, Color.Red) }, // Road
                { 1, Color.FromArgb(128, Color.Green) }, // Sidewalks
                { 2, Color.FromArgb(128, Color.Blue) }, // Buildings
                { 3, Color.FromArgb(128, Color.Yellow) }, // Walls
                { 4, Color.FromArgb(128, Color.Magenta) }, // Fences
                { 5, Color.FromArgb(128, Color.Cyan) }, // Poles
                { 6, Color.FromArgb(128, Color.Orange) }, // Traffic lights
                { 7, Color.FromArgb(128, Color.Purple) }, // Steet sign
                { 8, Color.FromArgb(128, Color.Pink) }, // Nature
                { 9, Color.FromArgb(128, Color.Brown) }, // Dirt
                { 10, Color.FromArgb(128, Color.LightBlue) }, // Sky
                { 11, Color.FromArgb(128, Color.LightGreen) }, // Pedistrian
                { 12, Color.FromArgb(128, Color.Gray) }, // Rider
                { 13, Color.FromArgb(128, Color.Lime) }, // Vehicle (Continous unit)
                { 14, Color.FromArgb(128, Color.Gold) }, // Truck (Separate components)
                { 15, Color.FromArgb(128, Color.Silver) }, // Bus
                { 16, Color.FromArgb(128, Color.Maroon) }, // Train
                { 17, Color.FromArgb(128, Color.Red) }, // Motorcycle
                { 18, Color.FromArgb(128, Color.Navy) } // Bicycle
            };

            // Loop through each pixel to determine its color based on the best class
            for (int y = 0; y < scaledHeight; y++)
            {
                for (int x = 0; x < scaledWidth; x++)
                {
                    float maxProbability = -1f;
                    int bestClass = -1;

                    for (int c = 0; c < num_classes; c++)
                    {
                        if (output[0, c, y, x] > maxProbability)
                        {
                            maxProbability = output[0, c, y, x];
                            bestClass = c;
                        }
                    }

                    if (bestClass >= 0)
                    {
                        Color color = classColors[bestClass];
                        int index = (y * scaledData.Stride) + (x * 4);
                        scaledRgbValues[index] = color.B;
                        scaledRgbValues[index + 1] = color.G;
                        scaledRgbValues[index + 2] = color.R;
                        scaledRgbValues[index + 3] = color.A;
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(scaledRgbValues, 0, scaledPtr, scaledBytes);
            scaledBitmap.UnlockBits(scaledData);

            // Resize scaledBitmap to original dimensions
            using Bitmap upscaledBitmap = new(originalImageWidth, originalImageHeight);
            using (Graphics g = Graphics.FromImage(upscaledBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(scaledBitmap, 0, 0, originalImageWidth, originalImageHeight);
            }

            // Overlay the upscaled mask on the original image
            Bitmap combinedImage = new(originalImageWidth, originalImageHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.DrawImage(originalImage, 0, 0, originalImageWidth, originalImageHeight);
                g.DrawImage(upscaledBitmap, 0, 0, originalImageWidth, originalImageHeight);
            }

            return combinedImage;
        });

        // Convert the final overlay to BitmapImage for display
        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(processedImage);

        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: all regions segmented."); // <exclude-line>

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });

        // Dispose of processedImage after the conversion is done
        processedImage.Dispose();
    }
}