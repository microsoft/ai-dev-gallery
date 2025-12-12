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
    /// Gets the expected SHA256 hash of the file.
    /// For Hugging Face: from LFS oid field.
    /// For GitHub: from LFS pointer file.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Gets a value indicating whether this file should be verified for integrity (main model files like .onnx).
    /// </summary>
    public bool ShouldVerifyIntegrity => Name != null &&
        (Name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ||
         Name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
         Name.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a value indicating whether this file has a hash available for verification.
    /// </summary>
    public bool HasVerificationHash => !string.IsNullOrEmpty(Sha256);
}