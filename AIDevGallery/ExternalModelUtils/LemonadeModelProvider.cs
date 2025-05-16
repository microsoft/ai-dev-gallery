// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Models;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

// TODOs
// icon - get official svg
// links to models?
// check if server is running and get port
internal class LemonadeModelProvider : IExternalModelProvider
{
    private IEnumerable<ModelDetails>? _cachedModels;
    public string Name => "Lemonade";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.AMD;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run localy via Lemonade";

    public string UrlPrefix => "lemonade://";

    public string LightIcon => "lemonade.svg";

    public string DarkIcon => "lemonade.svg";

    public string Url => "http://localhost:8000/api/v0";

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(CancellationToken cancelationToken = default)
    {
        if (_cachedModels != null && _cachedModels.Any())
        {
            return _cachedModels;
        }

        try
        {
            OpenAIModelClient client = new OpenAIModelClient(new ApiKeyCredential("not needed"), new OpenAIClientOptions
            {
                Endpoint = new Uri(Url)
            });

            // TODO: when server is not running this method never returns or throws
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
                HardwareAccelerators = [HardwareAccelerator.AMD],
                Size = 0,
                SupportedOnQualcomm = true,
                ParameterSize = string.Empty,
            };
        }
    }

    private static bool? isOllamaAvailable;

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
        {
            Endpoint = new Uri(Url)
        }).AsChatClient(modelId);
    }

    public string? GetDetailsUrl(ModelDetails details)
    {
        return $"https://github.com/onnx/turnkeyml/tree/main";
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Split('/').LastOrDefault();
        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).AsChatClient(\"{modelId}\")";
    }
}