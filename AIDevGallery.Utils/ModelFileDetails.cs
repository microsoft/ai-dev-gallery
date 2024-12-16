// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
}