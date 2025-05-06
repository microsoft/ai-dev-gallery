// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using ColorCode.Compilation.Languages;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml.Documents;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AIDevGallery.ExternalModelUtils;

internal record FoundryModel(string Name, string Variant, string Size, string ParamSize, string Description, string License, string Task, string? Id = null);

internal class FoundryUtils
{
    public async static Task<(string? Output, string? Error, int ExitCode)> RunFoundryWithArguments(string arguments)
    {
        try
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = "foundry";
                p.StartInfo.Arguments = arguments;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                string output = await p.StandardOutput.ReadToEndAsync();
                string error = await p.StandardError.ReadToEndAsync();

                await p.WaitForExitAsync();

                return (output, error, p.ExitCode);
            }
        }
        catch
        {
            return (null, null, -1);
        }
    }
}

internal class FoundryCatalog
{
    private List<FoundryModel> _models = [];
    private bool _loaded;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private async Task LoadCatalog()
    {
        await _semaphore.WaitAsync();
        if (_loaded)
        {
            return;
        }

        var result = await FoundryUtils.RunFoundryWithArguments("model list");

        _loaded = true;

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
        {
            return;
        }

        var lines = result.Output.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (lines.Count > 1)
        {
            var headers = lines[0]
                .Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(h => h.ToLowerInvariant())
                .ToList();
            var nameIndex = headers.IndexOf("name");
            var variantIndex = headers.IndexOf("variant");
            var sizeIndex = headers.IndexOf("file size");
            var paramSizeIndex = headers.IndexOf("parameter size");
            var licenseIndex = headers.IndexOf("license");
            var descriptionIndex = headers.IndexOf("description");
            var taskIndex = headers.IndexOf("task");

            for (var i = 1; i < lines.Count; ++i)
            {
                var line = lines[i];
                var tokens = line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                _models.Add(new FoundryModel(
                    nameIndex != -1 ? tokens[nameIndex] : "Name not found",
                    variantIndex != -1 ? tokens[variantIndex] : string.Empty,
                    sizeIndex != -1 ? tokens[sizeIndex] : string.Empty,
                    paramSizeIndex != -1 ? tokens[paramSizeIndex] : string.Empty,
                    descriptionIndex != -1 ? tokens[descriptionIndex] : string.Empty,
                    licenseIndex != -1 ? tokens[licenseIndex] : string.Empty,
                    taskIndex != -1 ? tokens[taskIndex] : string.Empty));
            }
        }

        _semaphore.Release();
    }

    public async Task<List<FoundryModel>> GetAvailableModels()
    {
        await LoadCatalog();
        return _models;
    }

    public async Task<FoundryModel?> GetModelInfo(string modelId)
    {
        await LoadCatalog();
        return _models.FirstOrDefault(m => m.Name == modelId);
    }
}

internal class FoundryServiceManager()
{
    public static FoundryServiceManager? CreateAsync()
    {
        if (IsAvailable())
        {
            return new FoundryServiceManager();
        }

        return null;
    }

    private static bool IsAvailable()
    {
        // run "where foundry" to check if the foundry command is available
        using var p = new Process();
        p.StartInfo.FileName = "where";
        p.StartInfo.Arguments = "foundry";
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
        p.WaitForExit();
        return p.ExitCode == 0;
    }

    private string? GetUrl(string output)
    {
        var match = Regex.Match(output, @"https?:\/\/[^\/]+:\d+");
        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }

    public async Task<string?> GetServiceUrl()
    {
        var status = await FoundryUtils.RunFoundryWithArguments("service status");

        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return null;
        }

        return GetUrl(status.Output);
    }

    public async Task<bool> IsRunning()
    {
        var url = await GetServiceUrl();
        return url != null;
    }

    public async Task<bool> StartService()
    {
        if (await IsRunning())
        {
            return true;
        }

        var status = await FoundryUtils.RunFoundryWithArguments("service start");
        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return false;
        }

        return GetUrl(status.Output) != null;
    }
}

internal class FoundryManager()
{
    private static readonly Regex _tupleRegex =
        new Regex(@"\(\s*""[^""]*""\s*,\s*([0-9]*\.?[0-9]+)\s*\)",
                  RegexOptions.Compiled);

    public static async Task<FoundryManager?> CreateAsync(HttpClient httpClient = null)
    {
        var serviceManager = FoundryServiceManager.CreateAsync();
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

        return new FoundryManager(serviceUrl, serviceManager, httpClient ?? new HttpClient());
    }

