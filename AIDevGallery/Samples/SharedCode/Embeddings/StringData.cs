// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.VectorData;
using System;

namespace AIDevGallery.Samples.SharedCode;

internal class StringData
{
    [VectorStoreRecordKey]
    public required int Key { get; init; }

    [VectorStoreRecordData]
    public required string Text { get; init; }

    [VectorStoreRecordVector(384, DistanceFunction.CosineSimilarity)]
    public required ReadOnlyMemory<float> Vector { get; init; }
}