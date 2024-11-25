// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Samples.SharedCode;

internal class Prediction
{
    public Box? Box { get; set; }
    public required string Label { get; set; }
    public required float Confidence { get; set; }
}

internal class Box
{
    public float Xmin { get; set; }
    public float Ymin { get; set; }
    public float Xmax { get; set; }
    public float Ymax { get; set; }

    public Box(float xmin, float ymin, float xmax, float ymax)
    {
        Xmin = xmin;
        Ymin = ymin;
        Xmax = xmax;
        Ymax = ymax;
    }
}