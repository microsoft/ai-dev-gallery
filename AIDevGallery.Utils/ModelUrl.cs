// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace AIDevGallery.Utils;

/// <summary>
/// ModelUrl class
/// </summary>
public abstract class ModelUrl
{
    /// <summary>
    /// Gets the FullUrl property
    /// </summary>
    public abstract string FullUrl { get; private protected init; }
    internal string? PartialUrl { get; private protected init; }

    /// <summary>
    /// Gets the Repo property
    /// </summary>
    public abstract string Repo { get; private protected init; }

    /// <summary>
    /// Gets the Organization property
    /// </summary>
    public abstract string Organization { get; private protected init; }

    /// <summary>
    /// Gets the file path if exists
    /// </summary>
    public string? Path { get; private protected init; }

    /// <summary>
    /// Gets the Ref property
    /// </summary>
    public string Ref { get; private protected init; } = "main";

    /// <summary>
    /// Gets a value indicating whether the url references a file
    /// </summary>
    public abstract bool IsFile { get; private protected init; }

    /// <summary>
    /// Gets the local path with the given cache folder
    /// </summary>
    /// <param name="cacheRoot">The root directory of the cache</param>
    /// <returns>The local path constructed from the cache root, organization, repository, and reference</returns>
    public string GetLocalPath(string cacheRoot)
    {
        var localFolderPath = $"{cacheRoot}\\{Organization}--{Repo}\\{Ref}";
        var path = Path;
        if (!string.IsNullOrEmpty(path))
        {
            if (IsFile)
            {
                var pathComponents = path!.Split('/');
                pathComponents = pathComponents.Take(pathComponents.Length - 1).ToArray();

                localFolderPath = $"{localFolderPath}\\{string.Join("\\", pathComponents)}";
            }
            else
            {
                localFolderPath = $"{localFolderPath}\\{path!.Replace("/", "\\")}";
            }
        }

        return localFolderPath;
    }
}

/// <summary>
/// HuggingFaceUrl class
/// </summary>
public class HuggingFaceUrl : ModelUrl
{
    /// <inheritdoc/>
    public override string FullUrl
    {
        get { return $"https://huggingface.co/{PartialUrl}"; }
        private protected init { }
    }

    /// <inheritdoc/>
    public override string Repo { get; private protected init; }

    /// <inheritdoc/>
    public override string Organization { get; private protected init; }

    /// <inheritdoc/>
    public override bool IsFile { get; private protected init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceUrl"/> class.
    /// </summary>
    /// <param name="modelNameOrUrl">The model name or URL to initialize the HuggingFaceUrl instance.</param>
    public HuggingFaceUrl(string modelNameOrUrl)
    {
        if (string.IsNullOrEmpty(modelNameOrUrl))
        {
            throw new ArgumentException("Model name or URL cannot be null or empty", nameof(modelNameOrUrl));
        }

        modelNameOrUrl = modelNameOrUrl.Trim();

        if (modelNameOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!modelNameOrUrl.StartsWith("https://huggingface.co", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid URL", nameof(modelNameOrUrl));
            }

            modelNameOrUrl = modelNameOrUrl[23..];
        }

        string[] urlComponents = modelNameOrUrl.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        if (urlComponents.Length < 2)
        {
            throw new ArgumentException("Invalid URL", nameof(modelNameOrUrl));
        }

        Organization = urlComponents[0];
        Repo = urlComponents[1];

        if (urlComponents.Length < 4)
        {
            PartialUrl = $"{Organization}/{Repo}/tree/{Ref}";
            return;
        }

        Ref = urlComponents[3];

        if (urlComponents[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
        {
            IsFile = true;
        }

        Path = string.Join("/", urlComponents.Skip(4));
        PartialUrl = $"{Organization}/{Repo}/{urlComponents[2]}/{Ref}";
        if (!string.IsNullOrEmpty(Path))
        {
            PartialUrl = $"{PartialUrl}/{Path}";
        }
    }

    /// <summary>
    /// Gets the URL root
    /// </summary>
    /// <returns>The root URL of the HuggingFace repository</returns>
    public string GetUrlRoot()
    {
        return $"https://huggingface.co/{Organization}/{Repo}";
    }
}

/// <summary>
/// Represents a URL for a GitHub repository.
/// </summary>
public class GitHubUrl : ModelUrl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubUrl"/> class.
    /// </summary>
    /// <param name="url">The GitHub repository URL.</param>
    public GitHubUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("url cannot be null or empty", nameof(url));
        }

        url = url.Trim();
        FullUrl = url;

        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid URL", nameof(url));
        }

        url = url[19..];

        string[] urlComponents = url.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        if (urlComponents.Length < 2)
        {
            throw new ArgumentException("Invalid URL", nameof(url));
        }

        Organization = urlComponents[0];
        Repo = urlComponents[1];

        if (urlComponents.Length < 4)
        {
            return;
        }

        Ref = urlComponents[3];

        if (urlComponents[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
        {
            IsFile = true;
        }

        Path = string.Join("/", urlComponents.Skip(4));
    }

    /// <summary>
    /// Gets the full URL of the GitHub repository.
    /// </summary>
    public override string FullUrl { get; private protected init; }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public override string Repo { get; private protected init; }

    /// <summary>
    /// Gets the organization name.
    /// </summary>
    public override string Organization { get; private protected init; }

    /// <summary>
    /// Gets a value indicating whether the URL references a file.
    /// </summary>
    public override bool IsFile { get; private protected init; }

    /// <summary>
    /// Gets the URL root of the GitHub repository.
    /// </summary>
    /// <returns>The root URL of the GitHub repository.</returns>
    public string GetUrlRoot()
    {
        return $"https://github.com/{Organization}/{Repo}";
    }
}

/// <summary>
/// Provides helper methods for URL manipulation.
/// </summary>
public static class UrlHelpers
{
    /// <summary>
    /// Gets the full URL for a given partial URL.
    /// </summary>
    /// <param name="url">The partial URL to be converted to a full URL.</param>
    /// <returns>The full URL as a string.</returns>
    public static string GetFullUrl(string url)
    {
        if (url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        else if (url.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        else
        {
            return new HuggingFaceUrl(url).FullUrl;
        }
    }
}