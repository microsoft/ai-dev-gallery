﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class VaeDecoder : IDisposable
{
    private InferenceSession? vaeDecoderInferenceSession;
    private bool disposedValue;

    private VaeDecoder()
    {
    }

    public static async Task<VaeDecoder> CreateAsync(
        string modelPath,
        ExecutionProviderDevicePolicy? policy,
        string? device,
        bool compileOption)
    {
        var instance = new VaeDecoder();
        instance.vaeDecoderInferenceSession = await instance.GetInferenceSession(modelPath, policy, device, compileOption);
        return instance;
    }

    private Task<InferenceSession> GetInferenceSession(string modelPath, ExecutionProviderDevicePolicy? policy, string? device, bool compileOption)
    {
        return Task.Run(async () =>
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("Model file not found.", modelPath);
            }

            Microsoft.Windows.AI.MachineLearning.Infrastructure infrastructure = new();

            try
            {
                await infrastructure.DownloadPackagesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to download packages: {ex.Message}");
            }

            await infrastructure.RegisterExecutionProviderLibrariesAsync();

            SessionOptions sessionOptions = new();
            sessionOptions.RegisterOrtExtensions();

            if (policy != null)
            {
                sessionOptions.SetEpSelectionPolicy(policy.Value);
            }
            else if (device != null)
            {
                sessionOptions.AppendExecutionProviderFromEpName(device);

                if (compileOption)
                {
                    modelPath = sessionOptions.GetCompiledModel(modelPath, device) ?? modelPath;
                }
            }

            InferenceSession inferenceSession = new(modelPath, sessionOptions);
            return inferenceSession;
        });
    }

    public Tensor<float>? Decoder(List<NamedOnnxValue> input)
    {
        if (vaeDecoderInferenceSession == null)
        {
            throw new InvalidOperationException("VaeDecoder is not initialized.");
        }

        // Run session and send the input data in to get inference output.
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output = vaeDecoderInferenceSession.Run(input);
        var result = output[0].AsTensor<float>().Clone();

        return result;
    }

    // create method to convert float array to an image with imagesharp
    public Bitmap ConvertToImage(Tensor<float> output, StableDiffusionConfig config)
    {
        Bitmap bitmap = new(config.Width, config.Height);

        // convert tensor result to bitmap
        for (int y = 0; y < config.Height; y++)
        {
            for (int x = 0; x < config.Width; x++)
            {
                // Assuming imageTensor has shape [1, 3, height, width] for RGB
                byte r = (byte)Math.Round(Math.Clamp(output[0, 0, y, x] / 2 + 0.5f, 0f, 1f) * 255);
                byte g = (byte)Math.Round(Math.Clamp(output[0, 1, y, x] / 2 + 0.5f, 0f, 1f) * 255);
                byte b = (byte)Math.Round(Math.Clamp(output[0, 2, y, x] / 2 + 0.5f, 0f, 1f) * 255);

                // Set pixel in bitmap
                bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }

        return bitmap;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing && vaeDecoderInferenceSession != null)
            {
                vaeDecoderInferenceSession.Dispose();
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