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

    /// <summary>
    /// Gets the LFS (Large File Storage) information for the file.
    /// </summary>
    [JsonPropertyName("lfs")]
    public HuggingFaceLfsInfo? Lfs { get; init; }
}

/// <summary>
/// LFS (Large File Storage) information for a Hugging Face file.
/// </summary>
public class HuggingFaceLfsInfo
{
    /// <summary>
    /// Gets the OID (SHA256 hash) of the file. Format: "sha256:abc123..."
    /// </summary>
    [JsonPropertyName("oid")]
    public string? Oid { get; init; }

    /// <summary>
    /// Gets the size of the file in LFS.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }
}