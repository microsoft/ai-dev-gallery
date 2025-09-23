// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests;

#pragma warning disable MVVMTK0045 // Using [ObservableProperty] on fields is not AOT compatible for WinRT
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1708 // Identifiers should differ by more than case
public partial class SampleUIData : ObservableObject
{
    internal static SolidColorBrush? graySolidColorBrush;
    internal static SolidColorBrush? greenSolidColorBrush;
    internal static SolidColorBrush? redSolidColorBrush;
    internal static SolidColorBrush? yellowSolidColorBrush;

    public string SampleName { get; }
    internal Sample Sample { get; }
    internal Dictionary<ModelType, ExpandedModelDetails> CachedModelsToGenerator { get; }

    private static int currentId;

    internal SampleUIData(string sampleName, Sample sample, Dictionary<ModelType, ExpandedModelDetails> cachedModelsToGenerator)
    {
        Id = Interlocked.Increment(ref currentId);
        SampleName = sampleName;
        Sample = sample;
        CachedModelsToGenerator = cachedModelsToGenerator;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenBuildFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenLogsFolderCommand))]
    public Brush? statusColor;

    public int Id { get; internal set; }
    public string? ProjectPath { get; internal set; }

    public string GetLogFileName()
    {
        return $"build_{Id}_{SampleName.Replace(' ', '_')}.log";
    }

    public bool CompilationStarted => StatusColor == yellowSolidColorBrush
                || StatusColor == greenSolidColorBrush
                || StatusColor == redSolidColorBrush;

    [RelayCommand(CanExecute = nameof(CompilationStarted))]
    private void OpenBuildFolder()
    {
        if (ProjectPath == null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", ProjectPath) { UseShellExecute = true });
    }

    public bool CompilationDone => StatusColor == greenSolidColorBrush || StatusColor == redSolidColorBrush;

    [RelayCommand(CanExecute = nameof(CompilationDone))]
    private void OpenLogsFolder()
    {
        string logFileName = GetLogFileName();
        var logFilePath = Path.Combine(ProjectGenerator.TmpPathLogs, logFileName);
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            Arguments = $"\"{logFilePath}\""
        });
    }

    public override string ToString()
    {
        return SampleName;
    }
}
#pragma warning restore CA1708 // Identifiers should differ by more than case
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#pragma warning restore MVVMTK0045 // Using [ObservableProperty] on fields is not AOT compatible for WinRT

[TestClass]
public class ProjectGenerator
{
    private readonly Generator generator = new();
    private static readonly string TmpPath = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests");
    private static readonly string TmpPathProjectGenerator = Path.Combine(TmpPath, "ProjectGenerator");
    internal static readonly string TmpPathLogs = Path.Combine(TmpPath, "Logs");

    public TestContext TestContext { get; set; } = null!;

    private static List<SampleUIData> Source { get; set; } = null!;
    private static ListView listView = null!;

    [ClassInitialize]
    public static async Task Initialize(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Directory.Exists(TmpPath))
        {
            Directory.Delete(TmpPath, true);
        }

        if (Directory.Exists(TmpPathProjectGenerator))
        {
            Directory.Delete(TmpPathProjectGenerator, true);
        }

        if (Directory.Exists(TmpPathLogs))
        {
            Directory.Delete(TmpPathLogs, true);
        }

        Directory.CreateDirectory(TmpPath);
        Directory.CreateDirectory(TmpPathProjectGenerator);
        Directory.CreateDirectory(TmpPathLogs);

        TaskCompletionSource taskCompletionSource = new();

        UITestMethodAttribute.DispatcherQueue?.TryEnqueue(() =>
        {
            SampleUIData.greenSolidColorBrush = new(Colors.Green);
            SampleUIData.redSolidColorBrush = new(Colors.Red);
            SampleUIData.yellowSolidColorBrush = new(Colors.Yellow);
            SampleUIData.graySolidColorBrush = new(Colors.LightGray);

            Source = SampleDetails.Samples.SelectMany(s => GetAllForSample(s)).ToList();

            listView = new ListView
            {
                ItemsSource = Source,
                ItemTemplate = Microsoft.UI.Xaml.Application.Current.Resources["SampleItemTemplate"] as Microsoft.UI.Xaml.DataTemplate
            };
            UnitTestApp.SetWindowContent(listView);

            taskCompletionSource.SetResult();
        });

        await taskCompletionSource.Task;

