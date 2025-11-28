// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AIDevGallery.Tests.TestInfra;

public class PerformanceReport
{
    public Metadata Meta { get; set; } = new();
    public EnvironmentInfo Environment { get; set; } = new();
    public List<Measurement> Measurements { get; set; } = new();
}

public class Metadata
{
    public string SchemaVersion { get; set; } = "1.0";
    public string RunId { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Trigger { get; set; } = string.Empty;
}

public class EnvironmentInfo
{
    public string OS { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
    public HardwareInfo Hardware { get; set; } = new();
}

public class HardwareInfo
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Gpu { get; set; } = string.Empty;
}

public class Measurement
{
    public string Category { get; set; } = "General";
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Performance metrics collector for tracking timing and memory usage during tests.
/// 
/// Usage examples:
/// 
/// 1. Manual timing with Stopwatch:
/// <code>
///   var sw = Stopwatch.StartNew();
///   // ... perform operation ...
///   sw.Stop();
///   PerformanceCollector.Track("OperationTime", sw.ElapsedMilliseconds, "ms");
/// </code>
/// 
/// 2. Automatic timing with using statement:
/// <code>
///   using (PerformanceCollector.BeginTiming("OperationTime"))
///   {
///       // ... perform operation ...
///   } // Time automatically recorded here
/// </code>
/// 
/// 3. Memory tracking:
/// <code>
///   PerformanceCollector.TrackMemoryUsage(processId, "MemoryUsage_Startup");
///   PerformanceCollector.TrackCurrentProcessMemory("MemoryUsage_Current");
/// </code>
/// </summary>
public static class PerformanceCollector
{
    private static readonly List<Measurement> _measurements = new();
    private static readonly object _lock = new();

    public static void Track(string name, double value, string unit, Dictionary<string, string>? tags = null, string category = "General")
    {
        lock (_lock)
        {
            _measurements.Add(new Measurement
            {
                Category = category,
                Name = name,
                Value = value,
                Unit = unit,
                Tags = tags
            });
        }
    }

    public static string Save(string? outputDirectory = null)
    {
        List<Measurement> measurementsSnapshot;
        lock (_lock)
        {
            measurementsSnapshot = new List<Measurement>(_measurements);
        }

        var report = new PerformanceReport
        {
            Meta = new Metadata
            {
                // Support both GitHub Actions and Azure Pipelines variables
                RunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local-run",
                CommitHash = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION") ?? "local-sha",
                Branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME") ?? "local-branch",
                Timestamp = DateTime.UtcNow,
                Trigger = Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") ?? Environment.GetEnvironmentVariable("BUILD_REASON") ?? "manual"
            },
            Environment = new EnvironmentInfo
            {
                OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Platform = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
#if DEBUG
                Configuration = "Debug",
#else
                Configuration = "Release",
#endif
                Hardware = GetHardwareInfo()
            },
            Measurements = measurementsSnapshot
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(report, options);

        // Allow overriding output directory via environment variable (useful for CI)
        string? envOutputDir = Environment.GetEnvironmentVariable("PERFORMANCE_OUTPUT_PATH");
        string dir = outputDirectory ?? envOutputDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerfResults");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string filename = $"perf-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid()}.json";
        string filePath = Path.Combine(dir, filename);

        File.WriteAllText(filePath, json);
        Console.WriteLine($"Performance metrics saved to: {filePath}");

        return filePath;
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _measurements.Clear();
        }
    }

    /// <summary>
    /// Tracks memory usage for a specific process.
    /// </summary>
    /// <param name="processId">The process ID to measure.</param>
    /// <param name="metricName">The name of the metric (e.g., "MemoryUsage_Startup").</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="category">The category for this metric (default: "Memory").</param>
    /// <returns>True if successful, false if measurement failed.</returns>
    public static bool TrackMemoryUsage(int processId, string metricName, Dictionary<string, string>? tags = null, string category = "Memory")
    {
        try
        {
            Console.WriteLine($"Attempting to measure memory for process ID: {processId}");
            var process = Process.GetProcessById(processId);
            
            // Refresh process info to get latest memory values
            process.Refresh();
            
            var memoryMB = process.PrivateMemorySize64 / 1024.0 / 1024.0;
            var workingSetMB = process.WorkingSet64 / 1024.0 / 1024.0;

            Track(metricName, memoryMB, "MB", tags, category);
            Console.WriteLine($"{metricName}: {memoryMB:F2} MB (Private), {workingSetMB:F2} MB (Working Set)");
            
            // Also track working set as a separate metric
            Track($"{metricName}_WorkingSet", workingSetMB, "MB", tags, category);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Could not measure memory for process {processId}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            Console.WriteLine($"Exception message: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Tracks memory usage for the current process.
    /// </summary>
    /// <param name="metricName">The name of the metric (e.g., "MemoryUsage_Current").</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="category">The category for this metric (default: "Memory").</param>
    /// <returns>True if successful, false if measurement failed.</returns>
    public static bool TrackCurrentProcessMemory(string metricName, Dictionary<string, string>? tags = null, string category = "Memory")
    {
        return TrackMemoryUsage(Process.GetCurrentProcess().Id, metricName, tags, category);
    }

    /// <summary>
    /// Creates a timing scope that automatically tracks elapsed time when disposed.
    /// Use with 'using' statement for automatic timing.
    /// </summary>
    /// <param name="metricName">The name of the metric to track.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="category">The category for this metric (default: "Timing").</param>
    /// <returns>A disposable timing scope.</returns>
    public static IDisposable BeginTiming(string metricName, Dictionary<string, string>? tags = null, string category = "Timing")
    {
        return new TimingScope(metricName, tags, category);
    }

    private class TimingScope : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly string _metricName;
        private readonly Dictionary<string, string>? _tags;
        private readonly string _category;

        public TimingScope(string metricName, Dictionary<string, string>? tags, string category)
        {
            _metricName = metricName;
            _tags = tags;
            _category = category;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Track(_metricName, _stopwatch.ElapsedMilliseconds, "ms", _tags, _category);
            Console.WriteLine($"{_metricName}: {_stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private static HardwareInfo GetHardwareInfo()
    {
        var info = new HardwareInfo();

        try
        {
            // Basic CPU info from environment if WMI fails or on non-Windows
            info.Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

            // On Windows, we can try to get more details via WMI (System.Management)
            // Note: This requires the System.Management NuGet package and Windows OS
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                try
                {
                    // Simple memory check
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    long totalMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
                    info.Ram = $"{totalMemoryBytes / (1024 * 1024 * 1024)} GB";
                }
                catch
                { /* Ignore hardware detection errors */
                }
            }
        }
        catch
        {
            // Fallback defaults
            info.Cpu = "Unknown";
            info.Ram = "Unknown";
        }

        return info;
    }
}