// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class FoundryClient
{
    private static readonly Regex _tupleRegex = new Regex(@"\(\s*""[^""]*""\s*,\s*([0-9]*\.?[0-9]+)\s*\)", RegexOptions.Compiled);

    public static async Task<FoundryClient?> CreateAsync(HttpClient? httpClient = null)
    {
        var serviceManager = FoundryServiceManager.TryCreate();
        if (serviceManager == null)
        {
            return null;
        }

        if (!await serviceManager.IsRunning())
        {
            if (!await serviceManager.StartService())
            {
                return null;
            }
        }

        var serviceUrl = await serviceManager.GetServiceUrl();

        if (string.IsNullOrEmpty(serviceUrl))
        {
            return null;
        }

        return new FoundryClient(serviceUrl, serviceManager, httpClient ?? new HttpClient());
    }

    public FoundryServiceManager ServiceManager { get; init; }

    private HttpClient _httpClient;
    private string _baseUrl;
    private List<FoundryCatalogModel> _catalogModels = [];

    private record DownloadResult(bool Success, string? ErrorMessage);

    private FoundryClient(string baseUrl, FoundryServiceManager serviceManager, HttpClient httpClient)
    {
        this.ServiceManager = serviceManager;
        this._baseUrl = baseUrl;
        this._httpClient = httpClient;
    }

    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalogModels.Count > 0)
        {
            return _catalogModels;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/foundry/list");
            response.EnsureSuccessStatusCode();

            var models = await JsonSerializer.DeserializeAsync(
                response.Content.ReadAsStream(),
                FoundryJsonContext.Default.ListFoundryCatalogModel);

            if (models != null && models.Count > 0)
            {
                models.ForEach(_catalogModels.Add);
            }
        }
        catch (Exception e)
        {
        }

        return _catalogModels;
    }

    //public async Task<string> GetCacheLocation()
    //{
    //    var response = await _httpClient.GetAsync($"{_baseUrl}/openai/status");
    //    response.EnsureSuccessStatusCode();

    //    return await response.Content.ReadAsStringAsync();
    //}

    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        // TODO: no way to match returned ids with catalog models yet
        // fallback to calling cli
        var response = await _httpClient.GetAsync($"{_baseUrl}/openai/models");
        response.EnsureSuccessStatusCode();

        var catalogModels = await ListCatalogModels();

        var content = await response.Content.ReadAsStringAsync();
        var modelIds = content.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries).Select(id => id.Trim('"'));

        List<FoundryCachedModel> models = [];

        foreach (var id in modelIds)
        {
            var model = catalogModels.FirstOrDefault(m => m.Name == id);
            if (model != null)
            {
                models.Add(new FoundryCachedModel(id, model.Alias));
            }
            else
            {
                models.Add(new FoundryCachedModel(id, null));
            }
        }

        return models;
    }

    public async Task<bool> DownloadModel(string modelName, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        var models = await ListCachedModels();

        if (models.Any(m => m.Name == modelName))
        {
            return true;
        }

        var body = JsonSerializer.Serialize(new
        {
            model = new
            {
                Name = modelName
            }
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/openai/download")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            _httpClient.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? finalJson = null;
            var line = await reader.ReadLineAsync(cancellationToken);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    continue;
                }

                line = line.Trim();

                // Final response starts with '{'
                if (line.StartsWith('{'))
                {
                    finalJson = line;
                    break;
                }

                // Progress tuple: ("file", 0.42)
                var m = _tupleRegex.Match(line);
                if (m.Success &&
                    float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                {
                    progress?.Report(pct);          // pct is 0‑1
                }
            }

            // Parse closing JSON; default if malformed
            var result = finalJson is not null
                   ? JsonSerializer.Deserialize<DownloadResult>(finalJson,
                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
                   : new DownloadResult(false, "Missing final result from server.");

            return result.Success;
        }
        catch (Exception e)
        {
            //var errorMessage = await response.Content.ReadAsStringAsync(cancellationToken)
            //                                  .ConfigureAwait(false);
            //throw new Exception($"Error downloading model: {errorMessage}", e);
            return false;
        }
    }
}
