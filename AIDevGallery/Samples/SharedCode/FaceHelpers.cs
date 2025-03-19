// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode;

internal class FaceHelpers
{
    public static List<Prediction> PostprocessFacialResults(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int originalWidth, int originalHeight, float threshold = 0.5f)
    {
        var heatmap = results.ElementAt(0).AsTensor<float>();
        var bbox = results.ElementAt(1).AsTensor<float>();

        Prediction? bestPrediction = null;
        float bestScore = 0;

        int modelInputHeight = heatmap.Dimensions[2] * 8;
        int modelInputWidth = heatmap.Dimensions[3] * 8;

        float scaleX = (float)originalWidth / modelInputWidth;
        float scaleY = (float)originalHeight / modelInputHeight;

        for (int y = 0; y < heatmap.Dimensions[2]; y++)
        {
            for (int x = 0; x < heatmap.Dimensions[3]; x++)
            {
                float score = Sigmoid(heatmap[0, 0, y, x]);
                if (score > threshold)
                {
                    float x_off = bbox[0, 0, y, x];
                    float y_off = bbox[0, 1, y, x];
                    float r_off = bbox[0, 2, y, x];
                    float b_off = bbox[0, 3, y, x];

                    float left = (x * 8 - x_off * 8) * scaleX;
                    float top = (y * 8 - y_off * 8) * scaleY;
                    float right = (x * 8 + r_off * 8) * scaleX;
                    float bottom = (y * 8 + b_off * 8) * scaleY;

                    bestPrediction = new Prediction
                    {
                        Box = new Box(left, top, right, bottom),
                        Label = "face",
                        Confidence = score
                    };

                    bestScore = score;
                }
            }
        }

        return bestPrediction != null ? new List<Prediction> { bestPrediction } : new List<Prediction>();
    }

    private static float Sigmoid(float x)
    {
        return 1 / (1 + (float)Math.Exp(-x));
    }
}