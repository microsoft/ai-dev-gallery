// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal record OllamaModel(string Name, string Tag, string Id, string Size, string Modified);

internal class OllamaModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _cachedModels;

    public static OllamaModelProvider Instance { get; } = new OllamaModelProvider();

    public string Name => "Ollama";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.OLLAMA;

    public List<string> NugetPackageReferences => ["OllamaSharp"];

    public string ProviderDescription => "The model will run locally via Ollama";

    public string UrlPrefix => "ollama://";

    public string Icon => $"Ollama{AppUtils.GetThemeAssetSuffix()}.svg";

    public string Url => Environment.GetEnvironmentVariable("OLLAMA_HOST", EnvironmentVariableTarget.User) ?? "http://localhost:11434/";

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            isOllamaAvailable = null;
            _cachedModels = null;
        }

        if (isOllamaAvailable != null && !isOllamaAvailable.Value)
        {
            return [];
        }

        if (_cachedModels != null && _cachedModels.Any())
        {
            return _cachedModels;
        }

        try
        {
            var lines = new List<string>();

            await Task.WhenAny(
                Task.Run(
                    async () =>
                    {
                        using var p = new Process();
                        p.StartInfo.FileName = "ollama";
                        p.StartInfo.Arguments = "list";
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.RedirectStandardError = true;
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.CreateNoWindow = true;

                        p.Start();
                        await p.WaitForExitAsync(cancelationToken).ConfigureAwait(false);

                        while (p.StandardOutput.Peek() > -1)
                        {
                            var line = await p.StandardOutput.ReadLineAsync(cancelationToken).ConfigureAwait(false);
                            lines.Add(line ?? string.Empty);
                        }
                    },
                    cancelationToken),
                Task.Delay(1000, cancelationToken)).ConfigureAwait(false);

            List<OllamaModel> models = [];

            if (lines.Count > 1)
            {
                for (var i = 1; i < lines.Count; ++i)
                {
                    var line = lines[i];
                    var tokens = line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (tokens.Length != 4)
                    {
                        continue;
                    }

                    var nameTag = tokens[0].Split(':');

                    models.Add(new OllamaModel(nameTag[0], nameTag[1], tokens[1], tokens[2], tokens[3]));
                }

                isOllamaAvailable = true;
            }
            else
            {
                isOllamaAvailable = false;
            }

            _cachedModels = models.Select(om => new ModelDetails()
            {
                Id = $"ollama-{om.Id}",
                Name = $"{om.Name}:{om.Tag}",
                Url = $"ollama://{om.Name}:{om.Tag}",
                Description = $"{om.Name}:{om.Tag} running locally via Ollama",
                HardwareAccelerators = new List<HardwareAccelerator>() { HardwareAccelerator.OLLAMA },
                Size = AppUtils.StringToFileSize(om.Size),
                SupportedOnQualcomm = true,
                ParameterSize = om.Tag.ToUpperInvariant(),
            });

            return _cachedModels;
        }
        catch
        {
            isOllamaAvailable = false;
            return [];
        }
    }

    private static bool? isOllamaAvailable;

    public async Task<bool> IsAvailable()
    {
        await GetModelsAsync();
        return isOllamaAvailable ?? false;
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return modelId == null ? null : new OllamaApiClient(Url, modelId);
    }

    public string? GetDetailsUrl(ModelDetails details)
    {
        return $"https://ollama.com/library/{details.Name}";
    }

    public string? IChatClientImplementationNamespace { get; }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return modelId == null ? null : $"new OllamaApiClient(\"{Url}\", \"{modelId}\")";
    }
}