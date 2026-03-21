// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.IntegrationTests.FoundryLocalCli;
using AIDevGallery.Tests.TestInfra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

/// <summary>
/// Performance benchmark for Foundry Local CLI (HTTP API) catalog query operations.
/// Measures service startup + first catalog query, and subsequent HTTP queries.
/// </summary>
[TestClass]
[TestCategory("PerformanceBenchmark")]
public class FoundryLocalPerformanceBenchmark
{
    private static FoundryCliClient? _client;

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        _client?.Dispose();
    }

    private static void EnsureCliAvailable()
    {
        if (FoundryCliServiceManager.TryCreate() == null)
        {
            Assert.Inconclusive("Foundry Local CLI is not installed on this machine.");
        }
    }

    /// <summary>
    /// Measures the full cold path: CLI service startup + first HTTP catalog query.
    /// This test is run in a fresh process each time (via workflow loop) to get true cold start data.
    /// Includes step-by-step timing for diagnostics.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [TestCategory("FirstCatalogQuery")]
    [Timeout(120000)]
    public async Task BenchmarkFirstCatalogQuery()
    {
        EnsureCliAvailable();

        var totalSw = Stopwatch.StartNew();

        // Step 1: Create client (includes service discovery/start + get URL)
        var stepSw = Stopwatch.StartNew();
        var client = await FoundryCliClient.CreateAsync();
        stepSw.Stop();
        var clientCreateMs = stepSw.ElapsedMilliseconds;
        Console.WriteLine($"[FirstCatalogQuery] ClientCreate: {clientCreateMs} ms");

        if (client == null)
        {
            Console.WriteLine("[FirstCatalogQuery] FAILED: Could not create CLI client");
            Assert.Inconclusive("Could not create CLI client. Service may not be available.");
            return;
        }

        // Step 2: Query catalog (first HTTP request)
        stepSw.Restart();
        var models = await client.ListCatalogModels();
        stepSw.Stop();
        var listCatalogMs = stepSw.ElapsedMilliseconds;

        totalSw.Stop();

        Assert.IsNotNull(models);
        Assert.IsTrue(models.Count > 0, "Catalog should have at least one model");

        Console.WriteLine($"[FirstCatalogQuery] ListCatalog: {listCatalogMs} ms ({models.Count} models)");
        Console.WriteLine($"[FirstCatalogQuery] Total: {totalSw.ElapsedMilliseconds} ms");

        PerformanceCollector.Track("FirstCatalogQuery_ClientCreate", clientCreateMs, "ms", category: "FirstCatalogQuery");
        PerformanceCollector.Track("FirstCatalogQuery_ListCatalog", listCatalogMs, "ms", category: "FirstCatalogQuery");
        PerformanceCollector.Track(
            "FirstCatalogQuery_Total",
            totalSw.ElapsedMilliseconds,
            "ms",
            new() { { "model_count", models.Count.ToString(CultureInfo.InvariantCulture) } },
            "FirstCatalogQuery");

        client.Dispose();
        PerformanceCollector.Save();
    }

    /// <summary>
    /// Measures subsequent catalog queries via HTTP API (warm path).
    /// Each iteration creates a new client to force a fresh HTTP request,
    /// but the Foundry service is already running.
    /// Runs 20 iterations in a single process.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [TestCategory("SubsequentCatalogQuery")]
    [Timeout(120000)]
    public async Task BenchmarkSubsequentCatalogQuery()
    {
        EnsureCliAvailable();

        // Ensure service is running first
        _client = await FoundryCliClient.CreateAsync();
        if (_client == null)
        {
            Assert.Inconclusive("Could not create CLI client. Service may not be available.");
            return;
        }

        // Warm-up call
        await _client.ListCatalogModels();
        _client.Dispose();

        for (int i = 0; i < 20; i++)
        {
            // Create fresh client each time to force new HTTP connection + query
            var client = await FoundryCliClient.CreateAsync();
            Assert.IsNotNull(client, "Client creation failed during iteration");

            var sw = Stopwatch.StartNew();
            var models = await client.ListCatalogModels();
            sw.Stop();

            Assert.IsNotNull(models);
            Assert.IsTrue(models.Count > 0);

            PerformanceCollector.Track(
                $"SubsequentCatalogQuery_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) }, { "model_count", models.Count.ToString(CultureInfo.InvariantCulture) } },
                "SubsequentCatalogQuery");

            Console.WriteLine($"[SubsequentCatalogQuery] Run {i}: {sw.ElapsedMilliseconds} ms ({models.Count} models)");
            client.Dispose();
        }

        PerformanceCollector.Save();
    }
}