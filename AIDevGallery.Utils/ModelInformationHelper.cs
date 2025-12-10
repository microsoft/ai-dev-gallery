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
        string getModelDetailsUrl = $"https://api.github.com/repos/{url.Organization}/{url.Repo}/contents/{url.Path}?ref={url.Ref}";

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

        var result = new List<ModelFileDetails>();

        foreach (var f in files)
        {
            string? sha256 = null;

            // Check if this is a Git LFS file by checking if download_url points to media.githubusercontent.com
            // LFS files have their actual content stored separately and we need to get SHA256 from the LFS pointer
#if NET8_0_OR_GREATER
            if (f.DownloadUrl != null && f.DownloadUrl.Contains("media.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
#else
            if (f.DownloadUrl != null && f.DownloadUrl.IndexOf("media.githubusercontent.com", StringComparison.OrdinalIgnoreCase) >= 0)
#endif
            {
                // This is an LFS file - fetch the LFS pointer to get SHA256
                sha256 = await GetLfsFileSha256Async(client, url, f.Path, cancellationToken);
            }

            result.Add(new ModelFileDetails()
            {
                DownloadUrl = f.DownloadUrl,
                Size = f.Size,
                Name = (f.Path ?? string.Empty).Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                Path = f.Path,
                Sha256 = sha256
            });
        }

        return result;
    }

    /// <summary>
    /// Retrieves the SHA256 hash from a Git LFS pointer file.
    /// </summary>
    /// <param name="client">The HTTP client to use.</param>
    /// <param name="url">The GitHub URL.</param>
    /// <param name="filePath">The path to the file in the repository.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The SHA256 hash if found, otherwise null.</returns>
    private static async Task<string?> GetLfsFileSha256Async(HttpClient client, GitHubUrl url, string? filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        try
        {
            // Fetch the raw LFS pointer file from raw.githubusercontent.com
            var lfsPointerUrl = $"https://raw.githubusercontent.com/{url.Organization}/{url.Repo}/{url.Ref}/{filePath}";
#if NET8_0_OR_GREATER
            var lfsPointerContent = await client.GetStringAsync(lfsPointerUrl, cancellationToken);
#else
            var response = await client.GetAsync(lfsPointerUrl, cancellationToken);
            var lfsPointerContent = await response.Content.ReadAsStringAsync();
#endif

            // Parse the LFS pointer format:
            // version https://git-lfs.github.com/spec/v1
            return ParseLfsPointerSha256(lfsPointerContent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get LFS pointer for {filePath}: {ex.Message}");
            return null;
        }
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
        string getModelDetailsUrl;

        if (hfUrl.IsFile)
        {
            getModelDetailsUrl = $"https://huggingface.co/api/models/{hfUrl.Organization}/{hfUrl.Repo}/tree/{hfUrl.Ref}";
            if (hfUrl.Path != null)
            {
                var filePath = hfUrl.Path.Split('/');
                filePath = filePath.Take(filePath.Length - 1).ToArray();

                if (filePath.Length > 0)
                {
                    getModelDetailsUrl = $"{getModelDetailsUrl}/{string.Join("/", filePath)}";
                }
            }
        }
        else
        {
            getModelDetailsUrl = $"https://huggingface.co/api/models/{hfUrl.PartialUrl}";
        }

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
            var baseUrl = $"https://huggingface.co/api/models/{hfUrl.Organization}/{hfUrl.Repo}/tree/{hfUrl.Ref}";

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

        return hfFiles.Where(f => f.Type != "directory").Select(f =>
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

            return new ModelFileDetails()
            {
                DownloadUrl = $"https://huggingface.co/{hfUrl.Organization}/{hfUrl.Repo}/resolve/{hfUrl.Ref}/{f.Path}",
                Size = f.Lfs?.Size ?? f.Size,
                Name = (f.Path ?? string.Empty).Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
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