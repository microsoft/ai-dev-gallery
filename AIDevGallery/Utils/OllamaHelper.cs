// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal record OllamaModel(string Name, string Tag, string Id, string Size, string Modified);

internal class OllamaHelper
{
    private static bool? isOllamaAvailable;
    public static List<ModelDetails>? GetOllamaModels()
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
                p.WaitForExit();

                var lines = new List<string>();

                Task.WhenAny(
                    Task.Run(() =>
                    {
                        while (p.StandardOutput.Peek() > -1)
                        {
                            var line = p.StandardOutput.ReadLine();
                            lines.Add(line ?? string.Empty);
                        }
                    }),
                    Task.Delay(10)).Wait();

                List<OllamaModel> models = new();

                if (lines.Count > 1)
                {
                    for (var i = 1; i < lines.Count; ++i)
                    {
                        var line = lines[i];
                        var tokens = line.Split("  ", System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
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

                return models.Select(om => new ModelDetails()
                {
                    Id = $"ollama-{om.Id}",
                    Name = om.Name,
                    Url = $"ollama://{om.Name}:{om.Tag}",
                    Description = $"{om.Name}:{om.Tag} running locally via Ollama",
                    HardwareAccelerators = new List<HardwareAccelerator>() { HardwareAccelerator.OLLAMA },
                    Size = AppUtils.StringToFileSize(om.Size),
                    SupportedOnQualcomm = true,
                    ParameterSize = om.Tag.ToUpperInvariant(),
                }).ToList();
            }
        }
        catch
        {
            isOllamaAvailable = false;
            return null;
        }
    }

    public static string GetOllamaUrl()
    {
        return Environment.GetEnvironmentVariable("OLLAMA_HOST", EnvironmentVariableTarget.User) ?? "http://localhost:11434/";
    }
}