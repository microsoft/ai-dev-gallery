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
    /// Throws an ArgumentException if the argument is null, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="argument">The string argument to validate.</param>
    /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
    private protected static void ThrowIfNullOrWhiteSpace(string? argument, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException($"{paramName ?? nameof(argument)} cannot be null or whitespace.", paramName ?? nameof(argument));
        }
    }

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
    internal const string BaseUrl = "https://huggingface.co";

    /// <summary>
    /// The base API URL for Hugging Face
    /// </summary>
    public const string ApiUrl = "https://huggingface.co/api";

    /// <inheritdoc/>
    public override string FullUrl
    {
        get { return $"{BaseUrl}/{PartialUrl}"; }
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
            if (!modelNameOrUrl.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase))
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

        if (urlComponents[2].Equals("blob", StringComparison.OrdinalIgnoreCase) ||
            urlComponents[2].Equals("resolve", StringComparison.OrdinalIgnoreCase))
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
    /// Gets the URL root of the repository
    /// </summary>
    /// <returns>The root URL of the HuggingFace repository</returns>
    public string GetUrlRoot()
    {
        return BuildRepoUrl(Organization, Repo);
    }

    /// <summary>
    /// Builds a tree URL for browsing directory structure
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="path">The path within the repository</param>
    /// <returns>The tree URL</returns>
    public static string BuildTreeUrl(string organization, string repo, string @ref = "main", string? path = null)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);

        var url = $"{BaseUrl}/{organization}/{repo}/tree/{@ref}";
        if (!string.IsNullOrEmpty(path))
        {
            url = $"{url}/{path}";
        }

        return url;
    }

    /// <summary>
    /// Builds a resolve URL for downloading files
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="filePath">The file path within the repository</param>
    /// <returns>The resolve URL</returns>
    public static string BuildResolveUrl(string organization, string repo, string @ref, string filePath)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);
        ThrowIfNullOrWhiteSpace(filePath);

        return $"{BaseUrl}/{organization}/{repo}/resolve/{@ref}/{filePath}";
    }

    /// <summary>
    /// Builds a blob URL for viewing file content
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="filePath">The file path within the repository</param>
    /// <returns>The blob URL</returns>
    public static string BuildBlobUrl(string organization, string repo, string @ref, string filePath)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);
        ThrowIfNullOrWhiteSpace(filePath);

        return $"{BaseUrl}/{organization}/{repo}/blob/{@ref}/{filePath}";
    }

    /// <summary>
    /// Builds a base repository URL
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <returns>The base repository URL</returns>
    public static string BuildRepoUrl(string organization, string repo)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);

        return $"{BaseUrl}/{organization}/{repo}";
    }

    /// <summary>
    /// Builds an API URL for retrieving directory structure
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="path">The path within the repository</param>
    /// <returns>The API URL</returns>
    public static string BuildApiUrl(string organization, string repo, string @ref, string? path = null)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);

        var url = $"{ApiUrl}/models/{organization}/{repo}/tree/{@ref}";
        if (!string.IsNullOrEmpty(path))
        {
            url = $"{url}/{path}";
        }

        return url;
    }
}

/// <summary>
/// Represents a URL for a GitHub repository.
/// </summary>
public class GitHubUrl : ModelUrl
{
    private const string BaseUrl = "https://github.com";
    private const string ApiBaseUrl = "https://api.github.com/repos";
    private const string RawBaseUrl = "https://raw.githubusercontent.com";

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

        if (!url.StartsWith($"{BaseUrl}/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid URL", nameof(url));
        }

        url = url[(BaseUrl.Length + 1)..];

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
        return BuildRepoUrl(Organization, Repo);
    }

    /// <summary>
    /// Builds a base repository URL
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <returns>The base repository URL</returns>
    public static string BuildRepoUrl(string organization, string repo)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);

        return $"{BaseUrl}/{organization}/{repo}";
    }

    /// <summary>
    /// Builds an API URL for retrieving repository contents
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="path">The path within the repository</param>
    /// <returns>The GitHub API endpoint URL</returns>
    public static string BuildApiUrl(string organization, string repo, string @ref, string? path = null)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);

        var url = $"{ApiBaseUrl}/{organization}/{repo}/contents";
        if (!string.IsNullOrEmpty(path))
        {
            url = $"{url}/{path}";
        }

        url = $"{url}?ref={@ref}";
        return url;
    }

    /// <summary>
    /// Builds a raw content URL for downloading individual files
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="filePath">The file path within the repository</param>
    /// <returns>The raw.githubusercontent.com URL</returns>
    public static string BuildRawUrl(string organization, string repo, string @ref, string filePath)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);
        ThrowIfNullOrWhiteSpace(filePath);

        return $"{RawBaseUrl}/{organization}/{repo}/{@ref}/{filePath}";
    }

    /// <summary>
    /// Builds a blob URL for viewing file content
    /// </summary>
    /// <param name="organization">The organization name</param>
    /// <param name="repo">The repository name</param>
    /// <param name="ref">The branch or commit reference</param>
    /// <param name="filePath">The file path within the repository</param>
    /// <returns>The blob URL</returns>
    public static string BuildBlobUrl(string organization, string repo, string @ref, string filePath)
    {
        ThrowIfNullOrWhiteSpace(organization);
        ThrowIfNullOrWhiteSpace(repo);
        ThrowIfNullOrWhiteSpace(@ref);
        ThrowIfNullOrWhiteSpace(filePath);

        return $"{BaseUrl}/{organization}/{repo}/blob/{@ref}/{filePath}";
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
        else if (url.StartsWith("https://huggingface.co", StringComparison.OrdinalIgnoreCase))
        {
            return new HuggingFaceUrl(url).FullUrl;
        }
        else if (url.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        else if (url.Contains("://"))
        {
            return url;
        }
        else
        {
            return new HuggingFaceUrl(url).FullUrl;
        }
    }
}