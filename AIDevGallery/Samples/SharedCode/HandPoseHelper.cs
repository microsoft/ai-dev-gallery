// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AIDevGallery.Samples.SharedCode;

internal readonly record struct HandKeypoint(float X, float Y, double Score);

internal class HandPoseHelper
{
    // The model takes one 256 x 256 RGB image (NCHW, mean [123.675, 116.28, 103.53] and standard deviation [58.395, 57.12, 57.375] on the 0-255 scale).
    public const int InputSize = 256;

    // The model returns two outputs of shape [1, 21, 512]: for each of the 21 key points, scores over 512 positions along x and along y (two positions per input pixel).
    public const int KeypointCount = 21;
    public const int PositionsPerPixel = 2;

    // Key points with a score at or below this value are not drawn.
    public const double ScoreThreshold = 0.2;

    private const float MarkerRatio = 0.012f;

    // Key point order: 0 wrist, 1-4 thumb, 5-8 index finger, 9-12 middle finger, 13-16 ring finger, 17-20 little finger, each finger from its base to its tip.
    private static readonly (int Start, int End)[] Connections =
    [
        (0, 1), (1, 2), (2, 3), (3, 4),
        (0, 5), (5, 6), (6, 7), (7, 8),
        (0, 9), (9, 10), (10, 11), (11, 12),
        (0, 13), (13, 14), (14, 15), (15, 16),
        (0, 17), (17, 18), (18, 19), (19, 20)
    ];

    private static readonly Color[] FingerColors =
    [
        Color.FromArgb(255, 128, 0),
        Color.FromArgb(255, 153, 255),
        Color.FromArgb(102, 178, 255),
        Color.FromArgb(255, 51, 51),
        Color.FromArgb(0, 255, 0)
    ];

    // Returns the largest square in the middle of an image of the given size.
    public static Rectangle GetCenterSquare(int width, int height)
    {
        int side = Math.Min(width, height);
        return new Rectangle((width - side) / 2, (height - side) / 2, side, side);
    }

    // simccX and simccY hold the two model outputs: for each key point in turn, the scores of all positions along x (or y).
    // A key point is the position with the highest score on each axis, divided by two; its score is the mean of those two scores.
    public static List<HandKeypoint> DecodeKeypoints(ReadOnlySpan<float> simccX, ReadOnlySpan<float> simccY)
    {
        if (simccX.Length == 0 || simccX.Length % KeypointCount != 0)
        {
            throw new ArgumentException("The output must hold the same number of scores for each of the 21 key points.", nameof(simccX));
        }

        if (simccY.Length == 0 || simccY.Length % KeypointCount != 0)
        {
            throw new ArgumentException("The output must hold the same number of scores for each of the 21 key points.", nameof(simccY));
        }

        int positionsX = simccX.Length / KeypointCount;
        int positionsY = simccY.Length / KeypointCount;
        List<HandKeypoint> keypoints = new(KeypointCount);

        for (int i = 0; i < KeypointCount; i++)
        {
            ReadOnlySpan<float> scoresX = simccX.Slice(i * positionsX, positionsX);
            ReadOnlySpan<float> scoresY = simccY.Slice(i * positionsY, positionsY);
            int bestX = ArgMax(scoresX);
            int bestY = ArgMax(scoresY);
            double score = ((double)scoresX[bestX] + scoresY[bestY]) / 2;
            keypoints.Add(new HandKeypoint((float)bestX / PositionsPerPixel, (float)bestY / PositionsPerPixel, score));
        }

        return keypoints;
    }

    // Maps key points from the model input (InputSize x InputSize) back to the square of the picture they were taken from.
    public static List<HandKeypoint> MapToImage(IReadOnlyList<HandKeypoint> keypoints, Rectangle square)
    {
        float scale = (float)square.Width / InputSize;
        List<HandKeypoint> mapped = new(keypoints.Count);

        foreach (HandKeypoint keypoint in keypoints)
        {
            mapped.Add(keypoint with { X = square.X + (keypoint.X * scale), Y = square.Y + (keypoint.Y * scale) });
        }

        return mapped;
    }

    // Returns a copy of the image with the key points that score above ScoreThreshold and the lines between them.
    public static Bitmap RenderPredictions(Bitmap image, IReadOnlyList<HandKeypoint> keypoints)
    {
        if (keypoints.Count != KeypointCount)
        {
            throw new ArgumentException("Expected one entry for each of the 21 key points.", nameof(keypoints));
        }

        Bitmap output = new(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(output);
        graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        float markerSize = Math.Max(4f, Math.Min(image.Width, image.Height) * MarkerRatio);
        using Pen linePen = new(Color.White, markerSize / 2);

        foreach (var (start, end) in Connections)
        {
            if (keypoints[start].Score > ScoreThreshold && keypoints[end].Score > ScoreThreshold)
            {
                linePen.Color = GetColor(end);
                graphics.DrawLine(linePen, keypoints[start].X, keypoints[start].Y, keypoints[end].X, keypoints[end].Y);
            }
        }

        for (int i = 0; i < keypoints.Count; i++)
        {
            if (keypoints[i].Score > ScoreThreshold)
            {
                using SolidBrush brush = new(GetColor(i));
                graphics.FillEllipse(brush, keypoints[i].X - (markerSize / 2), keypoints[i].Y - (markerSize / 2), markerSize, markerSize);
            }
        }

        return output;
    }

    // Returns the index of the first highest value.
    private static int ArgMax(ReadOnlySpan<float> values)
    {
        int best = 0;

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }

        return best;
    }

    // The wrist is white and each finger has its own color.
    private static Color GetColor(int keypointIndex)
    {
        return keypointIndex == 0 ? Color.White : FingerColors[(keypointIndex - 1) / 4];
    }
}