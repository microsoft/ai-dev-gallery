// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

/// <summary>
/// Factory for creating an IChatClient backed by Foundry Local SDK.
/// Handles the multi-step initialization (manager → catalog → model → load → chat client)
/// and wraps the SDK's OpenAIChatClient into an IChatClient via FoundryLocalChatClientAdapter.
/// </summary>
internal static class FoundryLocalChatClientFactory
{
    public static async Task<IChatClient?> CreateAsync(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!FoundryLocalManager.IsInitialized)
            {
                var config = new Configuration
                {
                    AppName = "AIDevGallery"
                };

                await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);
            }

            var manager = FoundryLocalManager.Instance;

            try
            {
                await manager.EnsureEpsDownloadedAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FoundryLocal] EP registration issue: {ex.Message}");
            }

            var catalog = await manager.GetCatalogAsync();
            var model = await catalog.GetModelAsync(alias);

            if (model == null)
            {
                throw new InvalidOperationException($"Model '{alias}' not found in Foundry Local catalog.");
            }

            if (!await model.IsLoadedAsync())
            {
                if (!await model.IsCachedAsync())
                {
                    Debug.WriteLine($"[FoundryLocal] Model '{alias}' is not cached. Downloading...");
                    await model.DownloadAsync(null, cancellationToken);
                    Debug.WriteLine($"[FoundryLocal] Model '{alias}' download completed.");
                }

                await model.LoadAsync(cancellationToken);
            }

            var chatClient = await model.GetChatClientAsync();
            var maxOutputTokens = (int?)model.SelectedVariant.Info.MaxOutputTokens;

            return new FoundryLocalChatClientAdapter(chatClient, model.Id, maxOutputTokens);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FoundryLocal] Failed to create chat client for '{alias}': {ex.Message}");
            throw;
        }
    }
}