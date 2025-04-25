// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal record FoundryLocalModel(string Name, string Variant, string Size, string ParamSize, string Description, string? Id);
internal record FoundryLocalCachedModel(string Id, string Name);

internal class FoundryLocalModelProvider : IExternalModelProvider
{
    private static bool? isAvailable;

    private IEnumerable<ModelDetails>? _cachedModels;

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run localy via Foundry Local";

    public string UrlPrefix => "fl://";

    public string LightIcon => "azure-ai-foundry.svg";

    public string DarkIcon => LightIcon;

    public string Url => "http://localhost:5272/v1";

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
        }).AsChatClient(modelId);
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).AsChatClient(\"{modelId}\")";
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(CancellationToken cancelationToken = default)
    {
        await InitializeAsync(cancelationToken);

        return _cachedModels != null && _cachedModels.Any() ? _cachedModels : [];
    }

    private static List<FoundryLocalModel>? GetModelListViaCli()
    {
        var cachedModels = GetCachedModelsViaCli();

        if (isAvailable != null && !isAvailable.Value)
        {
            return null;
        }

        try
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = "foundry";
                p.StartInfo.Arguments = "model list";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                p.WaitForExit();

                List<FoundryLocalModel> models = [];
                var lines = output.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                if (lines.Count > 1)
                {
                    for (var i = 1; i < lines.Count; ++i)
                    {
                        var line = lines[i];
                        var tokens = line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (tokens.Length != 6)
                        {
                            continue;
                        }

                        var cachedModel = cachedModels?.FirstOrDefault(cachedModels => cachedModels.Name == tokens[0]);

                        models.Add(new FoundryLocalModel(tokens[0], tokens[1], tokens[3], tokens[4], tokens[5], cachedModel?.Id));
                    }

                    isAvailable = true;
                }
                else
                {
                    isAvailable = false;
                }

                return models;
            }
        }
        catch
        {
            isAvailable = false;
            return null;
        }
    }

    private static List<FoundryLocalCachedModel>? GetCachedModelsViaCli()
    {
        if (isAvailable != null && !isAvailable.Value)
        {
            return null;
        }

        try
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = "foundry";
                p.StartInfo.Arguments = "cache list";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

                p.WaitForExit();

                List<FoundryLocalCachedModel> models = [];
                var lines = output.Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                if (lines.Count > 2)
                {
                    for (var i = 2; i < lines.Count; ++i)
                    {
                        var line = lines[i];
                        var tokens = line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (tokens.Length != 2)
                        {
                            continue;
                        }

                        models.Add(new (tokens[0].Substring(5), tokens[1]));
                    }

                    isAvailable = true;
                }
                else
                {
                    isAvailable = false;
                }

                return models;
            }
        }
        catch
        {
            isAvailable = false;
            return null;
        }
    }

    public async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_cachedModels != null && _cachedModels.Any())
        {
            return;
        }

        var allModels = GetModelListViaCli();

        if (allModels == null || allModels.Count == 0)
        {
            return;
        }

        _cachedModels = allModels.Where(m => m.Id != null)
            .Select(ToModelDetails)
            .ToList();

        static ModelDetails ToModelDetails(FoundryLocalModel model)
        {
            return new ModelDetails()
            {
                Id = $"fl-{model.Name}",
                Name = model.Name,
                Url = $"fl://{model.Id}",
                Description = $"{model.Name} running localy with Foundry Local",
                HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
                Size = AppUtils.StringToFileSize(model.Size),
                SupportedOnQualcomm = true,
                ParameterSize = model.ParamSize,
            };
        }
    }
}