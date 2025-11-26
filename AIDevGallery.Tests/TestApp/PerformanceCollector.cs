using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.Json;

namespace AIDevGallery.Tests.TestApp;

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
                catch { /* Ignore hardware detection errors */ }
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
