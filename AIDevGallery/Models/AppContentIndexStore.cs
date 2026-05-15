// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace AIDevGallery.Models;

internal class AppContentIndexStore
{
    public string IndexName { get; }
    public string Path { get; }
    public long IndexSize { get; }

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

    public AppContentIndexStore(string indexName, string path, long size)
    {
        IndexName = indexName;
        Path = path;
        IndexSize = size;
    }
}