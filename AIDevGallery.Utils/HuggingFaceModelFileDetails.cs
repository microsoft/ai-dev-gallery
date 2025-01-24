// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace AIDevGallery.Utils;

/// <summary>
/// Details of a file in a Hugging Face model.
/// </summary>
public class HuggingFaceModelFileDetails
{
    /// <summary>
    /// Gets the Type of the file.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the size of the file.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Gets the path of the file.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}