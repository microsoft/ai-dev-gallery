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

namespace AIDevGallery.Utils
{
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

            return files.Select(f =>
                new ModelFileDetails()
                {
                    DownloadUrl = f.DownloadUrl,
                    Size = f.Size,
                    Name = (f.Path ?? string.Empty).Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                    Path = f.Path
                }).ToList();
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

                var httpClient = httpMessageHandler != null ? new HttpClient(httpMessageHandler) : new HttpClient();
                var semaphore = new SemaphoreSlim(4, 4);

                while (hfFiles.Any(f => f.Type == "directory"))
                {
                    var folders = hfFiles.Where(f => f.Type == "directory").ToList();
                    List<Task> tasks = [];
                    foreach (var folder in folders)
                    {
                        hfFiles.Remove(folder);
                        tasks.Add(Task.Run(
                            async () =>
                            {
                                await semaphore.WaitAsync(cancellationToken);
                                var response = await httpClient.GetAsync($"{baseUrl}/{folder.Path}", cancellationToken);
                                var responseContent = await response.Content.ReadAsStringAsync();

                                var files = JsonSerializer.Deserialize(responseContent, SourceGenerationContext.Default.ListHuggingFaceModelFileDetails);
                                if (files != null)
                                {
                                    hfFiles.AddRange(files);
                                }

                                semaphore.Release();
#if NET8_0_OR_GREATER
                            },
                            cancellationToken));
#else
                            }));
#endif
                    }

                    await Task.WhenAll(tasks);
                }

                semaphore.Dispose();
                httpClient.Dispose();
            }

            return hfFiles.Select(f =>
                new ModelFileDetails()
                {
                    DownloadUrl = $"https://huggingface.co/{hfUrl.Organization}/{hfUrl.Repo}/resolve/{hfUrl.Ref}/{f.Path}",
                    Size = f.Size,
                    Name = (f.Path ?? string.Empty).Split(["/"], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                    Path = f.Path
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
}