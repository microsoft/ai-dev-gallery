// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Extensions.AI;
using Microsoft.Win32;
using OpenAI;
using OpenAI.Models;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

// TODOs
// links to models?
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
            using var p = new Process();
            p.StartInfo.FileName = "lemonade-server";
            p.StartInfo.Arguments = "status";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            string output = await p.StandardOutput.ReadToEndAsync(cancellationToken);

            await p.WaitForExitAsync(cancellationToken);

            if (p.ExitCode == 0)
            {
                return output;
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                Match m = Regex.Match(output, @"\b(\d{1,5})\b$");
                if (m.Success)
                {
                    return $"\"http://localhost:{m.Groups[1].Value}/api/v0";
                }
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
                    return [];
                }
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
        string? cpu = Registry.GetValue("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0", "ProcessorNameString", null) as string;

        if (string.IsNullOrWhiteSpace(cpu) || !Regex.IsMatch(cpu, @"Ryzen AI.*\b3\d{2}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
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