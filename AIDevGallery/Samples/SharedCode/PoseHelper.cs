// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Drawing;

namespace AIDevGallery.Samples.SharedCode;

internal class PoseHelper
{
    public static List<(float X, float Y)> PostProcessResults(Tensor<float> heatmaps, float originalWidth, float originalHeight, float outputWidth, float outputHeight)
    {
        List<(float X, float Y)> keypointCoordinates = [];

        // Scaling factors from heatmap (64x48) directly to original image size
        float scale_x = originalWidth / outputWidth;
        float scale_y = originalHeight / outputHeight;

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

    public static Bitmap RenderPredictions(Bitmap originalImage, List<(float X, float Y)> keypoints, float markerRatio, Bitmap? baseImage = null)
    {
        Bitmap outputImage = new(originalImage);

        using (Graphics g = Graphics.FromImage(outputImage))
        {
            // If refernce is multipose, use base image not cropped image for scaling
            // If reference is one person pose, use original image as base image isn't used.
            var imageValue = baseImage != null ? baseImage.Width + baseImage.Height : originalImage.Width + originalImage.Height;
            int markerSize = (int)(imageValue * markerRatio / 2);
            Brush brush = Brushes.Red;

            using Pen linePen = new(Color.Blue, markerSize / 2);
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
}