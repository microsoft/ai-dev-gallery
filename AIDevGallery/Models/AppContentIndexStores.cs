// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace AIDevGallery.Models;

internal class AppContentIndexStores
{
    public string IndexName { get; }
    public string Path { get; }
    public long ModelSize { get; }

    public string ShortPath
    {
        get
        {
            if (Path.Length <= 100)
            {
                return Path;
            }

            return string.Concat("...", Path.AsSpan(Path.Length - 100));
        }
    }

    public AppContentIndexStores(string indexName, string path, long size)
    {
        IndexName = indexName;
        Path = path;
        ModelSize = size;
    }
}