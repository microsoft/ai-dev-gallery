using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using AIDevGallery.Samples.Attributes;
using Windows.Data.Xml.Dom;
using Microsoft.ML.OnnxRuntime;
using AIDevGallery.Models;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using AIDevGallery.Utils;
using AIDevGallery.Samples.SharedCode;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace AIDevGallery.Samples.OpenSourceModels.HandDetection;

[GallerySample(
    Model1Types = [ModelType.MediaPipeHandLandmarkDetector],
    Scenario = ScenarioType.ImageDetectPose,
    Name = "Detect Hand Landmarks",
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
       "hand.png",
    ],
    Id = "9b74acc0-a111-430f-bed0-958ffc063598",
    Icon = "\uE8B3")]
internal sealed partial class MediaPipeHandDetection : BaseSamplePage
{
    private InferenceSession? _inferenceSession;
    public MediaPipeHandDetection()
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
        await InitModel(sampleParams.ModelPath, sampleParams.HardwareAccelerator);
        sampleParams.NotifyCompletion();

        await DetectPose(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "handpose.jpg"));
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
            await DetectPose(file.Path);
        }
    }

    private async Task DetectPose(string filePath)
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

        var inputMetadataName = _inferenceSession!.InputNames[0];
        var inputDimensions = _inferenceSession!.InputMetadata[inputMetadataName].Dimensions;

        int modelInputWidth = inputDimensions[2];
        int modelInputHeight = inputDimensions[3];

        using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(originalImage, modelInputWidth, modelInputHeight);

        string lr = String.Empty;

        var predictions = await Task.Run(() =>
        {
            Tensor<float> input = new DenseTensor<float>([.. inputDimensions]);
            input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

            var onnxInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(onnxInputs);

            var heatmaps = results[0].AsTensor<float>();

            var score = results[0].AsTensor<float>();

            // closer to 1 = R
            lr = results[1].AsTensor<float>()[0] > .5 ? "R" : "L";
            var landmarks = results[2].AsTensor<float>();

            List<(float X, float Y)> keypointCoordinates = PoseHelper.PostProcessLandmarks(landmarks, originalImage.Width, originalImage.Height, modelInputWidth, modelInputHeight);
            return keypointCoordinates;
        });

        using Bitmap output = PoseHelper.RenderHandPredictions(originalImage, predictions, .02f, lr);
        BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImage(output);
        NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: key points rendered."); // <exclude-line>

        DispatcherQueue.TryEnqueue(() =>
        {
            DefaultImage.Source = outputImage;
            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        });
    }
}