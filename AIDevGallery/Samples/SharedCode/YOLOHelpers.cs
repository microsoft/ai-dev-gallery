// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode
{
    internal class YOLOHelpers
    {
        public static List<Prediction> ExtractPredictions(List<Tensor<float>> gridTensors, List<(float Width, float Height)> anchors, int inputWidth, int inputHeight, int originalWidth, int originalHeight)
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

        private static float Sigmoid(float x)
        {
            return 1f / (1f + (float)Math.Exp(-x));
        }

        public static List<Prediction> ApplyNms(List<Prediction> predictions, float nmsThreshold)
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
        private static float IoU(Box boxA, Box boxB)
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