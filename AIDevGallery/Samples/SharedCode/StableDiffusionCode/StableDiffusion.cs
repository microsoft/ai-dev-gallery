// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class StableDiffusion : IDisposable
{
    private InferenceSession UnetInferenceSession { get; }

    private readonly TextProcessing textProcessor;
    private readonly VaeDecoder vaeDecoder;
    private readonly SafetyChecker safetyChecker;
    private SessionOptions SessionOptions { get; }

    private readonly StableDiffusionConfig config = new()
    {
        // Number of denoising steps
        NumInferenceSteps = 15,

        // Scale for classifier-free guidance
        GuidanceScale = 7.5
    };

    private bool disposedValue;
    private bool running;

    public StableDiffusion(string textEncoderPath, string unetPath, string vaeDecoderPath, string safetyPath, HardwareAccelerator hardwareAccelerator)
    {
        config.TextEncoderModelPath = textEncoderPath;
        config.UnetModelPath = unetPath;
        config.VaeDecoderModelPath = vaeDecoderPath;
        config.SafetyModelPath = safetyPath;

        // Tokenizer model isn't built in with the SD code, need to pull it in from local folder.
        // Need to solve this later!
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory + "Samples\\Open Source Models\\Stable Diffusion";
        string tokenizerPath = Path.Combine(currentDirectory, config.TokenizerModelPath);

        config.DeviceId = DeviceUtils.GetBestDeviceId();

        SessionOptions = config.GetSessionOptionsForEp(hardwareAccelerator);

        textProcessor = new TextProcessing(tokenizerPath, config.TextEncoderModelPath, SessionOptions);
        UnetInferenceSession = new InferenceSession(config.UnetModelPath, SessionOptions);
        vaeDecoder = new VaeDecoder(config.VaeDecoderModelPath, SessionOptions);
        safetyChecker = new SafetyChecker(config.SafetyModelPath, SessionOptions);
    }

    public StableDiffusion(string modelFolder, HardwareAccelerator hardwareAccelerator)
        : this(@$"{modelFolder}\text_encoder\model.onnx", @$"{modelFolder}\unet\model.onnx", @$"{modelFolder}\vae_decoder\model.onnx", @$"{modelFolder}\safety_checker\model.onnx", hardwareAccelerator)
    {
    }

    public static List<NamedOnnxValue> CreateUnetModelInput(Tensor<float> encoderHiddenStates, Tensor<float> sample, long timeStep)
    {
        var input = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHiddenStates),
            NamedOnnxValue.CreateFromTensor("sample", sample),
            NamedOnnxValue.CreateFromTensor("timestep", new DenseTensor<long>(new long[] { timeStep }, [ 1 ]))
        };

        return input;
    }

    public static Tensor<float> GenerateLatentSample(int height, int width, int seed, float initNoiseSigma)
    {
        var random = new Random(seed);
        var batchSize = 1;
        var channels = 4;
        ReadOnlySpan<int> dimensions = [batchSize, channels, height / 8, width / 8];
        var latentsArray = new float[dimensions[0] * dimensions[1] * dimensions[2] * dimensions[3]];

        for (int i = 0; i < latentsArray.Length; i++)
        {
            // Generate a random number from a normal distribution with mean 0 and variance 1
            var u1 = random.NextDouble(); // Uniform(0,1) random number
            var u2 = random.NextDouble(); // Uniform(0,1) random number
            var radius = Math.Sqrt(-2.0 * Math.Log(u1)); // Radius of polar coordinates
            var theta = 2.0 * Math.PI * u2; // Angle of polar coordinates
            var standardNormalRand = radius * Math.Cos(theta); // Standard normal random number

            // add noise to latents with * scheduler.init_noise_sigma
            // generate randoms that are negative and positive
            latentsArray[i] = (float)standardNormalRand * initNoiseSigma;
        }

        return TensorHelper.CreateTensor(latentsArray, dimensions.ToArray());
    }

    private static Tensor<float> PerformGuidance(Tensor<float> noisePred, Tensor<float> noisePredText, double guidanceScale)
    {
        for (int i = 0; i < noisePred.Dimensions[0]; i++)
        {
            for (int j = 0; j < noisePred.Dimensions[1]; j++)
            {
                for (int k = 0; k < noisePred.Dimensions[2]; k++)
                {
                    for (int l = 0; l < noisePred.Dimensions[3]; l++)
                    {
                        noisePred[i, j, k, l] = noisePred[i, j, k, l] + (float)guidanceScale * (noisePredText[i, j, k, l] - noisePred[i, j, k, l]);
                    }
                }
            }
        }

        return noisePred;
    }

    public Bitmap? Inference(string prompt, CancellationToken token)
    {
        // Preprocess text
        token.ThrowIfCancellationRequested();
        var textEmbeddings = textProcessor.PreprocessText(prompt);
        token.ThrowIfCancellationRequested();

        var scheduler = new LMSDiscreteScheduler();

        var timesteps = scheduler.SetTimesteps(config.NumInferenceSteps);

        // If you use the same seed, you will get the same image result.
        var seed = new Random().Next();

        // create latent tensor
        var latents = GenerateLatentSample(config.Height, config.Width, seed, scheduler.InitNoiseSigma);

        // Unet Loop
        for (int t = 0; t < timesteps.Length; t++)
        {
            var latentModelInput = TensorHelper.Duplicate([.. latents], [2, 4, config.Height / 8, config.Width / 8]);

            latentModelInput = scheduler.ScaleInput(latentModelInput, timesteps[t]);

            var input = CreateUnetModelInput(textEmbeddings, latentModelInput, timesteps[t]);

            token.ThrowIfCancellationRequested();

            try
            {
                running = true;
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output = UnetInferenceSession.Run(input);
                var outputTensor = output[0].AsTensor<float>();

                var splitTensors = TensorHelper.SplitTensor(outputTensor!, [1, 4, config.Height / 8, config.Width / 8]);
                var noisePred = splitTensors.Item1;
                var noisePredText = splitTensors.Item2;

                noisePred = PerformGuidance(noisePred, noisePredText, config.GuidanceScale);

                latents = scheduler.Step(noisePred, timesteps[t], latents);
            }
            catch
            {
                return null;
            }

            running = false;

            token.ThrowIfCancellationRequested();
        }

        // Scale and decode the image latents with vae.
        latents = TensorHelper.MultipleTensorByFloat([.. latents], 1.0f / 0.18215f, latents.Dimensions.ToArray());
        var decoderInput = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("latent_sample", latents) };

        token.ThrowIfCancellationRequested();
        Tensor<float>? imageResultTensor = vaeDecoder.Decoder(decoderInput);
        if (imageResultTensor == null)
        {
            throw new ArgumentException("An error occurred during image generation. Please try again.");
        }

        token.ThrowIfCancellationRequested();
        var isNotSafe = safetyChecker.IsNotSafe(imageResultTensor, config);
        token.ThrowIfCancellationRequested();
        if (isNotSafe)
        {
            throw new InvalidOperationException("The generated image did not pass the safety checker. Please try again.");
        }

        var image = vaeDecoder.ConvertToImage(imageResultTensor, config);
        return image;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue && !running)
        {
            if (disposing)
            {
                textProcessor.Dispose();
                UnetInferenceSession.Dispose();
                vaeDecoder.Dispose();
                safetyChecker.Dispose();
                SessionOptions.Dispose();
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