        context.WriteLine($"Running {Source.Count} tests");
    }

    public static IEnumerable<SampleGroup> BatchSource()
    {
        var batchSize = 4;
        var nextbatch = new SampleGroup(batchSize);
        foreach (SampleUIData item in Source)
        {
            nextbatch.Add(item);
            if (nextbatch.Count == batchSize)
            {
                yield return nextbatch;
                nextbatch = new SampleGroup();
            }
        }

        if (nextbatch.Count > 0)
        {
            yield return nextbatch;
        }
    }

    public class SampleGroup : List<SampleUIData>
    {
        public SampleGroup()
        {
        }

        public SampleGroup(int capacity)
            : base(capacity)
        {
        }

        public override string ToString()
        {
            return string.Join(", ", this.Select(i => i.SampleName));
        }
    }

    [TestMethod]
    [DynamicData(nameof(BatchSource), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public async Task GenerateForAllSamples(SampleGroup items)
    {
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (item, ct) =>
        {
            await GenerateForSampleUI(item, ct);
        });
    }

    public async Task GenerateForSampleUI(SampleUIData item, CancellationToken ct)
    {
        listView.DispatcherQueue.TryEnqueue(() =>
        {
            item.StatusColor = SampleUIData.yellowSolidColorBrush;
        });

        var success = await GenerateForSample(item, ct);

        TestContext.WriteLine($"Built {item.SampleName} with status {success}");
        Debug.WriteLine($"Built {item.SampleName} with status {success}");

        listView.DispatcherQueue.TryEnqueue(() =>
        {
            item.StatusColor = success ? SampleUIData.greenSolidColorBrush : SampleUIData.redSolidColorBrush;
        });

        Assert.IsTrue(success, $"{item.SampleName} should build successfully");
    }

    private static IEnumerable<SampleUIData> GetAllForSample(Sample s)
    {
        var modelsDetails = ModelDetailsHelper.GetModelDetails(s);

        if (modelsDetails[0].ContainsKey(ModelType.LanguageModels) &&
            modelsDetails[0].ContainsKey(ModelType.PhiSilica))
        {
            yield return new SampleUIData(
                $"{s.Name} GenAI",
                s,
                GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First(md => md.Key == ModelType.LanguageModels)))
            {
                StatusColor = SampleUIData.graySolidColorBrush
            };

            yield return new SampleUIData(
                $"{s.Name} PhiSilica",
                s,
                GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First(md => md.Key == ModelType.PhiSilica)))
            {
                StatusColor = SampleUIData.graySolidColorBrush
            };
        }
        else
        {
            yield return new SampleUIData(
                s.Name,
                s,
                GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First()))
            {
                StatusColor = SampleUIData.graySolidColorBrush
            };
        }

        static Dictionary<ModelType, ExpandedModelDetails> GetModelsToGenerator(Sample s, List<Dictionary<ModelType, List<ModelDetails>>> modelsDetails, KeyValuePair<ModelType, List<ModelDetails>> keyValuePair)
        {
            Dictionary<ModelType, ExpandedModelDetails> cachedModelsToGenerator = new();

            ModelDetails modelDetails1 = keyValuePair.Value.First();
            cachedModelsToGenerator[keyValuePair.Key] = new(modelDetails1.Id, modelDetails1.Url, modelDetails1.Url, 0, modelDetails1.HardwareAccelerators.First());

            if (s.Model2Types != null && modelsDetails.Count > 1)
            {
                ModelDetails modelDetails2 = modelsDetails[1].Values.First().First();
                cachedModelsToGenerator[s.Model2Types.First()] = new(modelDetails2.Id, modelDetails2.Url, modelDetails2.Url, 0, modelDetails2.HardwareAccelerators.First());
            }

            return cachedModelsToGenerator;
        }
    }

    private async Task<bool> GenerateForSample(SampleUIData sampleUIData, CancellationToken cancellationToken)
    {
        var outputPath = Path.Join(TmpPathProjectGenerator, sampleUIData.Id.ToString(CultureInfo.InvariantCulture));
        var projectPath = await generator.GenerateAsync(sampleUIData.Sample, sampleUIData.CachedModelsToGenerator, false, outputPath, cancellationToken);

        var safeProjectName = Path.GetFileName(projectPath);
        string logFileName = sampleUIData.GetLogFileName();
        sampleUIData.ProjectPath = projectPath;

        var arch = DeviceUtils.IsArm64() ? "arm64" : "x64";

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = @"C:\Program Files\dotnet\dotnet",
            WorkingDirectory = projectPath,
            Arguments = $"build {safeProjectName}.csproj -r win-{arch} -f {Generator.DotNetVersion}-windows10.0.26100.0 /p:Configuration=Release /p:Platform={arch} /flp:logfile={logFileName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null)
        {
            return false;
        }

        var console = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        var logFilePath = Path.Combine(TmpPathLogs, logFileName);
        File.Move(Path.Combine(projectPath, logFileName), logFilePath, true);

        TestContext.AddResultFile(logFilePath);

        if (process.ExitCode != 0)
        {
            Debug.Write(console);
            Debug.WriteLine(string.Empty);
            Debug.Write(error);
            Debug.WriteLine(string.Empty);
        }

        return process.ExitCode == 0;
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void Cleanup()
    {
        Directory.Delete(TmpPathProjectGenerator, true);
    }
}