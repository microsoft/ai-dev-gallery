// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

[TestClass]
[TestCategory("Integration")]
public class MiniMaxIntegrationTests
{
    private static string? GetApiKey()
    {
        return Environment.GetEnvironmentVariable("MINIMAX_API_KEY");
    }

    [TestMethod]
    public async Task GetModelsAsync_WithValidApiKey_ReturnsModels()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Inconclusive("MINIMAX_API_KEY environment variable not set. Skipping integration test.");
            return;
        }

        // Set the key temporarily
        var originalKey = MiniMaxModelProvider.MiniMaxKey;
        try
        {
            MiniMaxModelProvider.MiniMaxKey = apiKey;

            var models = await MiniMaxModelProvider.Instance.GetModelsAsync();
            Assert.IsNotNull(models);

            var modelList = new System.Collections.Generic.List<AIDevGallery.Models.ModelDetails>(models);
            Assert.IsTrue(modelList.Count > 0, "Should return at least one model");
        }
        finally
        {
            MiniMaxModelProvider.MiniMaxKey = originalKey;
        }
    }

    [TestMethod]
    public async Task GetIChatClient_WithValidApiKey_CanSendMessage()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Inconclusive("MINIMAX_API_KEY environment variable not set. Skipping integration test.");
            return;
        }

        var originalKey = MiniMaxModelProvider.MiniMaxKey;
        try
        {
            MiniMaxModelProvider.MiniMaxKey = apiKey;

            var client = MiniMaxModelProvider.Instance.GetIChatClient("minimax://MiniMax-M2.5-highspeed");
            Assert.IsNotNull(client, "Should create a chat client with valid API key");

            var response = await client.GetResponseAsync("Say hello in one word.");
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Text.Length > 0, "Response should contain text");
        }
        finally
        {
            MiniMaxModelProvider.MiniMaxKey = originalKey;
        }
    }

    [TestMethod]
    public async Task GetIChatClient_WithValidApiKey_SupportsStreaming()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Assert.Inconclusive("MINIMAX_API_KEY environment variable not set. Skipping integration test.");
            return;
        }

        var originalKey = MiniMaxModelProvider.MiniMaxKey;
        try
        {
            MiniMaxModelProvider.MiniMaxKey = apiKey;

            var client = MiniMaxModelProvider.Instance.GetIChatClient("minimax://MiniMax-M2.5-highspeed");
            Assert.IsNotNull(client, "Should create a chat client with valid API key");

            var chunks = new System.Text.StringBuilder();
            await foreach (var update in client.GetStreamingResponseAsync("Say hello in one word."))
            {
                if (update.Text != null)
                {
                    chunks.Append(update.Text);
                }
            }

            Assert.IsTrue(chunks.Length > 0, "Streaming response should contain text");
        }
        finally
        {
            MiniMaxModelProvider.MiniMaxKey = originalKey;
        }
    }
}
