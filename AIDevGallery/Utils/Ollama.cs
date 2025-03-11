// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;

namespace Utils;

internal record OllamaModel(string Name, string Tag, string Id, string Size, string Modified);

internal class Ollama
{
    public static List<OllamaModel>? GetOllamaModels()
    {
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
                string output = p.StandardOutput.ReadToEnd();
                var lines = output.Split('\n');

                List<OllamaModel> models = new();

                if (lines.Length > 1)
                {
                    for (var i = 1; i < lines.Length; ++i)
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
                }

                return models;
            }
        }
        catch
        {
            return null;
        }
    }
}