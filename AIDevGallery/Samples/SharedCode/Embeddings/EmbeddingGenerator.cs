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

    public EmbeddingGenerator(string modelPath, HardwareAccelerator hardwareAccelerator)
    {
        _metadata = new EmbeddingGeneratorMetadata("ORTEmbeddingGenerator", new Uri($"file://{modelPath}"), modelPath, 384);

        _sessionOptions = new SessionOptions();

        if (hardwareAccelerator == HardwareAccelerator.DML)
        {
            _sessionOptions.AppendExecutionProvider_DML(DeviceUtils.GetBestDeviceId());
        }
        else if (hardwareAccelerator == HardwareAccelerator.QNN)
        {
            Dictionary<string, string> options = new()
            {
                { "backend_path", "QnnHtp.dll" },
                { "htp_performance_mode", "high_performance" },
                { "htp_graph_finalization_optimization_mode", "3" }
            };
            _sessionOptions.AppendExecutionProvider("QNN", options);
            _chunkSize = 8;
        }

        _inferenceSession = new InferenceSession(Path.Join(modelPath, "onnx", "model.onnx"), _sessionOptions);
        _tokenizer = BertTokenizer.Create(Path.Join(modelPath, "vocab.txt"));
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

                    var vectors = await GetVectorsAsync(values, runOptions).ConfigureAwait(false);

                    for (var i = 0; i < values.Count(); i++)
                    {
                        generatedEmbeddings.Add(new Embedding<float>(vectors[i])
                        {
                            AdditionalProperties = new AdditionalPropertiesDictionary
                            {
                                ["Text"] = values.ElementAt(i)
                            }
                        });
                    }

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

        var encoded = _tokenizer.EncodeBatch(values);

        var count = values.Count();

        var input = new EmbeddingModelInput
        {
            InputIds = encoded.SelectMany(t => t.InputIds.Select(x => x)).ToArray(),
            AttentionMask = encoded.SelectMany(t => t.AttentionMask).ToArray(),
            TokenTypeIds = encoded.SelectMany(t => t.TokenTypeIds).ToArray()
        };

        // round up
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

            var sentence_embeddings = MeanPooling(output.GetTensorDataAsSpan<float>(), input.AttentionMask, typeAndShape.Shape);

            var resultArray = NormalizeAndDivide(sentence_embeddings, typeAndShape.Shape);

            return Enumerable.Chunk(resultArray, resultArray.Length / count).ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static float[] MeanPooling(ReadOnlySpan<float> embeddings, long[] attentionMask, long[] shape)
    {
        //// Extract shapes
        var batchSize = (int)shape[0];
        var sequenceLength = (int)shape[1];
        var embeddingSize = (int)shape[2];

        // Create a tensor for attention mask
        var attentionMaskTensor = Tensor.ConvertSaturating<long, float>(Tensor.Create(attentionMask, [batchSize, sequenceLength]));

        // Create a tensor for token embeddings
        var tokenEmbeddings = new ReadOnlyTensorSpan<float>(embeddings, [(nint)batchSize, (nint)sequenceLength, (nint)embeddingSize], []);

        // Add a dimension to attention mask [2,11,1]
        var unsqueezed = Tensor.Unsqueeze(attentionMaskTensor, 2);

        // Expand Attention [2,11,384]
        var expandedAttention = Tensor.Broadcast<float>(unsqueezed, tokenEmbeddings.Lengths);

        // Multiply unsqueezed tensor with token embeddings [2,11,384]
        // Implicit broadcasting
        var lhs = Tensor.Multiply(unsqueezed, tokenEmbeddings);

        // Contains intermediate calculator of embedding and attention
        // Tensors summed across the first axis.
        // Results in tensor shapes [2,384]
        var numerator = Tensor.Create<float>([batchSize, embeddingSize]);
        var denominator = Tensor.Create<float>([batchSize, embeddingSize]);

        // Apply sums along first axis.
        for (var batch = 0; batch < batchSize; batch++)
        {
            var sumEmbedding = Tensor.Create<float>([1, embeddingSize]);
            var sumAttention = Tensor.Create<float>([1, embeddingSize]);
            for (var sequence = 0; sequence < sequenceLength; sequence++)
            {
                var embeddingSlice =
                    Tensor.Squeeze(lhs.Slice([batch..(batch + 1), sequence..(sequence + 1), 0..embeddingSize]));

                var attentionSlice =
                    Tensor.Squeeze(expandedAttention.Slice([batch..(batch + 1), sequence..(sequence + 1), 0..embeddingSize]));

                sumEmbedding = Tensor.Add<float>(sumEmbedding, embeddingSlice);
                sumAttention = Tensor.Add<float>(sumAttention, attentionSlice);
            }

            Tensor.SetSlice(numerator, sumEmbedding, [batch..(batch + 1), 0..embeddingSize]);
            Tensor.SetSlice(denominator, sumAttention, [batch..(batch + 1), 0..embeddingSize]);
        }

        // Divide numerator by denominator. Mean pooling.
        var result = Tensor.Divide<float>(numerator, denominator);

        // Return result
        return [.. result];
    }

    private static float[] NormalizeAndDivide(float[] sentenceEmbeddings, long[] shape)
    {
        long batchSize = shape[0];
        int embeddingSize = (int)shape[2];

        // Create a tensor for the square of the embeddings
        var squaredEmbeddings = Tensor.Multiply<float>(sentenceEmbeddings, sentenceEmbeddings);

        // Create Tensor for sumSquaredEmbeddings
        var sumSquaredEmbeddings = Tensor.Create<float>([(nint)batchSize, 1]);

        // Sum the squared embeddings across the embedding dimension
        for (var batch = 0; batch < batchSize; batch++)
        {
            // Get the embeddings for the current batch
            var embeddings = squaredEmbeddings.Slice([0..embeddingSize]);

            // Sum the embeddings across the embedding dimension
            var clampedSumEmbedding = Math.Max(Tensor.Sum<float>(embeddings), 1e-9f);
            var sumEmbeddings = Tensor.Create([clampedSumEmbedding], [1, 1]);

            // Set the sum of the squared embeddings for the current batch
            sumSquaredEmbeddings[(ReadOnlySpan<nint>)[batch, 0]] = sumEmbeddings[(ReadOnlySpan<nint>)[0, 0]];
        }

        // Calculate the square root of the sum of the squared embeddings
        var sqrtSumSquaredEmbeddings = Tensor.Sqrt<float>(sumSquaredEmbeddings);

        // Divide the sentence embeddings by the denominator
        var normalizedEmbeddings = Tensor.Divide(sentenceEmbeddings, sqrtSumSquaredEmbeddings.First()); // Temporary fix

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