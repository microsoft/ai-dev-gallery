// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using Microsoft.Extensions.AI;
using Microsoft.Win32;
using OpenAI;
using OpenAI.Models;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal class LemonadeModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _cachedModels;
    private string? _url;
    private bool? _isLemonadeAvailable;

    public static LemonadeModelProvider Instance { get; } = new LemonadeModelProvider();

    public string Name => "Lemonade";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.LEMONADE;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run localy via Lemonade";

    public string UrlPrefix => "lemonade://";

    public string Url => _url ?? "http://localhost:8000/api/v0";

    public string Icon => $"lemonade.svg";

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";

    private async Task<string?> InitializeLemonadeServer(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExternalModelHelper.GetFromProcessAsync("where", "lemonade-server", cancellationToken);

            if (result == null || result.Value.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Value.Output))
            {
                return null;
            }

            result = await ExternalModelHelper.GetFromProcessAsync(result.Value.Output, "status", cancellationToken);

            if (result == null || result.Value.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Value.Output))
            {
                return null;
            }

            Match m = Regex.Match(result.Value.Output.Trim(), @"\b(\d{1,5})\b$");
            if (m.Success)
            {
                return $"http://localhost:{m.Groups[1].Value}/api/v0";
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            _cachedModels = null;
        }

        if (_cachedModels != null && _cachedModels.Any())
        {
            return _cachedModels;
        }

        if (_isLemonadeAvailable != null && !_isLemonadeAvailable.Value)
        {
            return [];
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                _url = await InitializeLemonadeServer(cancelationToken);
                if (string.IsNullOrWhiteSpace(_url))
                {
                    _isLemonadeAvailable = false;
                    return [];
                }

                _isLemonadeAvailable = true;
            }

            OpenAIModelClient client = new OpenAIModelClient(new ApiKeyCredential("not-needed"), new OpenAIClientOptions
            {
                Endpoint = new Uri(Url)
            });

            var models = await client.GetModelsAsync(cancelationToken);

            if (models?.Value == null)
            {
                return [];
            }

            _cachedModels = [.. models.Value
                .Where(model => model != null && model.Id != null)
                .Select(ToModelDetails)];

            return _cachedModels;
        }
        catch
        {
            return [];
        }

        static ModelDetails ToModelDetails(OpenAIModel model)
        {
            return new ModelDetails()
            {
                Id = $"lemonade-{model.Id}",
                Name = model.Id,
                Url = $"lemonade://{model.Id}",
                Description = $"{model.Id} running localy via Lemonade",
                HardwareAccelerators = [HardwareAccelerator.LEMONADE],
                Size = 0,
                SupportedOnQualcomm = true,
                ParameterSize = string.Empty,
            };
        }
    }

    public async Task<bool> IsAvailable()
    {
        if (_isLemonadeAvailable != null)
        {
            return _isLemonadeAvailable.Value;
        }

        string? cpu = Registry.GetValue("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0", "ProcessorNameString", null) as string;

        if (string.IsNullOrWhiteSpace(cpu) || !Regex.IsMatch(cpu, @"Ryzen AI.*\b3\d{2}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            _isLemonadeAvailable = false;
            return false;
        }

        if (_isLemonadeAvailable == null)
        {
            _url = await InitializeLemonadeServer();
            _isLemonadeAvailable = !string.IsNullOrWhiteSpace(_url);
        }

        return _isLemonadeAvailable ?? false;
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri(Url)
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetDetailsUrl(ModelDetails details)
    {
        return $"https://github.com/onnx/turnkeyml/tree/main";
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
    }
}