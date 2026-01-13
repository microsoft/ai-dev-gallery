// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

/// <summary>
/// Provides helper methods to use the Hugging Face API.
/// </summary>
internal class HuggingFaceApi
{
    /// <summary>
    /// Searched models on Hugging Face
    /// </summary>
    /// <param name="query">The search term</param>
    /// <param name="filter">The filter term</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<List<HFSearchResult>?> FindModels(string query, string filter = "onnx")
    {
        string searchUrl = $"{HuggingFaceUrl.ApiUrl}/models?search={query}&filter={filter}&full=true&config=true";
        using var client = new HttpClient();
        var response = await client.GetAsync(searchUrl);
        var responseContent = await response.Content.ReadAsStringAsync();

        try
        {
            return JsonSerializer.Deserialize(responseContent, SourceGenerationContext.Default.ListHFSearchResult);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Get<ITelemetry>().LogException("HuggingFaceApiSearchFailed_Event", ex);
            return [];
        }
    }

    /// <summary>
    /// Gets contents from a file in a Hugging Face repo
    /// </summary>
    /// <param name="modelId">the id of the model</param>
    /// <param name="filePath">the path of the file</param>
    /// <param name="commitOrBranch">the name or commit</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<string> GetContentsOfTextFile(string modelId, string filePath, string commitOrBranch = "main")
    {
        var parts = modelId.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException("modelId must be in format 'organization/repo'", nameof(modelId));
        }

        var fullUrl = HuggingFaceUrl.BuildResolveUrl(parts[0], parts[1], commitOrBranch, filePath);
        return await GetContentsOfTextFile(fullUrl);
    }

    /// <summary>
    /// Gets contens from a file in a Hugging Face repo
    /// </summary>
    /// <param name="fileUrl">url of file</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public static async Task<string> GetContentsOfTextFile(string fileUrl)
    {
        var url = new HuggingFaceUrl(fileUrl);

        if (string.IsNullOrEmpty(url.Path))
        {
            throw new ArgumentException("File URL must include a file path", nameof(fileUrl));
        }

        var requestUrl = HuggingFaceUrl.BuildResolveUrl(url.Organization, url.Repo, url.Ref, url.Path);

        using var client = new HttpClient();
        var response = await client.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch file from HuggingFace: {response.StatusCode} - {requestUrl}");
        }

        return await response.Content.ReadAsStringAsync();
    }
}