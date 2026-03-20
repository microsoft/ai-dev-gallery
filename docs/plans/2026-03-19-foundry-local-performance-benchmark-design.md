# Foundry Local SDK vs CLI Performance Benchmark Design

## Goal

Produce a data-driven performance comparison between the old Foundry Local CLI (HTTP API) approach and the new Foundry Local SDK (in-process) approach, to demonstrate to LT that the SDK migration delivers measurable improvements.

## Architecture

Two branches, same test code, same GitHub Actions workflow:

```
branch: perf/foundry-local-benchmark-cli   →  Old code (HTTP API) + Foundry CLI install + perf tests
branch: perf/foundry-local-benchmark-sdk   →  Current code (SDK) + perf tests
```

Both branches share the same `FoundryLocalPerformanceBenchmark.cs` test file. The difference is which `FoundryClient` implementation is called underneath:
- **CLI branch**: Restored `FoundryClient` (HTTP), `FoundryServiceManager`, `Utils` from git history (commit `ba748417^`)
- **SDK branch**: Current `FoundryClient` (SDK) as-is

## Test Cases (Integration Tests, no UI)

| Test | What it measures | CLI path | SDK path |
|------|-----------------|----------|----------|
| **Initialization** | Time from zero to client ready | `FoundryServiceManager.TryCreate()` + `StartService()` + `GetServiceUrl()` + `HttpClient` setup | `FoundryLocalManager.CreateAsync()` + `EnsureEpsDownloadedAsync()` + `GetCatalogAsync()` |
| **CatalogQuery** | Time to list all available models | `GET /foundry/list` → JSON deserialize | `catalog.ListModelsAsync()` |
| **ModelDownload** | Time to download smallest catalog model | `POST /openai/download` → SSE stream + regex progress | `model.DownloadAsync(progress)` |
| **CachedModelQuery** | Time to list downloaded models | `GET /openai/models` → parse JSON array | `catalog.GetCachedModelsAsync()` |

Each test runs **5 iterations**. Metrics collected per iteration:
- Elapsed time (ms) via `PerformanceCollector.BeginTiming()`
- Memory usage (MB) via `PerformanceCollector.TrackCurrentProcessMemory()`
- Success/failure status

## Test File

New file: `AIDevGallery.Tests/IntegrationTests/FoundryLocalPerformanceBenchmark.cs`

```csharp
[TestClass]
[TestCategory("PerformanceBenchmark")]
public class FoundryLocalPerformanceBenchmark
{
    private const int Iterations = 5;

    [TestMethod]
    public async Task Benchmark_Initialization()
    {
        for (int i = 0; i < Iterations; i++)
        {
            // Teardown client to force re-initialization each iteration
            using (PerformanceCollector.BeginTiming($"Initialization_Run{i}", category: "Initialization"))
            {
                var client = await FoundryClient.CreateAsync();
                Assert.IsNotNull(client);
            }
            PerformanceCollector.TrackCurrentProcessMemory($"Initialization_Memory_Run{i}", category: "Initialization");
        }
    }

    [TestMethod]
    public async Task Benchmark_CatalogQuery()
    {
        // Initialize once, then measure catalog queries
        var client = await FoundryClient.CreateAsync();
        Assert.IsNotNull(client);

        for (int i = 0; i < Iterations; i++)
        {
            using (PerformanceCollector.BeginTiming($"CatalogQuery_Run{i}", category: "CatalogQuery"))
            {
                // CLI: client.ListCatalogModels()
                // SDK: client.Catalog.ListModelsAsync()
                // (implementation differs per branch)
            }
        }
    }

    [TestMethod]
    public async Task Benchmark_ModelDownload()
    {
        var client = await FoundryClient.CreateAsync();
        Assert.IsNotNull(client);

        // Pick the smallest model from catalog
        // Clear cache before each run to ensure fresh download

        for (int i = 0; i < Iterations; i++)
        {
            // Clear model cache
            using (PerformanceCollector.BeginTiming($"ModelDownload_Run{i}", category: "ModelDownload"))
            {
                var result = await client.DownloadModel(smallestModel, progress, CancellationToken.None);
                Assert.IsTrue(result.Success);
            }
            PerformanceCollector.TrackCurrentProcessMemory($"ModelDownload_Memory_Run{i}", category: "ModelDownload");
            // Delete model to reset for next iteration
        }
    }

    [TestMethod]
    public async Task Benchmark_CachedModelQuery()
    {
        // Requires at least one model downloaded (run after ModelDownload)
        var client = await FoundryClient.CreateAsync();
        Assert.IsNotNull(client);

        for (int i = 0; i < Iterations; i++)
        {
            using (PerformanceCollector.BeginTiming($"CachedModelQuery_Run{i}", category: "CachedModelQuery"))
            {
                // CLI: client.ListCachedModels()
                // SDK: client.Catalog.GetCachedModelsAsync()
            }
        }
    }

    [ClassCleanup]
    public static void SaveResults()
    {
        PerformanceCollector.Save();
    }
}
```

## GitHub Actions Workflow

New file: `.github/workflows/foundry-local-benchmark.yml`

