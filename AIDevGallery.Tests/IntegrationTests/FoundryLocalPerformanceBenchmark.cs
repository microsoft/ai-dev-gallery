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
    /// Includes step-by-step diagnostic logging to identify bottlenecks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [Timeout(300000)]
    public async Task BenchmarkInitialization()
    {
        EnsureCliAvailable();

        for (int i = 0; i < Iterations; i++)
        {
            _client?.Dispose();
            _client = null;

            var totalSw = Stopwatch.StartNew();

            // Step 1: Check CLI availability
            var stepSw = Stopwatch.StartNew();
            var serviceManager = FoundryCliServiceManager.TryCreate();
            stepSw.Stop();
            Console.WriteLine($"[Init][Run{i}] Step1 TryCreate: {stepSw.ElapsedMilliseconds} ms (result={serviceManager != null})");

            Assert.IsNotNull(serviceManager, "FoundryCliServiceManager.TryCreate() returned null");

            // Step 2: Check if service is running
            stepSw.Restart();
            var isRunning = await serviceManager.IsRunning();
            stepSw.Stop();
            Console.WriteLine($"[Init][Run{i}] Step2 IsRunning: {stepSw.ElapsedMilliseconds} ms (result={isRunning})");

            // Step 3: Start service if not running
            if (!isRunning)
            {
                stepSw.Restart();
                var started = await serviceManager.StartService();
                stepSw.Stop();
                Console.WriteLine($"[Init][Run{i}] Step3 StartService: {stepSw.ElapsedMilliseconds} ms (result={started})");

                if (!started)
                {
                    Console.WriteLine($"[Init][Run{i}] FAILED: Could not start service");
                    Assert.Fail("Could not start Foundry Local service");
                }
            }

            // Step 4: Get service URL
            stepSw.Restart();
            var serviceUrl = await serviceManager.GetServiceUrl();
            stepSw.Stop();
            Console.WriteLine($"[Init][Run{i}] Step4 GetServiceUrl: {stepSw.ElapsedMilliseconds} ms (url={serviceUrl})");

            totalSw.Stop();

            Assert.IsFalse(string.IsNullOrEmpty(serviceUrl), "Service URL should not be empty");

            // Create client for subsequent tests
            _client = await FoundryCliClient.CreateAsync();

            PerformanceCollector.Track(
                $"Initialization_Run{i}",
                totalSw.ElapsedMilliseconds,
                "ms",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) }, { "cold_start", (i == 0).ToString(CultureInfo.InvariantCulture) } },
                "Initialization");

            PerformanceCollector.TrackCurrentProcessMemory(
                $"Initialization_Memory_Run{i}",
                new() { { "iteration", i.ToString(CultureInfo.InvariantCulture) } },
                "Initialization");

            Console.WriteLine($"[Initialization] Run {i}: {totalSw.ElapsedMilliseconds} ms (cold_start={i == 0})");
        }

        PerformanceCollector.Save();
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

        PerformanceCollector.Save();
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

        PerformanceCollector.Save();
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

        PerformanceCollector.Save();
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