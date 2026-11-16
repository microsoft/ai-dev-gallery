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
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace AIDevGallery.Samples.OpenSourceModels.RTMPoseHand;

[GallerySample(
    Model1Types = [ModelType.RTMPoseHand],
    Scenario = ScenarioType.ImageDetectHandPose,
    SharedCode = [
        SharedCodeEnum.Prediction,
        SharedCodeEnum.BitmapFunctions,
        SharedCodeEnum.DeviceUtils,
        SharedCodeEnum.HandPoseHelper
    ],
    NugetPackageReferences = [
        "System.Drawing.Common",
        "Microsoft.ML.OnnxRuntime.Extensions",
        "System.Numerics.Tensors"
    ],
    AssetFilenames = [
        "hand_pose_default.png"
    ],
    Name = "Hand Pose Detection",
    Id = "ac98627a-f975-46a3-b162-639373f4a5bd",
    Icon = "\uE8B3")]
internal sealed partial class HandPoseDetection : BaseSamplePage
{
    // The two model outputs: scores over the positions along x and along y for each key point.
    private const string OutputXName = "serving_default_output_0_output";
    private const string OutputYName = "serving_default_output_1_output";

    // The session is created on a background thread and can be in use there when the page is unloaded,
    // so storing, running and disposing it all take this lock.
    private readonly object _sessionLock = new();
    private InferenceSession? _inferenceSession;
    private bool _isUnloaded;

    public HandPoseDetection()
    {
        this.Unloaded += (s, e) => CleanUp();
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
        bool isModelLoaded = false;

        try
        {
            await InitModel(sampleParams.ModelPath, sampleParams.WinMlSampleOptions.Policy, sampleParams.WinMlSampleOptions.EpName, sampleParams.WinMlSampleOptions.CompileModel, sampleParams.WinMlSampleOptions.DeviceType);
            isModelLoaded = true;
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to load model.");
        }

        sampleParams.NotifyCompletion();

        if (isModelLoaded)
        {
            await DetectHandPose(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "hand_pose_default.png"));
        }
    }

    private Task InitModel(string modelPath, ExecutionProviderDevicePolicy? policy, string? epName, bool compileModel, string? deviceType)
    {
        return Task.Run(async () =>
        {
            if (_inferenceSession != null)
            {
                return;
            }

            var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();

            try
            {
                var registeredProviders = await catalog.EnsureAndRegisterCertifiedAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to install packages: {ex.Message}");
            }

            SessionOptions sessionOptions = new();
            sessionOptions.RegisterOrtExtensions();

            if (policy != null)
            {
                sessionOptions.SetEpSelectionPolicy(policy.Value);
            }
            else if (epName != null)
            {
                sessionOptions.AppendExecutionProviderFromEpName(epName, deviceType);

                if (compileModel)
                {
                    modelPath = sessionOptions.GetCompiledModel(modelPath, epName) ?? modelPath;
                }
            }

            InferenceSession session = new(modelPath, sessionOptions);

            lock (_sessionLock)
            {
                // The page was left while the model was loading.
                if (_isUnloaded)
                {
                    session.Dispose();
                    return;
                }

                _inferenceSession = session;
            }
        });
    }

    private void CleanUp()
    {
        lock (_sessionLock)
        {
            _isUnloaded = true;
            _inferenceSession?.Dispose();
            _inferenceSession = null;
        }
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
        UploadButton.Focus(FocusState.Programmatic);
        if (file != null)
        {
            SendSampleInteractedEvent("FileSelected"); // <exclude-line>
            await DetectHandPose(file.Path);
        }
    }

    private async Task DetectHandPose(string filePath)
    {
        if (!Path.Exists(filePath))
        {
            return;
        }

        Loader.IsActive = true;
        Loader.Visibility = Visibility.Visible;
        UploadButton.Visibility = Visibility.Collapsed;

        try
        {
            DefaultImage.Source = new BitmapImage(new Uri(filePath));
            NarratorHelper.AnnounceImageChanged(DefaultImage, "Content changed: new upload."); // <exclude-line>

            using Bitmap originalImage = new(filePath);

            // The model expects one hand in a square image, so the sample crops the center square of the picture before resizing it.
            Rectangle square = HandPoseHelper.GetCenterSquare(originalImage.Width, originalImage.Height);
            List<HandKeypoint>? keypoints = await Task.Run(() => DetectKeypoints(originalImage, square));
            if (keypoints == null)
            {
                return;
            }

            List<HandKeypoint> imageKeypoints = HandPoseHelper.MapToImage(keypoints, square);
            using Bitmap output = HandPoseHelper.RenderPredictions(originalImage, imageKeypoints);
            DefaultImage.Source = BitmapFunctions.ConvertBitmapToBitmapImage(output);

            // <exclude>
            if (imageKeypoints.Exists(keypoint => keypoint.Score > HandPoseHelper.ScoreThreshold))
            {
                NarratorHelper.AnnounceImageChanged(DefaultImage, "Content changed: key points rendered.");
            }
            else
            {
                NarratorHelper.AnnounceImageChanged(DefaultImage, "Content changed: no hand key points found.");
            }

            // </exclude>
        }
        catch (Exception ex)
        {
            ShowException(ex, "Failed to process the image.");
        }
        finally
        {
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        }
    }

    private List<HandKeypoint>? DetectKeypoints(Bitmap originalImage, Rectangle square)
    {
        using Bitmap squareImage = originalImage.Clone(square, PixelFormat.Format24bppRgb);

        // A square that already has the input size goes to the model as is, without resampling.
        if (square.Width == HandPoseHelper.InputSize)
        {
            return RunModel(squareImage);
        }

        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(squareImage, HandPoseHelper.InputSize, HandPoseHelper.InputSize);
        return RunModel(resizedImage);
    }

    // Returns null when the page has been unloaded.
    private List<HandKeypoint>? RunModel(Bitmap inputImage)
    {
        Tensor<float> input = new DenseTensor<float>([1, 3, inputImage.Height, inputImage.Width]);
        input = BitmapFunctions.PreprocessBitmapWithStdDev(inputImage, input);

        lock (_sessionLock)
        {
            if (_isUnloaded)
            {
                return null;
            }

            if (_inferenceSession == null)
            {
                throw new InvalidOperationException("The model is not loaded.");
            }

            var onnxInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inferenceSession.InputNames[0], input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(onnxInputs);
            float[] simccX = results.First(result => result.Name == OutputXName).AsTensor<float>().ToArray();
            float[] simccY = results.First(result => result.Name == OutputYName).AsTensor<float>().ToArray();
            return HandPoseHelper.DecodeKeypoints(simccX, simccY);
        }
    }
}