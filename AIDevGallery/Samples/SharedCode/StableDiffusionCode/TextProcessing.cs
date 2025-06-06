// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class TextProcessing : IDisposable
{
    private InferenceSession? tokenizerInferenceSession;
    private InferenceSession? encoderInferenceSession;
    private bool disposedValue;

    private TextProcessing()
    {
    }

    public static async Task<TextProcessing> CreateAsync(
        StableDiffusionConfig config,
        string tokenizerPath,
        string encoderPath,
        ExecutionProviderDevicePolicy? policy,
        string? device,
        bool compileOption)
    {
        var instance = new TextProcessing();
        instance.tokenizerInferenceSession = await instance.GetInferenceSession(config, tokenizerPath, policy, device, compileOption);
        instance.encoderInferenceSession = await instance.GetInferenceSession(config, encoderPath, policy, device, compileOption);
        return instance;
    }

    private Task<InferenceSession> GetInferenceSession(StableDiffusionConfig config, string modelPath, ExecutionProviderDevicePolicy? policy, string? device, bool compileOption)
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

            sessionOptions.AddFreeDimensionOverrideByName("batch", 1);
            sessionOptions.AddFreeDimensionOverrideByName("channels", 3);
            sessionOptions.AddFreeDimensionOverrideByName("height", config.Height);
            sessionOptions.AddFreeDimensionOverrideByName("width", config.Width);

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

    public DenseTensor<float> PreprocessText(string prompt)
    {
        // Load the tokenizer and text encoder to tokenize and encode the text.
        var textTokenized = TokenizeText(prompt);
        var textPromptEmbeddings = TextEncoder(textTokenized);

        // Create uncond_input of blank tokens
        var uncondInputTokens = CreateUncondInput();
        var uncondEmbedding = TextEncoder(uncondInputTokens);

        // Concant textEmeddings and uncondEmbedding
        DenseTensor<float> textEmbeddings = new([2, 77, 768]);

        for (var i = 0; i < textPromptEmbeddings.Length; i++)
        {
            textEmbeddings[0, i / 768, i % 768] = uncondEmbedding[i];
            textEmbeddings[1, i / 768, i % 768] = textPromptEmbeddings[i];
        }

        return textEmbeddings;
    }

    public int[] TokenizeText(string text)
    {
        if (tokenizerInferenceSession == null)
        {
            throw new InvalidOperationException("Tokenizer is not initialized.");
        }

        // Create an InferenceSession from the onnx clip tokenizer.
        var inputTensor = new DenseTensor<string>(new string[] { text }, [1]);
        var inputString = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("string_input", inputTensor) };

        // Run session and send the input data in to get inference output.
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> tokens = tokenizerInferenceSession.Run(inputString);

        var ids = tokens[0].AsEnumerable<long>();

        // Cast inputIds to Int32
        var inputIds = ids.Select(x => (int)x).ToArray();

        var modelMaxLength = 77;

        if (inputIds.Length > modelMaxLength)
        {
            throw new ArgumentException($"Input text is too long. Maximum allowed tokens: {modelMaxLength}, but received: {inputIds.Length}.");
        }

        // Pad array with 49407 until length is modelMaxLength
        if (inputIds.Length < modelMaxLength)
        {
            var pad = Enumerable.Repeat(49407, modelMaxLength - inputIds.Length);
            inputIds = [.. inputIds.Concat(pad)];
        }

        return inputIds;
    }

    public int[] CreateUncondInput()
    {
        // Create an array of empty tokens for the unconditional input.
        var blankTokenValue = 49407;
        var modelMaxLength = 77;
        var inputIds = new List<int>
        {
            49406
        };
        var pad = Enumerable.Repeat(blankTokenValue, modelMaxLength - inputIds.Count);
        inputIds.AddRange(pad);

        return [.. inputIds];
    }

    public float[] TextEncoder(int[] tokenizedInput)
    {
        if (encoderInferenceSession == null)
        {
            throw new InvalidOperationException("Encoder is not initialized.");
        }

        // Create input tensor.
        var input_ids = TensorHelper.CreateTensor(tokenizedInput, [1, tokenizedInput.Length]);

        var input = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input_ids", input_ids) };

        // Run inference.
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> encoded = encoderInferenceSession.Run(input);

        var lastHiddenState = encoded[0].AsEnumerable<float>().ToArray();
        var lastHiddenStateTensor = TensorHelper.CreateTensor(lastHiddenState, [1, 77, 768]);

        return [.. lastHiddenStateTensor];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                tokenizerInferenceSession!.Dispose();
                encoderInferenceSession!.Dispose();
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