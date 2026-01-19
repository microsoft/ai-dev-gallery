// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class GithubApi
{
    /// <summary>
    /// Gets contents from a file in a GitHub repo
    /// </summary>
    /// <param name="fileUrl">url of file</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<string> GetContentsOfTextFile(string fileUrl)
    {
        var url = new GitHubUrl(fileUrl);

        if (string.IsNullOrEmpty(url.Path))
        {
            throw new ArgumentException("File URL must include a file path", nameof(fileUrl));
        }

        var requestUrl = GitHubUrl.BuildRawUrl(url.Organization, url.Repo, url.Ref, url.Path);
        using var client = new HttpClient();
        var response = await client.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch file from GitHub: {response.StatusCode} - {requestUrl}");
        }

        return await response.Content.ReadAsStringAsync();
    }
}