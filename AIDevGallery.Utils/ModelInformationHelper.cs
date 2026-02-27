// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AIDevGallery.Utils;

/// <summary>
/// Provides helper methods to retrieve model file details from GitHub and Hugging Face.
/// </summary>
public static class ModelInformationHelper
{
    /// <summary>
    /// Retrieves a list of model file details from a specified GitHub repository.
    /// </summary>
    /// <param name="url">The GitHub URL containing the organization, repository, path, and reference.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model file details.</returns>
    public static async Task<List<ModelFileDetails>> GetDownloadFilesFromGitHub(GitHubUrl url, CancellationToken cancellationToken)
    {
        string getModelDetailsUrl = GitHubUrl.BuildApiUrl(url.Organization, url.Repo, url.Ref, url.Path);

        // call api and get json
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "AIDevGallery");
        var response = await client.GetAsync(getModelDetailsUrl, cancellationToken);
#if NET8_0_OR_GREATER
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var responseContent = await response.Content.ReadAsStringAsync();
#endif

        // make it a list if it isn't already
        responseContent = responseContent.Trim();
#if NET8_0_OR_GREATER
        if (!responseContent.StartsWith('['))
#else
        if (!responseContent.StartsWith("["))
#endif
        {
            responseContent = $"[{responseContent}]";
        }

        var files = JsonSerializer.Deserialize(responseContent, SourceGenerationContext.Default.ListGitHubModelFileDetails);

        if (files == null)
        {
            Debug.WriteLine("Failed to get model details from GitHub");
            return [];
        }

        return files.Select(f =>
        {
            string? sha256 = null;

            if (f.Content != null && f.Encoding == "base64")
            {
                try
                {
                    var decodedContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f.Content));
                    sha256 = ParseLfsPointerSha256(decodedContent);
                }
                catch (FormatException)
                {
                    Debug.WriteLine($"Failed to decode base64 content for {f.Path}");
                }
            }

            return new ModelFileDetails()
            {
                DownloadUrl = f.DownloadUrl,
                Size = f.Size,
                Name = (f.Path ?? string.Empty).Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                Path = f.Path,
                Sha256 = sha256
            };
        }).ToList();
    }

    /// <summary>
    /// Parses a Git LFS pointer file content to extract the SHA256 hash.
    /// </summary>
    /// <param name="lfsPointerContent">The content of the LFS pointer file.</param>
    /// <returns>The SHA256 hash if found, otherwise null.</returns>
    private static string? ParseLfsPointerSha256(string lfsPointerContent)
    {
        if (string.IsNullOrEmpty(lfsPointerContent))
        {
            return null;
        }

        var lines = lfsPointerContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("oid sha256:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring("oid sha256:".Length).Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves a list of model file details from a specified Hugging Face repository.
    /// </summary>
    /// <param name="hfUrl">The Hugging Face URL containing the organization, repository, path, and reference.</param>
    /// <param name="httpMessageHandler">The HTTP message handler used to configure the HTTP client.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model file details.</returns>
    public static async Task<List<ModelFileDetails>> GetDownloadFilesFromHuggingFace(HuggingFaceUrl hfUrl, HttpMessageHandler? httpMessageHandler = null, CancellationToken cancellationToken = default)
    {
        string? path = hfUrl.Path;
        if (hfUrl.IsFile && hfUrl.Path != null)
        {
            // For files, get the parent directory path
            var filePath = hfUrl.Path.Split('/');
            path = filePath.Length > 1
                ? string.Join("/", filePath.Take(filePath.Length - 1))
                : null;
        }

        string getModelDetailsUrl = HuggingFaceUrl.BuildApiUrl(hfUrl.Organization, hfUrl.Repo, hfUrl.Ref, path);

        // call api and get json
        using var client = new HttpClient();
        var response = await client.GetAsync(getModelDetailsUrl, cancellationToken);
#if NET8_0_OR_GREATER
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
#else
        var responseContent = await response.Content.ReadAsStringAsync();
#endif

        var hfFiles = JsonSerializer.Deserialize(responseContent, SourceGenerationContext.Default.ListHuggingFaceModelFileDetails);

        if (hfFiles == null)
        {
            Debug.WriteLine("Failed to get model details from Hugging Face");
            return [];
        }

        if (hfUrl.IsFile)
        {
            hfFiles = hfFiles.Where(f => f.Path == hfUrl.Path).ToList();
        }

        if (hfFiles.Any(f => f.Type == "directory"))
        {
            // Build base API URL for directory traversal (without path, will append paths in loop)
            var baseUrl = HuggingFaceUrl.BuildApiUrl(hfUrl.Organization, hfUrl.Repo, hfUrl.Ref);

            using var httpClient = httpMessageHandler != null ? new HttpClient(httpMessageHandler) : new HttpClient();

            ActionBlock<string> actionBlock = null!;
            actionBlock = new ActionBlock<string>(
                async (string path) =>
                {
                    var response = await httpClient.GetAsync($"{baseUrl}/{path}", cancellationToken);
#if NET8_0_OR_GREATER
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
                    var stream = await response.Content.ReadAsStreamAsync();
#endif
                    var files = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.ListHuggingFaceModelFileDetails, cancellationToken);
                    if (files != null)
                    {
                        lock (hfFiles)
                        {
                            foreach (var file in files.Where(f => f.Type != "directory"))
                            {
                                hfFiles.Add(file);
                            }
                        }

                        foreach (var folder in files.Where(f => f.Type == "directory" && f.Path != null))
                        {
                            actionBlock.Post(folder.Path!);
                        }
                    }

                    if (actionBlock.InputCount == 0)
                    {
                        actionBlock.Complete();
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken
                });

            foreach (var folder in hfFiles.Where(f => f.Type == "directory" && f.Path != null))
            {
                actionBlock.Post(folder.Path!);
            }

            await actionBlock.Completion;
        }

        return hfFiles.Where(f => f.Type != "directory" && !string.IsNullOrEmpty(f.Path)).Select(f =>
        {
            string? sha256 = null;
            if (f.Lfs?.Oid != null)
            {
                sha256 = f.Lfs.Oid;
                if (sha256.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    sha256 = sha256.Substring(7);
                }
            }

            // f.Path is guaranteed to be non-null due to the Where filter above
            return new ModelFileDetails()
            {
                DownloadUrl = HuggingFaceUrl.BuildResolveUrl(hfUrl.Organization, hfUrl.Repo, hfUrl.Ref, f.Path!),
                Size = f.Size,
                Name = f.Path!.Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                Path = f.Path,
                Sha256 = sha256
            };
        }).ToList();
    }

    /// <summary>
    /// Filters the list of files to download based on the specified file filters.
    /// </summary>
    /// <param name="filesToDownload">The list of files to download.</param>
    /// <param name="fileFilters">The list of file filters (wildcards) to apply.</param>
    /// <returns>The filtered list of files to download.</returns>
    public static List<ModelFileDetails> FilterFiles(List<ModelFileDetails> filesToDownload, List<string>? fileFilters)
    {
        if (fileFilters == null || fileFilters.Count == 0)
        {
            return filesToDownload;
        }

        return filesToDownload
            .Where(f => fileFilters.Any(filter =>
                f.Path != null &&
                f.Path.EndsWith(filter, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }
}