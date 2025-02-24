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

    public static List<(float X, float Y)> PostProcessLandmarks(
    Tensor<float> landmarks,
    float originalWidth,
    float originalHeight,
    float inputWidth,      // e.g., the model’s input width
    float inputHeight)     // e.g., the model’s input height
    {
        List<(float X, float Y)> keypointCoordinates = new List<(float X, float Y)>();
        int numKeypoints = landmarks.Dimensions[1];

        for (int i = 0; i < numKeypoints; i++)
        {
            // Get normalized coordinates (assumed 0 to 1, relative to the model input)
            float normalizedX = landmarks[0, i, 0];
            float normalizedY = landmarks[0, i, 1];

            // First scale to the model input resolution
            float inputX = normalizedX * inputWidth;
            float inputY = normalizedY * inputHeight;

            // Then map from model input to original image dimensions
            float scaledX = inputX * (originalWidth / inputWidth);
            float scaledY = inputY * (originalHeight / inputHeight);

            keypointCoordinates.Add((scaledX, scaledY));
        }

        return keypointCoordinates;
    }

    public static Bitmap RenderPredictions(Bitmap image, List<(float X, float Y)> keypoints, float markerRatio, Bitmap? baseImage = null)
    {
        using (Graphics g = Graphics.FromImage(image))
        {
            // If refernce is multipose, use base image not cropped image for scaling
            // If reference is one person pose, use original image as base image isn't used.
            var averageOfWidthAndHeight = baseImage != null ? baseImage.Width + baseImage.Height : image.Width + image.Height;
            int markerSize = (int)(averageOfWidthAndHeight * markerRatio / 2);
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

        return image;
    }

    public static Bitmap RenderHandPredictions(Bitmap image, List<(float X, float Y)> keypoints, float markerRatio, string lr, Bitmap? baseImage = null)
    {
        using (Graphics g = Graphics.FromImage(image))
        {
            // Use the base image dimensions if provided, otherwise use the cropped image's dimensions
            var averageOfWidthAndHeight = baseImage != null ? baseImage.Width + baseImage.Height : image.Width + image.Height;
            int markerSize = (int)(averageOfWidthAndHeight * markerRatio / 2);
            Brush brush = Brushes.Red;
            using Pen linePen = new(Color.Blue, markerSize / 2);

            // Define hand connections based on a common hand landmark model (e.g., MediaPipe Hands)
            List<(int StartIdx, int EndIdx)> connections = new List<(int, int)>
        {
            // Thumb: Only connect thumb landmarks (excluding a connection from the wrist)
            (1, 2), (2, 3), (3, 4),

            // Index finger: Connect the finger joints
            (5, 6), (6, 7), (7, 8),

            // Middle finger: Connect the finger joints
            (9, 10), (10, 11), (11, 12),

            // Ring finger: Connect the finger joints
            (13, 14), (14, 15), (15, 16),

            // Pinky finger: Connect the finger joints
            (17, 18), (18, 19), (19, 20),

            // Circle connecting the lowest (base) points of the four fingers (index, middle, ring, pinky)
            (0, 1), (1, 5), (5, 9), (9, 13), (13, 17), (17, 0)
        };

            // Draw connections between keypoints
            foreach (var (startIdx, endIdx) in connections)
            {
                var (startPointX, startPointY) = keypoints[startIdx];
                var (endPointX, endPointY) = keypoints[endIdx];
                g.DrawLine(linePen, startPointX, startPointY, endPointX, endPointY);
            }

            // Draw keypoint markers
            foreach (var (x, y) in keypoints)
            {
                g.FillEllipse(brush, x - markerSize / 2, y - markerSize / 2, markerSize, markerSize);
            }

            // Draw the lr label box in the top-right corner
            // Define rectangle dimensions and position
            int rectWidth = markerSize * 2;
            int rectHeight = markerSize * 2;
            int margin = markerSize / 2;
            int rectX = image.Width - rectWidth - margin;
            int rectY = margin;

            // Draw a black filled rectangle
            g.FillRectangle(Brushes.Black, rectX, rectY, rectWidth, rectHeight);

            // Draw the lr text ("L" or "R") in white, centered in the rectangle
            using (Font font = new Font("Arial", markerSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF textSize = g.MeasureString(lr, font);
                float textX = rectX + (rectWidth - textSize.Width) / 2;
                float textY = rectY + (rectHeight - textSize.Height) / 2;
                g.DrawString(lr, font, Brushes.White, textX, textY);
            }
        }

        return image;
    }
}