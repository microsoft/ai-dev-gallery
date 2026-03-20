// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.IntegrationTests.FoundryLocalCli;
using AIDevGallery.Tests.TestInfra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

/// <summary>
/// Performance benchmark tests for Foundry Local CLI (HTTP API) operations.
/// This tests the old approach where Foundry Local is managed via CLI process calls
/// and communicated with via HTTP REST API.
/// </summary>
[TestClass]
[TestCategory("PerformanceBenchmark")]
public class FoundryLocalPerformanceBenchmark
{
    private const int Iterations = 5;
    private static FoundryCliClient? _client;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // Verify Foundry CLI is available
        var serviceManager = FoundryCliServiceManager.TryCreate();
        if (serviceManager == null)
        {
            context.WriteLine("Foundry Local CLI is not installed. Tests will be inconclusive.");
        }
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        _client?.Dispose();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        // Save after each test method to capture AsyncLocal measurements
        PerformanceCollector.Save();
    }

    private static void EnsureCliAvailable()
    {
        if (FoundryCliServiceManager.TryCreate() == null)
        {
            Assert.Inconclusive("Foundry Local CLI is not installed on this machine.");
        }
    }

    /// <summary>
    /// Measures the time to initialize the Foundry Local service from scratch via CLI.
    /// CLI: FoundryServiceManager.TryCreate() + StartService() + GetServiceUrl() + HttpClient setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [Timeout(300000)]
    public async Task BenchmarkInitialization()
    {
        EnsureCliAvailable();

        for (int i = 0; i < Iterations; i++)
        {
            // Dispose previous client to force re-initialization
            _client?.Dispose();
            _client = null;

            var sw = Stopwatch.StartNew();

            _client = await FoundryCliClient.CreateAsync();

            sw.Stop();

            Assert.IsNotNull(_client, "FoundryCliClient should initialize successfully");

            PerformanceCollector.Track(
                $"Initialization_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) }, { "cold_start", (i == 0).ToString(CultureInfo.InvariantCulture) } },
                "Initialization");

            PerformanceCollector.TrackCurrentProcessMemory(
                $"Initialization_Memory_Run{i}",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) } },
                "Initialization");

            Console.WriteLine($"[Initialization] Run {i}: {sw.ElapsedMilliseconds} ms (cold_start={i == 0})");
        }
    }

    /// <summary>
    /// Measures the time to query the full model catalog via HTTP API.
    /// CLI: GET /foundry/list → JSON deserialization.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [Timeout(120000)]
    public async Task BenchmarkCatalogQuery()
    {
        EnsureCliAvailable();
        await EnsureClientInitializedAsync();

        for (int i = 0; i < Iterations; i++)
        {
            // Force fresh query by creating a new client (old client caches catalog in memory)
            _client?.Dispose();
            _client = await FoundryCliClient.CreateAsync();
            Assert.IsNotNull(_client);

            var sw = Stopwatch.StartNew();

            var models = await _client.ListCatalogModels();

            sw.Stop();

            Assert.IsNotNull(models);
            Assert.IsTrue(models.Count > 0, "Catalog should have at least one model");

            PerformanceCollector.Track(
                $"CatalogQuery_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) }, { "model_count", models.Count.ToString(CultureInfo.InvariantCulture) } },
                "CatalogQuery");

            Console.WriteLine($"[CatalogQuery] Run {i}: {sw.ElapsedMilliseconds} ms ({models.Count} models)");
        }
    }

    /// <summary>
    /// Measures the time to download the smallest model via HTTP API.
    /// CLI: POST /openai/download → SSE stream with regex progress parsing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [Timeout(600000)]
    public async Task BenchmarkModelDownload()
    {
        EnsureCliAvailable();
        await EnsureClientInitializedAsync();

        var catalogModels = await _client!.ListCatalogModels();
        Assert.IsTrue(catalogModels.Count > 0, "Need at least one model to benchmark download");

        // Find the smallest model
        var smallestModel = catalogModels
            .Where(m => m.FileSizeMb > 0)
            .OrderBy(m => m.FileSizeMb)
            .FirstOrDefault();

        Assert.IsNotNull(smallestModel, "Could not find a model with valid file size");

        Console.WriteLine($"[ModelDownload] Selected model: {smallestModel.Name} ({smallestModel.FileSizeMb} MB)");

        for (int i = 0; i < Iterations; i++)
        {
            // Note: CLI approach doesn't have a direct "delete from cache" API,
            // so after the first download the model will already be cached.
            // The first iteration measures cold download, subsequent ones measure "already cached" check.
            var progress = new Progress<float>();

            var sw = Stopwatch.StartNew();

            var result = await _client.DownloadModel(smallestModel, progress, CancellationToken.None);

            sw.Stop();

            Assert.IsTrue(result.Success, $"Download should succeed: {result.ErrorMessage}");

            PerformanceCollector.Track(
                $"ModelDownload_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new()
                {
                    { "iteration", i.ToString(CultureInfo.InvariantCulture) },
                    { "model_name", smallestModel.Name },
                    { "model_size_mb", smallestModel.FileSizeMb.ToString(CultureInfo.InvariantCulture) }
                },
                "ModelDownload");

            PerformanceCollector.TrackCurrentProcessMemory(
                $"ModelDownload_Memory_Run{i}",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) } },
                "ModelDownload");

            Console.WriteLine($"[ModelDownload] Run {i}: {sw.ElapsedMilliseconds} ms");
        }
    }

    /// <summary>
    /// Measures the time to query cached (downloaded) models via HTTP API.
    /// CLI: GET /openai/models → parse JSON array.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [Timeout(120000)]
    public async Task BenchmarkCachedModelQuery()
    {
        EnsureCliAvailable();
        await EnsureClientInitializedAsync();

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();

            var cachedModels = await _client!.ListCachedModels();

            sw.Stop();

            Assert.IsNotNull(cachedModels);

            PerformanceCollector.Track(
                $"CachedModelQuery_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) }, { "cached_count", cachedModels.Count.ToString(CultureInfo.InvariantCulture) } },
                "CachedModelQuery");

            Console.WriteLine($"[CachedModelQuery] Run {i}: {sw.ElapsedMilliseconds} ms ({cachedModels.Count} cached)");
        }
    }

    private static async Task EnsureClientInitializedAsync()
    {
        if (_client != null)
        {
            return;
        }

        _client = await FoundryCliClient.CreateAsync();
        Assert.IsNotNull(_client, "Failed to initialize FoundryCliClient. Is Foundry Local CLI installed and running?");
    }
}