// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.VectorData;
using System;

namespace AIDevGallery.Samples.SharedCode;

internal class StringData
{
    internal static readonly VectorStoreCollectionDefinition VectorStoreDefinition = new()
    {
        Properties =
        [
            new VectorStoreKeyProperty("Key", typeof(int)),
            new VectorStoreDataProperty("Text", typeof(string)),
            new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 384)
        ]
    };
}