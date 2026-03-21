// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

/// <summary>
/// Performance benchmark for Foundry Local SDK catalog query operations.
/// Measures initialization + first catalog query, and subsequent cached queries.
/// </summary>
[TestClass]
[TestCategory("PerformanceBenchmark")]
public class FoundryLocalPerformanceBenchmark
{
    private static string? _testCacheDir;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryPerfBenchmark", "foundrycache");
        Directory.CreateDirectory(_testCacheDir);
    }

    /// <summary>
    /// Measures the full cold path: SDK initialization + first catalog query.
    /// This test is run in a fresh process each time (via workflow loop) to get true cold start data.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [TestCategory("FirstCatalogQuery")]
    public async Task BenchmarkFirstCatalogQuery()
    {
        var totalSw = Stopwatch.StartNew();

        // Step 1: Initialize SDK
        var stepSw = Stopwatch.StartNew();
        if (!FoundryLocalManager.IsInitialized)
        {
            var config = new Configuration
            {
                AppName = "AIDevGalleryPerfBenchmark",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                ModelCacheDir = _testCacheDir!
            };

            try
            {
                await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);
            }
            catch (FoundryLocalException) when (FoundryLocalManager.IsInitialized)
            {
            }
        }

        stepSw.Stop();
        var initMs = stepSw.ElapsedMilliseconds;
        Console.WriteLine($"[FirstCatalogQuery] SDK Init: {initMs} ms");

        if (!FoundryLocalManager.IsInitialized)
        {
            Assert.Inconclusive("Foundry Local SDK did not initialize on this platform.");
        }

        // Step 2: Get catalog (first time)
        stepSw.Restart();
        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        stepSw.Stop();
        var getCatalogMs = stepSw.ElapsedMilliseconds;
        Console.WriteLine($"[FirstCatalogQuery] GetCatalog: {getCatalogMs} ms");

        // Step 3: List models (first time)
        stepSw.Restart();
        var models = await catalog.ListModelsAsync();
        stepSw.Stop();
        var listModelsMs = stepSw.ElapsedMilliseconds;

        totalSw.Stop();

        Assert.IsNotNull(models);
        Assert.IsTrue(models.Count > 0, "Catalog should have at least one model");

        Console.WriteLine($"[FirstCatalogQuery] ListModels: {listModelsMs} ms ({models.Count} models)");
        Console.WriteLine($"[FirstCatalogQuery] Total: {totalSw.ElapsedMilliseconds} ms");

        PerformanceCollector.Track("FirstCatalogQuery_Init", initMs, "ms", category: "FirstCatalogQuery");
        PerformanceCollector.Track("FirstCatalogQuery_GetCatalog", getCatalogMs, "ms", category: "FirstCatalogQuery");
        PerformanceCollector.Track("FirstCatalogQuery_ListModels", listModelsMs, "ms", category: "FirstCatalogQuery");
        PerformanceCollector.Track(
            "FirstCatalogQuery_Total",
            totalSw.ElapsedMilliseconds,
            "ms",
            new() { { "model_count", models.Count.ToString(CultureInfo.InvariantCulture) } },
            "FirstCatalogQuery");

        PerformanceCollector.Save();
    }

    /// <summary>
    /// Measures subsequent catalog queries after initialization (cached/warm path).
    /// Runs 100 iterations in a single process.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    [TestCategory("SubsequentCatalogQuery")]
    public async Task BenchmarkSubsequentCatalogQuery()
    {
        // Ensure initialized
        if (!FoundryLocalManager.IsInitialized)
        {
            var config = new Configuration
            {
                AppName = "AIDevGalleryPerfBenchmark",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
                ModelCacheDir = _testCacheDir!
            };

            try
            {
                await FoundryLocalManager.CreateAsync(config, NullLogger.Instance);
            }
            catch (FoundryLocalException) when (FoundryLocalManager.IsInitialized)
            {
            }
        }

        if (!FoundryLocalManager.IsInitialized)
        {
            Assert.Inconclusive("Foundry Local SDK did not initialize on this platform.");
        }

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();

        // Warm-up call (not measured)
        await catalog.ListModelsAsync();

        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var models = await catalog.ListModelsAsync();
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
        }

        PerformanceCollector.Save();
    }
}