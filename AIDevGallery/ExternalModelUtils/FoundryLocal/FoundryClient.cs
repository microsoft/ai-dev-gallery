// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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
        catch
        {
        }

        return _catalogModels;
    }

    // public async Task<string> GetCacheLocation()
    // {
    //    var response = await _httpClient.GetAsync($"{_baseUrl}/openai/status");
    //    response.EnsureSuccessStatusCode();
    //    return await response.Content.ReadAsStringAsync();
    // }
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

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel model, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        var models = await ListCachedModels();

        if (models.Any(m => m.Name == model.Name))
        {
            return new(true, "Model already downloaded");
        }

        return await Task.Run(async () =>
        {
            try
            {
                var uploadBody = new FoundryDownloadBody(
                    new FoundryModelDownload(
                        Name: model.Name,
                        Uri: model.Uri,
                        Path: await GetModelPath(model.Uri), // temporary
                        ProviderType: model.ProviderType,
                        PromptTemplate: model.PromptTemplate),
                    IgnorePipeReport: true);

                string body = JsonSerializer.Serialize(
                     uploadBody,
                     FoundryJsonContext.Default.FoundryDownloadBody);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/openai/download")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? finalJson = null;
                var line = await reader.ReadLineAsync(cancellationToken);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    line = await reader.ReadLineAsync(cancellationToken);
                    if (line is null)
                    {
                        continue;
                    }

                    line = line.Trim();

                    // Final response starts with '{'
                    if (finalJson != null || line.StartsWith('{'))
                    {
                        finalJson += line;
                        continue;
                    }

                    var match = Regex.Match(line, @"\d+(\.\d+)?%");
                    if (match.Success)
                    {
                        var percentage = match.Value;
                        if (float.TryParse(percentage.TrimEnd('%'), out float progressValue))
                        {
                            progress?.Report(progressValue / 100);
                        }
                    }
                }

                // Parse closing JSON; default if malformed
                var result = finalJson is not null
                       ? JsonSerializer.Deserialize(finalJson, FoundryJsonContext.Default.FoundryDownloadResult)!
                       : new FoundryDownloadResult(false, "Missing final result from server.");

                return result;
            }
            catch (Exception e)
            {
                return new FoundryDownloadResult(false, e.Message);
            }
        });
    }

    // this is a temporary function to get the model path from the blob storage
    //  it will be removed once the tag is available in the list response
    private async Task<string> GetModelPath(string assetId)
    {
        var registryUri =
           $"https://eastus.api.azureml.ms/modelregistry/v1.0/registry/models/nonazureaccount?assetId={Uri.EscapeDataString(assetId)}";

        using var resp = await _httpClient.GetAsync(registryUri);
        resp.EnsureSuccessStatusCode();

        await using var jsonStream = await resp.Content.ReadAsStreamAsync();
        var jsonRoot = await JsonDocument.ParseAsync(jsonStream);
        var blobSasUri = jsonRoot.RootElement.GetProperty("blobSasUri").GetString()!;

        var uriBuilder = new UriBuilder(blobSasUri);
        var existingQuery = string.IsNullOrWhiteSpace(uriBuilder.Query)
            ? string.Empty
            : uriBuilder.Query.TrimStart('?') + "&";

        uriBuilder.Query = existingQuery + "restype=container&comp=list&delimiter=/";

        var listXml = await _httpClient.GetStringAsync(uriBuilder.Uri);

        var match = Regex.Match(listXml, @"<Name>(.*?)\/<\/Name>");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}