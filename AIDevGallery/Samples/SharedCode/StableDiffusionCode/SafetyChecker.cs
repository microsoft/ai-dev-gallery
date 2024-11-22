// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class SafetyChecker : IDisposable
{
    private readonly InferenceSession safetyCheckerInferenceSession;
    private readonly SessionOptions sessionOptions;
    private bool disposedValue;

    public SafetyChecker(string safetyPath, SessionOptions options)
    {
        sessionOptions = options;
        safetyCheckerInferenceSession = new InferenceSession(safetyPath, sessionOptions);
    }

    public bool IsNotSafe(Tensor<float> resultImage, StableDiffusionConfig config)
    {
        // clip input
        var inputTensor = ClipImageFeatureExtractor(resultImage, config);

        // images input
        var inputImagesTensor = ReorderTensor(inputTensor);

        var input = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("clip_input", inputTensor),
            NamedOnnxValue.CreateFromTensor("images", inputImagesTensor)
        };

        // Run session and send the input data in to get inference output.
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output = safetyCheckerInferenceSession.Run(input);
        var result = output[1].AsEnumerable<bool>().First();

        return result;
    }

    private DenseTensor<float> ReorderTensor(Tensor<float> inputTensor)
    {
        // reorder from batch channel height width to batch height width channel
        var inputImagesTensor = new DenseTensor<float>([1, 224, 224, 3]);
        for (int y = 0; y < inputTensor.Dimensions[2]; y++)
        {
            for (int x = 0; x < inputTensor.Dimensions[3]; x++)
            {
                inputImagesTensor[0, y, x, 0] = inputTensor[0, 0, y, x];
                inputImagesTensor[0, y, x, 1] = inputTensor[0, 1, y, x];
                inputImagesTensor[0, y, x, 2] = inputTensor[0, 2, y, x];
            }
        }

        return inputImagesTensor;
    }

    private static Tensor<float> ClipImageFeatureExtractor(Tensor<float> imageTensor, StableDiffusionConfig config)
    {
        using Bitmap bitmap = new(config.Width, config.Height);

        // convert tensor result to bitmap
        for (int y = 0; y < config.Height; y++)
        {
            for (int x = 0; x < config.Width; x++)
            {
                // Assuming imageTensor has shape [1, 3, height, width] for RGB
                byte r = (byte)Math.Round(Math.Clamp(imageTensor[0, 0, y, x] / 2 + 0.5f, 0f, 1f) * 255);
                byte g = (byte)Math.Round(Math.Clamp(imageTensor[0, 1, y, x] / 2 + 0.5f, 0f, 1f) * 255);
                byte b = (byte)Math.Round(Math.Clamp(imageTensor[0, 2, y, x] / 2 + 0.5f, 0f, 1f) * 255);

                // Set pixel in bitmap
                bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }

        // Resize image
        using var resizeBitmap = BitmapFunctions.ResizeBitmap(bitmap, 224, 224);

        // Preprocess image
        var input = new DenseTensor<float>([1, 3, 224, 224]);
        var processedInput = BitmapFunctions.PreprocessBitmapWithStdDev(resizeBitmap, input);

        return processedInput;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                safetyCheckerInferenceSession.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}