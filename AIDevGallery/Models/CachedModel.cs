// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using System;
using System.Text.Json.Serialization;

namespace AIDevGallery.Models;

internal class CachedModel
{
    public ModelDetails Details { get; }
    public string Url { get; }
    public DateTime DateTimeCached { get; }
    public string Path { get; }
    public bool IsFile { get; }
    public long ModelSize { get; }
    public CachedModelSource Source { get; }

    public CachedModel(ModelDetails details, string path, bool isFile, long modelSize)
    {
        Details = details;
        if (details.Url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
        {
            Url = details.Url;
            Source = CachedModelSource.GitHub;
        }
        else if (details.Url.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            Url = details.Url;
            Source = CachedModelSource.Local;
        }
        else
        {
            Url = new HuggingFaceUrl(details.Url).FullUrl;
            Source = CachedModelSource.HuggingFace;
        }

        DateTimeCached = DateTime.Now;
        Path = path;
        IsFile = isFile;
        ModelSize = modelSize;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<CachedModelSource>))]
internal enum CachedModelSource
{
    GitHub,
    HuggingFace,
    Local
}