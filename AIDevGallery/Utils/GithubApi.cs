// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class GithubApi
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string RawGhUrl = "https://raw.githubusercontent.com";

    /// <summary>
    /// Gets contents from a file in a Hugging Face repo
    /// </summary>
    /// <param name="fileUrl">url of file</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<string> GetContentsOfTextFile(string fileUrl)
    {
        var url = new GitHubUrl(fileUrl);

        var requestUrl = $"{RawGhUrl}/{url.Organization}/{url.Repo}/{url.Ref}/{url.Path}";

        using var response = await _httpClient.GetAsync(requestUrl);
        return await response.Content.ReadAsStringAsync();
    }
}