    private FoundryCatalog _catalog;
    private FoundryServiceManager _serviceManager;
    private HttpClient _httpClient;
    private string _baseUrl;
    private record DownloadResult(bool Success, string? ErrorMessage);

    public FoundryManager(string baseUrl, FoundryServiceManager serviceManager, HttpClient httpClient)
        : this()
    {
        this._baseUrl = baseUrl;
        this._catalog = new FoundryCatalog();
        this._serviceManager = serviceManager;
        this._httpClient = httpClient;
    }

    public Task<List<FoundryModel>> ListCatalogModels()
    {
        return _catalog.GetAvailableModels();
    }

    public Task<FoundryModel?> GetModelInfo(string modelId)
    {
        return _catalog.GetModelInfo(modelId);
    }

    public async Task<string> GetCacheLocation()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/openai/status");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<(string Id, string Name)>> ListCachedModels()
    {
        // TODO: no way to match returned ids with catalog models yet
        // fallback to calling cli
        //var response = await _httpClient.GetAsync($"{_baseUrl}/openai/models");
        //response.EnsureSuccessStatusCode();

        //var content = await response.Content.ReadAsStringAsync();
        //return content.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries);

        var result = await FoundryUtils.RunFoundryWithArguments("cache list");

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
        {
            return [];
        }

        var lines = result.Output.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var models = new List<(string Id, string Name)>();

        foreach (var line in lines)
        {
            if (line.StartsWith("ðŸ’¾", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = line.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                models.Add((tokens[1], tokens[2]));
            }
        }

        return models;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
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

// FoundryLocal TODOs:
// Use the Foundry Local IChatClient
internal class FoundryLocalModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _downloadedModels;
    private IEnumerable<ModelDetails>? _catalogModels;
    private FoundryManager? _foundryManager;

    public static FoundryLocalModelProvider Instance { get; } = new FoundryLocalModelProvider();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run localy via Foundry Local";

    public string UrlPrefix => "fl://";

    public string LightIcon => "azure-ai-foundry.svg";

    public string DarkIcon => LightIcon;

    public string Url { get; private set; } = "http://localhost:5272/v1";

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";
    public string? GetDetailsUrl(ModelDetails details)
    {
        throw new NotImplementedException();
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri(Url)
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            Reset();
        }

        await InitializeAsync(cancelationToken);

        return _downloadedModels ?? [];
    }

    public IEnumerable<ModelDetails> GetAllModelsInCatalog()
    {
        return _catalogModels ?? [];
    }

    public Task<bool> DownloadModel(string name, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_foundryManager == null)
        {
            return Task.FromResult(false);
        }

        return _foundryManager.DownloadModel(name, progress, cancellationToken);
    }

    private void Reset()
    {
        _downloadedModels = null;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryManager != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        _foundryManager = await FoundryManager.CreateAsync();

        if (_foundryManager == null)
        {
            return;
        }

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await _foundryManager.ListCatalogModels()).Select(m => ToModelDetails(m.Name, m.Size, m.ParamSize, m.License));
        }

        var cachedModels = await _foundryManager.ListCachedModels();

        List<ModelDetails> downloadedModels = [];

        foreach (var model in _catalogModels)
        {
            var cachedModel = cachedModels.FirstOrDefault(m => m.Name == model.Name);

            if (cachedModel != default)
            {
                model.Id = $"{UrlPrefix}{cachedModel.Id}";
                downloadedModels.Add(model);
                cachedModels.Remove(cachedModel);
            }
        }

        foreach (var model in cachedModels)
        {
            downloadedModels.Add(ToModelDetails(model.Id, null, model.Name));
        }

        _downloadedModels = downloadedModels;

        return;
    }

    private ModelDetails ToModelDetails(string name, string? size = null, string? paramSize = null, string? license = null)
    {
        return new ModelDetails()
        {
            Id = $"fl-{name}",
            Name = name,
            Url = $"{UrlPrefix}{name}",
            Description = $"{name} running localy with Foundry Local",
            HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
            Size = size != null ? AppUtils.StringToFileSize(size) : 0,
            SupportedOnQualcomm = true,
            ParameterSize = paramSize,
            License = license?.ToLowerInvariant()
        };
    }
}