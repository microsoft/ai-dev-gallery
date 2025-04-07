// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
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
    public string Name => "Ollama";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.OLLAMA;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.Ollama"];

    public string ProviderDescription => "The model will run localy via Ollama";

    public string UrlPrefix => "ollama://";

    public string LightIcon => "ollama.light.svg";

    public string DarkIcon => "ollama.dark.svg";

    public string Url => Environment.GetEnvironmentVariable("OLLAMA_HOST", EnvironmentVariableTarget.User) ?? "http://localhost:11434/";

    public Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(CancellationToken cancelationToken = default)
    {
        var ollamaModels = await GetOllamaModelsAsync(cancelationToken);

        if (ollamaModels == null)
        {
            return [];
        }

        return ollamaModels.Select(ToModelDetails)
            .Where(modelDetails => modelDetails != null)
            .ToList();

        ModelDetails ToModelDetails(OllamaModel om)
        {
            return new ModelDetails()
            {
                Id = $"ollama-{om.Id}",
                Name = om.Name,
                Url = $"{UrlPrefix}{om.Name}:{om.Tag}",
                Description = $"{om.Name}:{om.Tag} running locally via Ollama",
                HardwareAccelerators = new List<HardwareAccelerator>() { HardwareAccelerator.OLLAMA },
                Size = AppUtils.StringToFileSize(om.Size),
                SupportedOnQualcomm = true,
                ParameterSize = om.Tag.ToUpperInvariant(),
            };
        }
    }

    private static bool? isOllamaAvailable;

    public static async Task<List<OllamaModel>?> GetOllamaModelsAsync(CancellationToken cancelationToken)
    {
        if (isOllamaAvailable != null && !isOllamaAvailable.Value)
        {
            return null;
        }

        try
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = "ollama";
                p.StartInfo.Arguments = "list";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();
                await p.WaitForExitAsync(cancelationToken).ConfigureAwait(false);

                var lines = new List<string>();

                await Task.WhenAny(
                    Task.Run(
                        async () =>
                        {
                            while (p.StandardOutput.Peek() > -1)
                            {
                                var line = await p.StandardOutput.ReadLineAsync(cancelationToken).ConfigureAwait(false);
                                lines.Add(line ?? string.Empty);
                            }
                        },
                        cancelationToken),
                    Task.Delay(10, cancelationToken)).ConfigureAwait(false);

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

                return models;
            }
        }
        catch
        {
            isOllamaAvailable = false;
            return null;
        }
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return new OllamaChatClient(Url, modelId);
    }

    public string? GetDetailsUrl(ModelDetails details)
    {
        return $"https://ollama.com/library/{details.Name}";
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OllamaChatClient(\"{Url}\", \"{modelId}\")";
    }
}