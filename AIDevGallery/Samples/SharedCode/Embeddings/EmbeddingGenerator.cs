// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tensor = System.Numerics.Tensors.Tensor;

// 'System.Numerics.Tensors' is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable SYSLIB5001

namespace AIDevGallery.Samples.SharedCode;

internal partial class EmbeddingGenerator : IDisposable, IEmbeddingGenerator<string, Embedding<float>>
{
    [GeneratedRegex(@"[\u0000-\u001F\u007F-\uFFFF]")]
    private static partial Regex MyRegex();

    private readonly EmbeddingGeneratorMetadata _metadata;
    private readonly SessionOptions _sessionOptions;
    private readonly InferenceSession _inferenceSession;
    private readonly BertTokenizer _tokenizer;
    private readonly int _chunkSize = 128;

    public EmbeddingGenerator(string vocabPath, string modelPath, SessionOptions sessionOptions)
    {
        _metadata = new EmbeddingGeneratorMetadata("ORTEmbeddingGenerator", new Uri($"file://{modelPath}"), modelPath, 384);
        _sessionOptions = sessionOptions;

        _inferenceSession = new InferenceSession(modelPath, _sessionOptions);
        _tokenizer = BertTokenizer.Create(vocabPath);
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return InternalGenerateEmbeddings(values, null, options, cancellationToken);
    }

