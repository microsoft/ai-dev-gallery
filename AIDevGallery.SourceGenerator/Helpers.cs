// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Models;
using AIDevGallery.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace AIDevGallery.SourceGenerator;

internal static class Helpers
{
    internal static string EscapeUnicodeString(string unicodeString)
    {
        return JsonSerializer.Serialize(unicodeString, SourceGenerationContext.Default.String);
    }

    private static ModelFamily Fix(ModelFamily modelFamily)
    {
        string? id = modelFamily.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
            return new ModelFamily
            {
                Id = id,
                Name = modelFamily.Name,
                Description = modelFamily.Description,
                DocsUrl = modelFamily.DocsUrl,
                Models = modelFamily.Models,
                ReadmeUrl = modelFamily.ReadmeUrl,
            };
        }

        return modelFamily;
    }

    private static ModelGroup Fix(ModelGroup modelGroup)
    {
        string? id = modelGroup.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
            return new ModelGroup
            {
                Id = id,
                Name = modelGroup.Name,
                Icon = modelGroup.Icon,
                Models = modelGroup.Models
            };
        }

        return modelGroup;
    }

    private static async Task<Model> FixAsync(Model model, CancellationToken cancellationToken)
    {
        long? size = model.Size;

        if (size is null or 0)
        {
            List<ModelFileDetails> filesToDownload;
            if (model.Url.StartsWith("https://github.com", StringComparison.InvariantCulture))
            {
                var ghUrl = new GitHubUrl(model.Url);
                filesToDownload = await ModelInformationHelper.GetDownloadFilesFromGitHub(ghUrl, cancellationToken);
            }
            else
            {
                var hfUrl = new HuggingFaceUrl(model.Url);
                using var httpClientHandler = new HttpClientHandler();
                filesToDownload = await ModelInformationHelper.GetDownloadFilesFromHuggingFace(hfUrl, httpClientHandler, cancellationToken);
            }

            filesToDownload = ModelInformationHelper.FilterFiles(filesToDownload, model.FileFilters);

            size = filesToDownload.Sum(f => f.Size);
        }

        string? id = model.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
        }

        return new Model
        {
            Id = id,
            Name = model.Name,
            Url = model.Url,
            Description = model.Description,
            HardwareAccelerators = model.HardwareAccelerators,
            SupportedOnQualcomm = model.SupportedOnQualcomm,
            Size = size,
            ParameterSize = model.ParameterSize,
            Icon = model.Icon,
            PromptTemplate = model.PromptTemplate,
            License = model.License,
            FileFilters = model.FileFilters
        };
    }

    internal static async Task<string> FixModelGroupAsync(Dictionary<string, ModelGroup> modelGroups, CancellationToken cancellationToken)
    {
        for (int k = 0; k < modelGroups.Values.Count; k++)
        {
            var modelGroup = modelGroups.ElementAt(k);
            modelGroups[modelGroup.Key] = Fix(modelGroup.Value);
            modelGroup = modelGroups.ElementAt(k);

            for (int j = 0; j < modelGroup.Value.Models.Count; j++)
            {
                var modelFamily = modelGroup.Value.Models.ElementAt(j);
                modelGroup.Value.Models[modelFamily.Key] = Fix(modelFamily.Value);
                modelFamily = modelGroup.Value.Models.ElementAt(j);

                for (int i = 0; i < modelFamily.Value.Models.Count; i++)
                {
                    var model = modelFamily.Value.Models.ElementAt(i);
                    modelFamily.Value.Models[model.Key] = await FixAsync(model.Value, cancellationToken);
                }
            }
        }

        return JsonSerializer.Serialize(modelGroups, SourceGenerationContext.Default.DictionaryStringModelGroup);
    }

    internal static async Task<string> FixModelFamiliesAsync(Dictionary<string, ModelFamily> modelFamilies, CancellationToken cancellationToken)
    {
        for (int j = 0; j < modelFamilies.Values.Count; j++)
        {
            var modelFamily = modelFamilies.ElementAt(j);
            modelFamilies[modelFamily.Key] = Fix(modelFamily.Value);
            modelFamily = modelFamilies.ElementAt(j);

            for (int i = 0; i < modelFamily.Value.Models.Count; i++)
            {
                var model = modelFamily.Value.Models.ElementAt(i);
                modelFamily.Value.Models[model.Key] = await FixAsync(model.Value, cancellationToken);
            }
        }

        return JsonSerializer.Serialize(modelFamilies, SourceGenerationContext.Default.DictionaryStringModelFamily);
    }

    internal static Dictionary<string, string> GetPackageVersions()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var packageVersions = new Dictionary<string, string>();

        using (Stream stream = assembly.GetManifestResourceStream("AIDevGallery.SourceGenerator.Directory.Packages.props"))
        {
            using (XmlTextReader xmlReader = new(stream))
            {
                while (xmlReader.ReadToFollowing("PackageVersion"))
                {
                    packageVersions.Add(xmlReader.GetAttribute("Include"), xmlReader.GetAttribute("Version"));
                }

                return packageVersions;
            }
        }
    }
}