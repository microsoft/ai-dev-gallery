// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Restored from pre-SDK migration (commit ba748417^) for performance benchmarking.
// This is the old HTTP-based FoundryClient that communicates via REST API.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests.FoundryLocalCli;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter

internal record CliPromptTemplate
{
    [JsonPropertyName("assistant")]
    public string Assistant { get; init; } = default!;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = default!;
}

internal record CliRuntime
{
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; init; } = default!;

    [JsonPropertyName("executionProvider")]
    public string ExecutionProvider { get; init; } = default!;
}

internal record CliCatalogModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = default!;

    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = default!;

    [JsonPropertyName("uri")]
    public string Uri { get; init; } = default!;

    [JsonPropertyName("promptTemplate")]
    public CliPromptTemplate PromptTemplate { get; init; } = default!;

    [JsonPropertyName("runtime")]
    public CliRuntime Runtime { get; init; } = default!;

    [JsonPropertyName("fileSizeMb")]
    public long FileSizeMb { get; init; }

    [JsonPropertyName("alias")]
    public string Alias { get; init; } = default!;

    [JsonPropertyName("license")]
    public string License { get; init; } = default!;
}

internal record CliCachedModel(string Name, string? Id);

internal record CliDownloadResult(bool Success, string? ErrorMessage);

internal record CliModelDownload(
    string Name,
    string Uri,
    string Path,
    string ProviderType,
    CliPromptTemplate PromptTemplate);

internal record CliDownloadBody(CliModelDownload Model, bool IgnorePipeReport);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(CliCatalogModel))]
[JsonSerializable(typeof(List<CliCatalogModel>))]
[JsonSerializable(typeof(CliDownloadResult))]
[JsonSerializable(typeof(CliDownloadBody))]
internal partial class CliFoundryJsonContext : JsonSerializerContext
{
}

internal class FoundryCliClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly List<CliCatalogModel> _catalogModels = [];

    public FoundryCliServiceManager ServiceManager { get; init; }

    private FoundryCliClient(string baseUrl, FoundryCliServiceManager serviceManager, HttpClient httpClient)
    {
        ServiceManager = serviceManager;
        _baseUrl = baseUrl;
        _httpClient = httpClient;
    }

    public static async Task<FoundryCliClient?> CreateAsync()
    {
        var serviceManager = FoundryCliServiceManager.TryCreate();
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

        return new FoundryCliClient(serviceUrl, serviceManager, new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
    }

    public async Task<List<CliCatalogModel>> ListCatalogModels()
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
                CliFoundryJsonContext.Default.ListCliCatalogModel);

            if (models != null && models.Count > 0)
            {
                models.ForEach(_catalogModels.Add);
            }
        }
        catch
        {
            // Silently fail for catalog listing
        }

        return _catalogModels;
    }

    public async Task<List<CliCachedModel>> ListCachedModels()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/openai/models");
        response.EnsureSuccessStatusCode();

        var catalogModels = await ListCatalogModels();

        var content = await response.Content.ReadAsStringAsync();
        var modelIds = content.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim('"'));

        List<CliCachedModel> models = [];

        foreach (var id in modelIds)
        {
            // Handle v0.8.x name format: catalog may have "name:version" while cached uses "name"
            var model = catalogModels.FirstOrDefault(m => m.Name == id || m.Name.StartsWith(id + ":", StringComparison.Ordinal));
            if (model != null)
            {
                models.Add(new CliCachedModel(id, model.Alias));
            }
            else
            {
                models.Add(new CliCachedModel(id, null));
            }
        }

        return models;
    }

    public async Task<CliDownloadResult> DownloadModel(CliCatalogModel model, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        var models = await ListCachedModels();

        // Handle v0.8.x: model.Name may have ":version" suffix, cached name may not
        var nameWithoutVersion = model.Name.Contains(':')
            ? model.Name[..model.Name.LastIndexOf(':')]
            : model.Name;

        if (models.Any(m => m.Name == model.Name || m.Name == nameWithoutVersion))
        {
            return new CliDownloadResult(true, "Model already downloaded");
        }

        return await Task.Run(
            async () =>
            {
                try
                {
                    // Fix for Foundry Local v0.8.x: List API returns name with ":version" suffix
                    // (e.g., "qwen2.5-coder-0.5b-instruct-generic-cpu:4"), but Download API
                    // expects name without version. Strip the suffix before calling download.
                    var downloadName = model.Name.Contains(':')
                        ? model.Name[..model.Name.LastIndexOf(':')]
                        : model.Name;

                    var uploadBody = new CliDownloadBody(
                        new CliModelDownload(
                            Name: downloadName,
                            Uri: model.Uri,
                            Path: await GetModelPath(model.Uri),
                            ProviderType: model.ProviderType,
                            PromptTemplate: model.PromptTemplate),
                        IgnorePipeReport: true);

                    string body = JsonSerializer.Serialize(
                         uploadBody,
                         CliFoundryJsonContext.Default.CliDownloadBody);

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

                    var result = finalJson is not null
                           ? JsonSerializer.Deserialize(finalJson, CliFoundryJsonContext.Default.CliDownloadResult)!
                           : new CliDownloadResult(false, "Missing final result from server.");

                    return result;
                }
                catch (Exception e)
                {
                    return new CliDownloadResult(false, e.Message);
                }
            },
            cancellationToken);
    }

    private async Task<string> GetModelPath(string assetId)
    {
        try
        {
            var registryUri =
               $"https://eastus.api.azureml.ms/modelregistry/v1.0/registry/models/nonazureaccount?assetId={System.Uri.EscapeDataString(assetId)}";

            using var resp = await _httpClient.GetAsync(registryUri);
            resp.EnsureSuccessStatusCode();

            await using var jsonStream = await resp.Content.ReadAsStreamAsync();
            var jsonRoot = await JsonDocument.ParseAsync(jsonStream);
            var blobSasUri = jsonRoot.RootElement.GetProperty("blobSasUri").GetString();
            if (string.IsNullOrEmpty(blobSasUri))
            {
                return string.Empty;
            }

            var uriBuilder = new UriBuilder(blobSasUri);
            var existingQuery = string.IsNullOrWhiteSpace(uriBuilder.Query)
                ? string.Empty
                : uriBuilder.Query.TrimStart('?') + "&";

            uriBuilder.Query = existingQuery + "restype=container&comp=list&delimiter=/";

            var listXml = await _httpClient.GetStringAsync(uriBuilder.Uri);

            var match = Regex.Match(listXml, @"<Name>(.*?)\/<\/Name>");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetModelPath] Failed for assetId '{assetId}': {ex.Message}");
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

#pragma warning restore SA1402
#pragma warning restore SA1313