```yaml
name: Foundry Local Performance Benchmark

on:
  workflow_dispatch:

jobs:
  benchmark:
    runs-on: windows-2025
    timeout-minutes: 30
    permissions:
      contents: read
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 9.0.x

    - name: Add msbuild to PATH
      uses: microsoft/setup-msbuild@v2

    # CLI branch only: Install Foundry Local CLI
    - name: Install Foundry Local CLI
      if: contains(github.ref, 'benchmark-cli')
      shell: pwsh
      run: |
        winget install Microsoft.FoundryLocal --accept-source-agreements --accept-package-agreements
        # Verify installation
        foundry --version

    - name: Cache NuGet packages
      uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: ${{ runner.os }}-x64-nuget-${{ hashFiles('**/packages.lock.json', 'Directory.Packages.props', '**/*.csproj') }}
        restore-keys: |
          ${{ runner.os }}-x64-nuget-

    - name: Restore dependencies
      run: dotnet restore AIDevGallery.sln -r win-x64 /p:Configuration=Release /p:Platform=x64

    - name: Build AIDevGallery.Utils
      run: dotnet build AIDevGallery.Utils --no-restore /p:Configuration=Release

    - name: Build Test Project
      run: dotnet build AIDevGallery.Tests -r win-x64 -f net9.0-windows10.0.26100.0 /p:Configuration=Release /p:Platform=x64

    - name: Run Performance Benchmarks
      shell: pwsh
      run: |
        New-Item -ItemType Directory -Force -Path "${{ github.workspace }}\TestResults" | Out-Null
        New-Item -ItemType Directory -Force -Path "${{ github.workspace }}\PerfResults" | Out-Null
        $env:PERFORMANCE_OUTPUT_PATH = "${{ github.workspace }}\PerfResults"
        dotnet test AIDevGallery.Tests\bin\x64\Release\net9.0-windows10.0.26100.0\win-x64\AIDevGallery.Tests.dll `
          --filter "TestCategory=PerformanceBenchmark" `
          --logger "trx;LogFileName=${{ github.workspace }}\TestResults\PerfBenchmarkResults.trx" `
          --logger "console;verbosity=detailed" `
          --results-directory "${{ github.workspace }}\TestResults" `
          --verbosity normal

    - name: Upload Performance Results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: perf-results-${{ github.ref_name }}
        path: |
          PerfResults/
          TestResults/

    - name: Display Test Report
      if: always()
      uses: dorny/test-reporter@v2
      with:
        name: Performance Benchmark - ${{ github.ref_name }}
        path: 'TestResults/*.trx'
        reporter: dotnet-trx
        fail-on-error: false
```

## Branch Setup

### perf/foundry-local-benchmark-sdk (from current main)
1. Create branch from `main`
2. Add `FoundryLocalPerformanceBenchmark.cs` (SDK version)
3. Add `foundry-local-benchmark.yml` workflow
4. Push

### perf/foundry-local-benchmark-cli (from pre-migration)
1. Create branch from `main`
2. Restore deleted files from `ba748417^`:
   - `FoundryServiceManager.cs`
   - `FoundryJsonContext.cs`
   - `Utils.cs`
3. Restore old `FoundryClient.cs` (HTTP version)
4. Restore old `FoundryLocalModelProvider.cs` (HTTP version)
5. Add `FoundryLocalPerformanceBenchmark.cs` (CLI version — same tests, calling old client API)
6. Add `foundry-local-benchmark.yml` workflow (with CLI install step)
7. May need to restore old NuGet dependencies (`System.ClientModel`, `OpenAI` packages) and remove SDK package
8. Push

## Execution Plan

1. Push both branches
2. Manually trigger `workflow_dispatch` on each branch
3. Download `perf-results-*` artifacts from both runs
4. Compare the JSON reports side by side
5. Produce a summary table/chart for LT

## Expected Output Format (from PerformanceCollector)

```json
{
  "Meta": {
    "SchemaVersion": "1.0",
    "RunId": "12345",
    "CommitHash": "abc123",
    "Branch": "perf/foundry-local-benchmark-sdk",
    "Timestamp": "2026-03-20T10:00:00Z"
  },
  "Environment": {
    "OS": "Microsoft Windows 10.0.26100",
    "Platform": "X64",
    "Configuration": "Release",
    "Hardware": { "Cpu": "...", "Ram": "...", "Gpu": "..." }
  },
  "Measurements": [
    { "Category": "Initialization", "Name": "Initialization_Run0", "Value": 1234, "Unit": "ms" },
    { "Category": "Initialization", "Name": "Initialization_Memory_Run0", "Value": 150.5, "Unit": "MB" },
    ...
  ]
}
```

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| `winget` fails on runner | Fallback: download MSIX from GitHub releases + `Add-AppxPackage` |
| CLI branch build breaks (dependency conflicts) | May need to pin old package versions; keep changes minimal |
| Model download time dominated by network (masks protocol overhead) | Focus on Initialization + CatalogQuery for clear architectural wins; use smallest model for download |
| Runner hardware variance between runs | Both runs use same runner type (`windows-2025`); include hardware info in report |
| Foundry Local SDK unavailable on runner | Tests use `Assert.Inconclusive` pattern from existing integration tests |
