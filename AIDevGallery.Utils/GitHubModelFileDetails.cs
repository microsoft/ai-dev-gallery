// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace AIDevGallery.Utils;

/// <summary>
/// GitHub model file details
/// </summary>
public class GitHubModelFileDetails
{
    /// <summary>
    /// Gets the name of the file
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the relative path to the file
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>
    /// Gets the SHA of the file
    /// </summary>
    [JsonPropertyName("sha")]
    public string? Sha { get; init; }

    /// <summary>
    /// Gets the size of the file
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; init; }

    /// <summary>
    /// Gets the URL to download the file from
    /// </summary>
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Gets the type of the file
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}