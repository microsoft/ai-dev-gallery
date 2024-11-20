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
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIDevGallery.Samples.OpenSourceModels.MultiHRNetPose
{
    [GallerySample(
        Model1Types = [ModelType.HRNetPose],
        Model2Types = [ModelType.YOLO],
        Scenario = ScenarioType.ImageDetectPoses,
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
        Name = "Multiple Pose Detection",
        Id = "9b74ccc0-f5f7-430f-bed0-712ffc063508",
        Icon = "\uE8B3")]
    internal sealed partial class Multipose : Page
    {
        private InferenceSession? _detectionSession;
        private InferenceSession? _poseSession;

        public Multipose()
        {
            this.Unloaded += (s, e) =>
            {
                _detectionSession?.Dispose();
                _poseSession?.Dispose();
            };

            this.Loaded += (s, e) => Page_Loaded(); // <exclude-line>
            this.InitializeComponent();
        }

        // <exclude>
        private void Page_Loaded()
        {
            UploadButton.Focus(FocusState.Programmatic);
        }

        // </exclude>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is MultiModelSampleNavigationParameters sampleParams)
            {
                await InitModels(sampleParams.ModelPaths[0], sampleParams.HardwareAccelerators[0], sampleParams.ModelPaths[1], sampleParams.HardwareAccelerators[1]);
                sampleParams.NotifyCompletion();

                await RunPipeline(Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "team.jpg"));
            }
        }

        private Task InitModels(string poseModelPath, HardwareAccelerator poseHardwareAccelerator, string detectionModelPath, HardwareAccelerator detectionHardwareAccelerator)
        {
            return Task.Run(() =>
            {
                if (_poseSession != null)
                {
                    return;
                }

                SessionOptions poseOptions = new();
                poseOptions.RegisterOrtExtensions();
                if (poseHardwareAccelerator == HardwareAccelerator.DML)
                {
                    poseOptions.AppendExecutionProvider_DML(DeviceUtils.GetBestDeviceId());
                }

                _poseSession = new InferenceSession(poseModelPath, poseOptions);

                if (_detectionSession != null)
                {
                    return;
                }

                SessionOptions detectionOptions = new();
                detectionOptions.RegisterOrtExtensions();
                if (detectionHardwareAccelerator == HardwareAccelerator.DML)
                {
                    detectionOptions.AppendExecutionProvider_DML(DeviceUtils.GetBestDeviceId());
                }

                _detectionSession = new InferenceSession(detectionModelPath, detectionOptions);
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
                // Call function to run inference and classify image
                UploadButton.Focus(FocusState.Programmatic);
                await RunPipeline(file.Path);
            }
        }

        private async Task RunPipeline(string filePath)
        {
            if (!File.Exists(filePath)) // Corrected from Path.Exists
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                DefaultImage.Source = new BitmapImage(new Uri(filePath));
                Loader.IsActive = true;
                Loader.Visibility = Visibility.Visible;
                UploadButton.Visibility = Visibility.Collapsed;
            });

            Bitmap originalImage = new(filePath);

            // Step 1: Detect where the "person" tag is found in the image
            List<Prediction> predictions = await FindPeople(originalImage);
            predictions = predictions.Where(x => x.Label == "person").ToList();
            int i = 0;
            foreach (var prediction in predictions)
            {
                if (prediction.Box != null)
                {
                    Bitmap croppedImage = CropImage(originalImage, prediction.Box);

                    Bitmap poseOverlay = await DetectPose(croppedImage);

                    originalImage = OverlayImage(originalImage, poseOverlay, prediction.Box);

                    //if(i == 2)
                    //{
                    //    break;
                    //}
                    i++;
                }
            }

            // Convert the processed image back to BitmapImage asynchronously
            BitmapImage outputImage = BitmapFunctions.ConvertBitmapToBitmapImageAsync(originalImage);

            DispatcherQueue.TryEnqueue(() =>
            {
                DefaultImage.Source = outputImage;
                Loader.IsActive = false;
                Loader.Visibility = Visibility.Collapsed;
                UploadButton.Visibility = Visibility.Visible;
            });

            originalImage.Dispose();
        }

        private Bitmap OverlayImage(Bitmap originalImage, Bitmap overlay, Box box)
        {
            using Graphics graphics = Graphics.FromImage(originalImage);

            // Scale the overlay to match the bounding box size
            graphics.DrawImage(overlay, new Rectangle(
                (int)box.Xmin,
                (int)box.Ymin,
                (int)(box.Xmax - box.Xmin),
                (int)(box.Ymax - box.Ymin)
            ));

            return originalImage;
        }


        private async Task<Bitmap> DetectPose(Bitmap image)
        {
            if (image == null)
            {
                return null;
            }

            var inputName = _poseSession!.InputNames[0];
            var inputDimensions = _poseSession.InputMetadata[inputName].Dimensions;

            var originalImageWidth = image.Width;
            var originalImageHeight = image.Height;

            int modelInputWidth = inputDimensions[2];
            int modelInputHeight = inputDimensions[3];

            // Resize Bitmap
            using Bitmap resizedImage = BitmapFunctions.ResizeBitmap(image, modelInputWidth, modelInputHeight);

            var predictions = await Task.Run(() =>
            {
                // Preprocessing
                Tensor<float> input = new DenseTensor<float>([1, 3, modelInputWidth, modelInputHeight]);
                input = BitmapFunctions.PreprocessBitmapWithStdDev(resizedImage, input);

                // Setup inputs
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, input)
                };

                // Run inference
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _poseSession!.Run(inputs);
                var heatmaps = results[0].AsTensor<float>();
                List<(float X, float Y)> keypointCoordinates = PostProcessResults(heatmaps, originalImageWidth, originalImageHeight);
                return keypointCoordinates;
            });

            // Render predictions and create output bitmap
            return RenderPredictions(image, predictions);
        }

        private List<(float X, float Y)> PostProcessResults(Tensor<float> heatmaps, float originalWidth, float originalHeight)
        {
            List<(float X, float Y)> keypointCoordinates = [];

            // Scaling factors from heatmap (64x48) directly to original image size
            float scale_x = originalWidth / 64f;
            float scale_y = originalHeight / 48f;

            int numKeypoints = heatmaps.Dimensions[1];
            int heatmapWidth = heatmaps.Dimensions[2];
            int heatmapHeight = heatmaps.Dimensions[3];

            for (int i = 0; i < numKeypoints; i++)
            {
                float maxVal = float.MinValue;
                int maxX = 0, maxY = 0;

                for (int x = 0; x < heatmapWidth; x++)
                {
                    for (int y = 0; y < heatmapHeight; y++)
                    {
                        float value = heatmaps[0, i, y, x];
                        if (value > maxVal)
                        {
                            maxVal = value;
                            maxX = x;
                            maxY = y;
                        }
                    }
                }

                float scaledX = maxX * scale_x;
                float scaledY = maxY * scale_y;

                keypointCoordinates.Add((scaledX, scaledY));
            }

            return keypointCoordinates;
        }

        private Bitmap RenderPredictions(Bitmap originalImage, List<(float X, float Y)> keypoints)
        {
            Bitmap outputImage = new(originalImage);

            using (Graphics g = Graphics.FromImage(outputImage))
            {
                int markerSize = (int)((originalImage.Width + originalImage.Height) * 0.02 / 2);
                Brush brush = Brushes.Red;

                using Pen linePen = new(Color.Blue, 5);
                List<(int StartIdx, int EndIdx)> connections =
                [
                    (5, 6),   // Left shoulder to right shoulder
                        (5, 7),   // Left shoulder to left elbow
                        (7, 9),   // Left elbow to left wrist
                        (6, 8),   // Right shoulder to right elbow
                        (8, 10),  // Right elbow to right wrist
                        (11, 12), // Left hip to right hip
                        (5, 11),  // Left shoulder to left hip
                        (6, 12),  // Right shoulder to right hip
                        (11, 13), // Left hip to left knee
                        (13, 15), // Left knee to left ankle
                        (12, 14), // Right hip to right knee
                        (14, 16) // Right knee to right ankle
                ];

                foreach (var (startIdx, endIdx) in connections)
                {
                    var (startPointX, startPointY) = keypoints[startIdx];
                    var (endPointX, endPointY) = keypoints[endIdx];

                    g.DrawLine(linePen, startPointX, startPointY, endPointX, endPointY);
                }

                foreach (var (x, y) in keypoints)
                {
                    g.FillEllipse(brush, x - markerSize / 2, y - markerSize / 2, markerSize, markerSize);
                }
            }

            return outputImage;
        }

        private Bitmap CropImage(Bitmap originalImage, Box box)
        {
            int xmin = Math.Max(0, (int)box.Xmin);
            int ymin = Math.Max(0, (int)box.Ymin);
            int width = Math.Min(originalImage.Width - xmin, (int)(box.Xmax - box.Xmin));
            int height = Math.Min(originalImage.Height - ymin, (int)(box.Ymax - box.Ymin));

            Rectangle cropRectangle = new(xmin, ymin, width, height);
            return originalImage.Clone(cropRectangle, originalImage.PixelFormat);
        }

        private async Task<List<Prediction>> FindPeople(Bitmap image)
        {
            if (_detectionSession == null)
            {
                return [];
            }

            int originalWidth = image.Width;
            int originalHeight = image.Height;

            var predictions = await Task.Run(() =>
            {
                // Set up
                var inputName = _detectionSession.InputNames[0];
                var inputDimensions = _detectionSession.InputMetadata[inputName].Dimensions;

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
                var inputMetadataName = _detectionSession!.InputNames[0];
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputMetadataName, input)
                };

                // Run inference
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _detectionSession!.Run(inputs);

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

            return predictions;
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
    }
}