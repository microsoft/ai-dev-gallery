// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace AIDevGallery.Utils;

/// <summary>
/// Model file details
/// </summary>
public class ModelFileDetails
{
    /// <summary>
    /// Gets the URL to download the model from
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Gets the size of the file
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets the name of the file
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the relative path to the file
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the expected SHA256 hash of the file (from Hugging Face LFS).
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Gets the expected Git blob SHA-1 hash of the file (from GitHub API).
    /// This is NOT the same as SHA1 of the file content - it includes a "blob {size}\0" prefix.
    /// </summary>
    public string? GitBlobSha1 { get; init; }

    /// <summary>
    /// Gets a value indicating whether this file should be verified for integrity (main model files like .onnx).
    /// </summary>
    public bool ShouldVerifyIntegrity => Name != null &&
        (Name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ||
         Name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
         Name.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a value indicating whether this file has any hash available for verification.
    /// </summary>
    public bool HasVerificationHash => !string.IsNullOrEmpty(Sha256) || !string.IsNullOrEmpty(GitBlobSha1);
}