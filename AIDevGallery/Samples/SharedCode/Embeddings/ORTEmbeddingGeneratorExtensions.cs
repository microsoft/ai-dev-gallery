// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AIDevGallery.Samples.SharedCode;

internal static class ORTEmbeddingGeneratorExtensions
{
    public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        object? chunkSizeObj = generator.GetService(typeof(int), "ChunkSize");
        int chunkSize = chunkSizeObj != null ? (int)chunkSizeObj : values.Count();

        var chunks = values.Chunk(chunkSize);

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GeneratedEmbeddings<Embedding<float>> embeddings = await generator.GenerateAsync(chunk, options, cancellationToken).ConfigureAwait(false);

            foreach (var embedding in embeddings)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return embedding;
            }
        }
    }
}