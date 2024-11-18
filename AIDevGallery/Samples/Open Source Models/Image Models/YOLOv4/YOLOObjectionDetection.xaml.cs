// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.Attributes;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIDevGallery.Samples.OpenSourceModels.YOLOv4
{
    [GallerySample(
        Model1Types = [ModelType.YOLO],
        Scenario = ScenarioType.ImageDetectObjects,
        SharedCode = [
            SharedCodeEnum.Prediction,
            SharedCodeEnum.BitmapFunctions,
            SharedCodeEnum.RCNNLabelMap
        ],
        NugetPackageReferences = [
            "System.Drawing.Common",
            "Microsoft.ML.OnnxRuntime.DirectML",
            "Microsoft.ML.OnnxRuntime.Extensions"
        ],
        Name = "Object Detection",
        Id = "9b74ccc0-f5f7-430f-bed0-758ffd163508",
        Icon = "\uE8B3")]

    internal sealed partial class YOLOObjectionDetection : Page
    {
        private InferenceSession? _inferenceSession;

        public YOLOObjectionDetection()
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
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is SampleNavigationParameters sampleParams)
            {
                var hardwareAccelerator = sampleParams.HardwareAccelerator;
                await InitModel(sampleParams.ModelPath, hardwareAccelerator);
                sampleParams.NotifyCompletion();

                // Loads inference on default image
                await DetectObjects(Windows.ApplicationModel.Package.Current.InstalledLocation.Path + "\\Assets\\team.jpg");
            }
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
                await DetectObjects(file.Path);
            }
        }

        private async Task DetectObjects(string filePath)
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

            int originalWidth = image.Width;
            int originalHeight = image.Height;

            var predictions = await Task.Run(() =>
            {
                // Set up
                var inputName = _inferenceSession.InputNames[0];
                var inputDimensions = _inferenceSession.InputMetadata[inputName].Dimensions;

                // Set batch size
                int batchSize = 1;
                inputDimensions[0] = batchSize;

                // I know the input dimensions to be [batchSize, 416, 416, 3]
                int inputWidth = inputDimensions[1];
                int inputHeight = inputDimensions[2];

                using var resizedImage = BitmapFunctions.ResizeWithPadding(image, inputWidth, inputHeight);

                // Preprocessing
                Tensor<float> input = new DenseTensor<float>(inputDimensions);
                input = BitmapFunctions.PreprocessBitmapForYOLO(resizedImage, input);

                // Setup inputs and outputs
                var inputMetadataName = _inferenceSession!.InputNames[0];
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
                };

                // Run inference
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession!.Run(inputs);

                // Extract tensors from inference results
                var outputTensor1 = results[0].AsTensor<float>();
                var outputTensor2 = results[1].AsTensor<float>();
                var outputTensor3 = results[2].AsTensor<float>();

                // Define anchors (as per your model)
                var anchors = new List<(float Width, float Height)>
                {
                    (12, 16), (19, 36), (40, 28),   // Small grid (52x52)
                    (36, 75), (76, 55), (72, 146),  // Medium grid (26x26)
                    (142, 110), (192, 243), (459, 401) // Large grid (13x13)
                };

                // Combine tensors into a list for processing
                var gridTensors = new List<Tensor<float>> { outputTensor1, outputTensor2, outputTensor3 };

                // Postprocessing steps
                var extractedPredictions = ExtractPredictions(gridTensors, anchors, inputWidth, inputHeight, originalWidth, originalHeight);
                var filteredPredictions = ApplyNms(extractedPredictions, .4f);

                // Return the final predictions
                return filteredPredictions;
            });

            RenderPredictions(image, predictions);
            image.Dispose();

            Loader.IsActive = false;
            Loader.Visibility = Visibility.Collapsed;
            UploadButton.Visibility = Visibility.Visible;
        }

        private List<Prediction> ExtractPredictions(List<Tensor<float>> gridTensors, List<(float Width, float Height)> anchors, int inputWidth, int inputHeight, int originalWidth, int originalHeight)
        {
            var predictions = new List<Prediction>();
            int anchorCounter = 0;
            float confidenceThreshold = .5f;

            foreach (var tensor in gridTensors)
            {
                var gridSize = tensor.Dimensions[2];

                int gridX = gridSize;
                int gridY = gridSize;
                int numAnchors = tensor.Dimensions[3];

                for (int anchor = 0; anchor < numAnchors; anchor++)
                {
                    for (int i = 0; i < gridX; i++)
                    {
                        for (int j = 0; j < gridY; j++)
                        {
                            // Access prediction vector
                            var predictionVector = new List<float>();
                            for (int k = 0; k < tensor.Dimensions[^1]; k++) // Loop over the last dimension
                            {
                                predictionVector.Add(tensor[0, i, j, anchor, k]);
                            }

                            // Extract bounding box and confidence
                            float bx = Sigmoid(predictionVector[0]); // x offset
                            float by = Sigmoid(predictionVector[1]); // y offset
                            float bw = (float)Math.Exp(predictionVector[2]) * anchors[anchorCounter + anchor].Width;
                            float bh = (float)Math.Exp(predictionVector[3]) * anchors[anchorCounter + anchor].Height;
                            float confidence = Sigmoid(predictionVector[4]);

                            // Skip low-confidence predictions
                            if (confidence < confidenceThreshold)
                            {
                                continue;
                            }

                            // Get class probabilities
                            var classProbs = predictionVector.Skip(5).Select(Sigmoid).ToArray();
                            float maxProb = classProbs.Max();
                            int classIndex = Array.IndexOf(classProbs, maxProb);

                            // Skip if class probability is low
                            if (maxProb * confidence < confidenceThreshold)
                            {
                                continue;
                            }

                            // Adjust bounding box to image dimensions
                            bx = (bx + j) * (inputWidth / gridX); // Convert to absolute x
                            by = (by + i) * (inputHeight / gridY); // Convert to absolute y
                            bw *= inputWidth / 416;          // Normalize to input width
                            bh *= inputHeight / 416;         // Normalize to input height

                            float scale = Math.Min((float)inputWidth / originalWidth, (float)inputHeight / originalHeight);
                            int offsetX = (inputWidth - (int)(originalWidth * scale)) / 2;
                            int offsetY = (inputHeight - (int)(originalHeight * scale)) / 2;

                            float xmin = (bx - bw / 2 - offsetX) / scale;
                            float ymin = (by - bh / 2 - offsetY) / scale;
                            float xmax = (bx + bw / 2 - offsetX) / scale;
                            float ymax = (by + bh / 2 - offsetY) / scale;

                            // Define your class labels (replace with your model's labels)
                            string[] labels = RCNNLabelMap.Labels.Skip(1).ToArray();

                            // Add prediction
                            predictions.Add(new Prediction
                            {
                                Box = new Box(xmin, ymin, xmax, ymax),
                                Label = labels[classIndex], // Use label from the provided labels array
                                Confidence = confidence * maxProb
                            });
                        }
                    }
                }

                // Increment anchorCounter for the next grid level
                anchorCounter += numAnchors;
            }

            return predictions;
        }

        private float Sigmoid(float x)
        {
            return 1f / (1f + (float)Math.Exp(-x));
        }

        private List<Prediction> ApplyNms(List<Prediction> predictions, float nmsThreshold)
        {
            var filteredPredictions = new List<Prediction>();

            // Group predictions by class
            var groupedPredictions = predictions.GroupBy(p => p.Label);

            foreach (var group in groupedPredictions)
            {
                var sortedGroup = group.OrderByDescending(p => p.Confidence).ToList();

                while (sortedGroup.Count > 0)
                {
                    // Take the highest confidence prediction
                    var bestPrediction = sortedGroup[0];
                    filteredPredictions.Add(bestPrediction);
                    sortedGroup.RemoveAt(0);

                    // Remove overlapping predictions
                    sortedGroup = sortedGroup
                        .Where(p => IoU(bestPrediction.Box!, p.Box!) < nmsThreshold)
                        .ToList();
                }
            }

            return filteredPredictions;
        }

        // Function to compute Intersection Over Union (IoU)
        private float IoU(Box boxA, Box boxB)
        {
            float x1 = Math.Max(boxA.Xmin, boxB.Xmin);
            float y1 = Math.Max(boxA.Ymin, boxB.Ymin);
            float x2 = Math.Min(boxA.Xmax, boxB.Xmax);
            float y2 = Math.Min(boxA.Ymax, boxB.Ymax);

            float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float union = (boxA.Xmax - boxA.Xmin) * (boxA.Ymax - boxA.Ymin) +
                          (boxB.Xmax - boxB.Xmin) * (boxB.Ymax - boxB.Ymin) -
                          intersection;

            return intersection / union;
        }

        private void RenderPredictions(Bitmap image, List<Prediction> predictions)
        {
            BitmapFunctions.DrawPredictions(image, predictions);

            BitmapImage bitmapImage = new();
            using (MemoryStream memoryStream = new())
            {
                image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

                memoryStream.Position = 0;

                bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
            }

            DefaultImage.Source = bitmapImage;
            NarratorHelper.AnnounceImageChanged(DefaultImage, "Image changed: objects detected."); // <exclude-line>
        }
    }
}