    private async Task<GeneratedEmbeddings<Embedding<float>>> InternalGenerateEmbeddings(IEnumerable<string> values, RunOptions? runOptions = null, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>();
        try
        {
            if (options?.Dimensions != null && options.Dimensions != 384)
            {
                throw new ArgumentException("Only 384 dimensions are supported.");
            }

            await Task.Run(
                async () =>
                {
                    bool ownsRunOptions = runOptions == null;
                    runOptions ??= new RunOptions();

                    float[][] vectors = await GetVectorsAsync(values, runOptions).ConfigureAwait(false);
                    generatedEmbeddings.AddRange(vectors.Select(x => new Embedding<float>(x)));

                    if (ownsRunOptions)
                    {
                        runOptions.Dispose();
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        return generatedEmbeddings;
    }

    public async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = values.Chunk(_chunkSize);

        using var runOptions = new RunOptions();

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GeneratedEmbeddings<Embedding<float>> embeddings = await InternalGenerateEmbeddings(chunk, runOptions, options, cancellationToken).ConfigureAwait(false);

            foreach (var embedding in embeddings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return embedding;
            }
        }
    }

    private async Task<float[][]> GetVectorsAsync(IEnumerable<string> values, RunOptions runOptions)
    {
        values = values.Select(s => MyRegex().Replace(s, string.Empty));
        IEnumerable<EmbeddingModelInput> encoded = _tokenizer.EncodeBatch(values);
        var count = values.Count();

        var input = new EmbeddingModelInput
        {
            InputIds = encoded.SelectMany(t => t.InputIds).ToArray(),
            AttentionMask = encoded.SelectMany(t => t.AttentionMask).ToArray(),
            TokenTypeIds = encoded.SelectMany(t => t.TokenTypeIds).ToArray()
        };

        int sequenceLength = input.InputIds.Length / count;

        // Create input tensors over the input data.
        using var inputIdsOrtValue = OrtValue.CreateTensorValueFromMemory(
            input.InputIds,
            [count, sequenceLength]);

        using var attMaskOrtValue = OrtValue.CreateTensorValueFromMemory(
            input.AttentionMask,
            [count, sequenceLength]);

        using var typeIdsOrtValue = OrtValue.CreateTensorValueFromMemory(
            input.TokenTypeIds,
            [count, sequenceLength]);

        var inputNames = new List<string>
        {
            "input_ids",
            "attention_mask",
            "token_type_ids"
        };

        var inputs = new List<OrtValue>
        {
            { inputIdsOrtValue },
            { attMaskOrtValue },
            { typeIdsOrtValue }
        };

        using var output = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float, [count, sequenceLength, 384]);

        try
        {
            await _inferenceSession.RunAsync(runOptions, inputNames, inputs, _inferenceSession.OutputNames, [output]);

            var typeAndShape = output.GetTensorTypeAndShape();

            ReadOnlyTensorSpan<float> sentence_embeddings = MeanPooling(output.GetTensorDataAsSpan<float>(), input.AttentionMask, typeAndShape.Shape);

            float[] resultArray = NormalizeSentenceEmbeddings(sentence_embeddings, typeAndShape.Shape);

            return Enumerable.Chunk(resultArray, resultArray.Length / count).ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static ReadOnlyTensorSpan<float> MeanPooling(ReadOnlySpan<float> embeddings, long[] attentionMask, long[] shape)
    {
        //// Extract shapes
        var batchSize = (int)shape[0];
        var sequenceLength = (int)shape[1];
        var embeddingSize = (int)shape[2];

        // Create a tensor for attention mask
        ReadOnlyTensorSpan<float> attentionMaskTensor = Tensor.ConvertSaturating<long, float>(Tensor.Create(attentionMask, [batchSize, sequenceLength]));

        // Create a tensor for token embeddings
        ReadOnlyTensorSpan<float> tokenEmbeddings = new ReadOnlyTensorSpan<float>(embeddings, [(nint)batchSize, (nint)sequenceLength, (nint)embeddingSize], []);

        // Add a dimension to attention mask [2,11,1]
        ReadOnlyTensorSpan<float> unsqueezed = Tensor.Unsqueeze(attentionMaskTensor, 2);

        // Expand Attention [2,11,384]
        ReadOnlyTensorSpan<float> expandedAttention = Tensor.Broadcast<float>(unsqueezed, tokenEmbeddings.Lengths);

        // Multiply unsqueezed tensor with token embeddings [2,11,384]
        // Implicit broadcasting
        ReadOnlyTensorSpan<float> lhs = Tensor.Multiply(unsqueezed, tokenEmbeddings);

        // Contains intermediate calculator of embedding and attention
        // Tensors summed across the first axis.
        // Results in tensor shapes [2,384]
        TensorSpan<float> numerator = Tensor.Create<float>([batchSize, embeddingSize]);
        TensorSpan<float> denominator = Tensor.Create<float>([batchSize, embeddingSize]);

        // Apply sums along first axis.
        for (var batch = 0; batch < batchSize; batch++)
        {
            TensorSpan<float> sumEmbedding = Tensor.Create<float>([1, embeddingSize]);
            TensorSpan<float> sumAttention = Tensor.Create<float>([1, embeddingSize]);
            for (var sequence = 0; sequence < sequenceLength; sequence++)
            {
                ReadOnlyTensorSpan<float> embeddingSlice =
                    Tensor.Squeeze(lhs.Slice([batch..(batch + 1), sequence..(sequence + 1), 0..embeddingSize]));

                ReadOnlyTensorSpan<float> attentionSlice =
                    Tensor.Squeeze(expandedAttention.Slice([batch..(batch + 1), sequence..(sequence + 1), 0..embeddingSize]));

                sumEmbedding = Tensor.Add<float>(sumEmbedding, embeddingSlice);
                sumAttention = Tensor.Add<float>(sumAttention, attentionSlice);
            }

            Tensor.SetSlice(numerator, sumEmbedding, [batch..(batch + 1), 0..embeddingSize]);
            Tensor.SetSlice(denominator, sumAttention, [batch..(batch + 1), 0..embeddingSize]);
        }

        // Divide numerator by denominator. Mean pooling.
        return Tensor.Divide<float>(numerator, denominator);
    }

    private static float[] NormalizeSentenceEmbeddings(ReadOnlyTensorSpan<float> sentenceEmbeddings, long[] shape)
    {
        int batchSize = (int)shape[0];
        int embeddingSize = (int)shape[2];

        // Create a tensor for the square of the embeddings
        ReadOnlyTensorSpan<float> squaredEmbeddings = Tensor.Multiply<float>(sentenceEmbeddings, sentenceEmbeddings);

        // Create Tensor for sumSquaredEmbeddings
        TensorSpan<float> sumSquaredEmbeddings = Tensor.Create<float>((ReadOnlySpan<nint>)[batchSize, 1]);

        // Sum the squared embeddings across the embedding dimension
        for (var batch = 0; batch < batchSize; batch++)
        {
            // Get the embeddings for the current batch
            ReadOnlyTensorSpan<float> embeddings = squaredEmbeddings.Slice([batch..(batch + 1), 0..embeddingSize]);

            float clampedSumEmbedding = Math.Max(Tensor.Sum<float>(embeddings), 1e-9f);
            sumSquaredEmbeddings[batch, 0] = clampedSumEmbedding;
        }

        // Calculate the square root of the sum of the squared embeddings
        ReadOnlyTensorSpan<float> sqrtSumSquaredEmbeddings = Tensor.Sqrt<float>(sumSquaredEmbeddings);

        // Divide the sentence embeddings by the denominator
        ReadOnlyTensorSpan<float> normalizedEmbeddings = Tensor.Divide<float>(sentenceEmbeddings, sqrtSumSquaredEmbeddings);

        // Return the normalized embeddings
        return [.. normalizedEmbeddings];
    }

    public void Dispose()
    {
        _inferenceSession.Dispose();
        _sessionOptions.Dispose();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return
            serviceKey is not null ? null :
            serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata :
            serviceType?.IsInstanceOfType(_inferenceSession) is true ? _inferenceSession :
            serviceType?.IsInstanceOfType(_tokenizer) is true ? _tokenizer :
            serviceType?.IsInstanceOfType(this) is true ? this :
            null;
    }
}

internal static class EmbeddingGeneratorFactory
{
    public static async Task<EmbeddingGenerator> GetEmbeddingGeneratorInstance(string modelPath, WinMlSampleOptions options)
    {
        ExecutionProviderDevicePolicy? policy = options.Policy;
        string? device = options.Device;
        bool compileModel = options.CompileModel;

        var vocabPath = Path.Join(modelPath, "vocab.txt");
        modelPath = Path.Join(modelPath, "onnx", "model.onnx");

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

            if (compileModel)
            {
                modelPath = sessionOptions.GetCompiledModel(modelPath, device) ?? modelPath;
            }
        }

        return new EmbeddingGenerator(vocabPath, modelPath, sessionOptions);
    }
}