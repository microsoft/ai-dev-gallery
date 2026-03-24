// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils;

internal class MiniMaxModelProvider : IExternalModelProvider
{
    public static MiniMaxModelProvider Instance { get; } = new MiniMaxModelProvider();

    private const string KeyName = "AI_DEV_GALLERY_MINIMAX_API_KEY";

    public static string? MiniMaxKey
    {
        get
        {
            return CredentialManager.ReadCredential(KeyName);
        }
        set
        {
            if (value != null)
            {
                CredentialManager.WriteCredential(KeyName, value);
            }
            else
            {
                CredentialManager.DeleteCredential(KeyName);
            }
        }
    }

    public string Name => "MiniMax";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.MINIMAX;

    public List<string> NugetPackageReferences => ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run on the cloud via MiniMax";

    public string UrlPrefix => "minimax://";

    public string Icon => $"MiniMax{AppUtils.GetThemeAssetSuffix()}.svg";

    public string Url => "https://api.minimax.io/v1";

    public string? IChatClientImplementationNamespace { get; } = "OpenAI";

    // MiniMax models with their display names
    private static readonly (string Id, string DisplayName, string Description)[] KnownModels =
    [
        ("MiniMax-M2.7", "MiniMax-M2.7", "MiniMax M2.7 - latest flagship model with 1M context"),
        ("MiniMax-M2.5", "MiniMax-M2.5", "MiniMax M2.5 with 204K context window"),
        ("MiniMax-M2.5-highspeed", "MiniMax-M2.5-highspeed", "MiniMax M2.5 high-speed variant with 204K context"),
    ];

    public string? GetDetailsUrl(ModelDetails details)
    {
        return "https://platform.minimaxi.com/document/Models";
    }

    public IChatClient? GetIChatClient(string url)
    {
        var modelId = url.Replace(UrlPrefix, string.Empty);
        if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(MiniMaxKey))
        {
            return null;
        }

        return new OpenAIClient(new ApiKeyCredential(MiniMaxKey), new OpenAIClientOptions
        {
            Endpoint = new Uri(Url)
        }).GetChatClient(modelId).AsIChatClient();
    }

    public string? GetIChatClientString(string url)
    {
        var modelId = url.Replace(UrlPrefix, string.Empty);

        return $"new OpenAIClient(new ApiKeyCredential(\"MINIMAX_API_KEY\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{Url}\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
    }

    public void ClearCachedModels()
    {
        // Static model list, nothing to clear
    }

    public Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(MiniMaxKey))
        {
            return Task.FromResult<IEnumerable<ModelDetails>>([]);
        }

        var models = KnownModels.Select(m => new ModelDetails
        {
            Id = $"minimax-{m.Id}",
            Name = m.DisplayName,
            Url = $"{UrlPrefix}{m.Id}",
            Description = m.Description,
            HardwareAccelerators = [HardwareAccelerator.MINIMAX],
            Size = 0,
            SupportedOnQualcomm = true,
            ParameterSize = string.Empty,
        });

        return Task.FromResult(models);
    }
}
