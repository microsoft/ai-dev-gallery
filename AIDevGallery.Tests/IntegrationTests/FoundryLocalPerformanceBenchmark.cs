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
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

/// <summary>
/// Performance benchmark tests for Foundry Local SDK operations.
/// Run on both perf/foundry-local-benchmark-sdk and perf/foundry-local-benchmark-cli branches
/// to compare SDK vs CLI performance.
/// </summary>
[TestClass]
[TestCategory("PerformanceBenchmark")]
public class FoundryLocalPerformanceBenchmark
{
    private const int Iterations = 5;
    private static string? _testCacheDir;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "AIDevGalleryPerfBenchmark", "foundrycache");
        Directory.CreateDirectory(_testCacheDir);
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
    }

    private static void EnsureSdkAvailable()
    {
        if (!FoundryLocalManager.IsInitialized)
        {
            Assert.Inconclusive("Foundry Local SDK did not initialize on this platform.");
        }
    }

    /// <summary>
    /// Measures the time to initialize FoundryLocalManager from scratch.
    /// SDK: FoundryLocalManager.CreateAsync() + EnsureEpsDownloadedAsync() + GetCatalogAsync().
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    public async Task BenchmarkInitialization()
    {
        for (int i = 0; i < Iterations; i++)
        {
            // For the first run, we initialize from scratch.
            // For subsequent runs, the SDK singleton is already initialized,
            // so we measure the "already initialized" fast path + GetCatalogAsync.
            var sw = Stopwatch.StartNew();

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
                    // Race condition: already initialized
                }
            }

            var manager = FoundryLocalManager.Instance;

            try
            {
                await manager.EnsureEpsDownloadedAsync();
            }
            catch (Exception epEx)
            {
                Debug.WriteLine($"[PerfBenchmark] EP registration issue (non-fatal): {epEx.Message}");
            }

            var catalog = await manager.GetCatalogAsync();
            Assert.IsNotNull(catalog, "Catalog should not be null after initialization");

            sw.Stop();

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

        EnsureSdkAvailable();

        PerformanceCollector.Save();
    }

    /// <summary>
    /// Measures the time to query the full model catalog.
    /// SDK: catalog.ListModelsAsync().
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    public async Task BenchmarkCatalogQuery()
    {
        await EnsureInitializedAsync();
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        Assert.IsNotNull(catalog);

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();

            var models = await catalog.ListModelsAsync();

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
    /// Measures the time to download the smallest model in the catalog.
    /// SDK: model.DownloadAsync().
    /// Clears cache before each iteration to ensure a fresh download.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    public async Task BenchmarkModelDownload()
    {
        await EnsureInitializedAsync();
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();
        Assert.IsTrue(models.Count > 0, "Need at least one model to benchmark download");

        // Find the smallest model by file size
        IModel? smallestModel = null;
        long smallestSize = long.MaxValue;

        foreach (var model in models)
        {
            var size = model.SelectedVariant.Info.FileSizeMb ?? long.MaxValue;
            if (size < smallestSize && size > 0)
            {
                smallestSize = size;
                smallestModel = model;
            }
        }

        Assert.IsNotNull(smallestModel, "Could not find a model with valid file size");
        var alias = smallestModel.Alias;

        Console.WriteLine($"[ModelDownload] Selected model: {alias} ({smallestSize} MB)");

        for (int i = 0; i < Iterations; i++)
        {
            // Clear cache for this model before each run
            try
            {
                if (await smallestModel.IsCachedAsync())
                {
                    if (await smallestModel.IsLoadedAsync())
                    {
                        await smallestModel.UnloadAsync();
                    }

                    await smallestModel.RemoveFromCacheAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModelDownload] Cache clear warning: {ex.Message}");
            }

            var progress = new Progress<float>();

            var sw = Stopwatch.StartNew();

            await smallestModel.DownloadAsync(
                progressPercent => ((IProgress<float>)progress).Report(progressPercent / 100f),
                CancellationToken.None);

            sw.Stop();

            PerformanceCollector.Track(
                $"ModelDownload_Run{i}",
                sw.ElapsedMilliseconds,
                "ms",
                new()
                {
                    { "iteration", i.ToString(CultureInfo.InvariantCulture) },
                    { "model_alias", alias },
                    { "model_size_mb", smallestSize.ToString(CultureInfo.InvariantCulture) }
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
    /// Measures the time to query cached (downloaded) models.
    /// SDK: catalog.GetCachedModelsAsync().
    /// Requires at least one model to be downloaded (run after BenchmarkModelDownload).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [TestMethod]
    public async Task BenchmarkCachedModelQuery()
    {
        await EnsureInitializedAsync();
        EnsureSdkAvailable();

        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync();

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();

            var cachedModels = await catalog.GetCachedModelsAsync();

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

    private static async Task EnsureInitializedAsync()
    {
        if (FoundryLocalManager.IsInitialized)
        {
            return;
        }

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
            // Already initialized
        }
    }
}