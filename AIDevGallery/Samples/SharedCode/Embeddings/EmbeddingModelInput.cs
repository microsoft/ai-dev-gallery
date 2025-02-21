// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Samples.SharedCode;

internal class EmbeddingModelInput
{
    public required long[] InputIds { get; init; }

    public required long[] AttentionMask { get; init; }

    public required long[] TokenTypeIds { get; init; }
}