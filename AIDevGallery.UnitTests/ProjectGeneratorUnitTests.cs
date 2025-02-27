// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
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
internal partial class SampleUIData : ObservableObject
{
    internal static SolidColorBrush? graySolidColorBrush;
    internal static SolidColorBrush? greenSolidColorBrush;
    internal static SolidColorBrush? redSolidColorBrush;
    internal static SolidColorBrush? yellowSolidColorBrush;

    public required string SampleName { get; init; }
    public required Sample Sample { get; init; }
    public required Dictionary<ModelType, ExpandedModelDetails> CachedModelsToGenerator { get; init; }

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
}
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

    [ClassInitialize]
    public static void Initialize(TestContext context)
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
    }

    [TestMethod]
    public async Task GenerateForAllSamples()
    {
        List<SampleUIData> source = null!;
        ListView listView = null!;
        TaskCompletionSource taskCompletionSource = new();

        UITestMethodAttribute.DispatcherQueue?.TryEnqueue(() =>
        {
            SampleUIData.greenSolidColorBrush = new(Colors.Green);
            SampleUIData.redSolidColorBrush = new(Colors.Red);
            SampleUIData.yellowSolidColorBrush = new(Colors.Yellow);
            SampleUIData.graySolidColorBrush = new(Colors.LightGray);

            source = SampleDetails.Samples.SelectMany(s => GetAllForSample(s)).ToList();

            listView = new ListView
            {
                ItemsSource = source,
                ItemTemplate = Microsoft.UI.Xaml.Application.Current.Resources["SampleItemTemplate"] as Microsoft.UI.Xaml.DataTemplate
            };
            UnitTestApp.SetWindowContent(listView);

            taskCompletionSource.SetResult();
        });

        await taskCompletionSource.Task;

        Dictionary<string, bool> successDict = [];

        // write test count
        TestContext.WriteLine($"Running {source.Count} tests");
        int currentId = 0;
        await Parallel.ForEachAsync(source, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (item, ct) =>
        {
            listView.DispatcherQueue.TryEnqueue(() =>
            {
                item.StatusColor = SampleUIData.yellowSolidColorBrush;
            });

            Interlocked.Increment(ref currentId);
            item.Id = currentId;
            var success = await GenerateForSample(item, ct);

            TestContext.WriteLine($"Built {item.SampleName} with status {success}");
            Debug.WriteLine($"Built {item.SampleName} with status {success}");

            listView.DispatcherQueue.TryEnqueue(() =>
            {
                item.StatusColor = success ? SampleUIData.greenSolidColorBrush : SampleUIData.redSolidColorBrush;
            });
            successDict.Add(item.SampleName, success);
        });

        successDict.Should().AllSatisfy(kvp => kvp.Value.Should().BeTrue($"{kvp.Key} should build successfully"));
    }

    private static IEnumerable<SampleUIData> GetAllForSample(Sample s)
    {
        var modelsDetails = ModelDetailsHelper.GetModelDetails(s);

        if (modelsDetails[0].ContainsKey(ModelType.LanguageModels) &&
            modelsDetails[0].ContainsKey(ModelType.PhiSilica))
        {
            yield return new SampleUIData
            {
                Sample = s,
                SampleName = $"{s.Name} GenAI",
                CachedModelsToGenerator = GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First(md => md.Key == ModelType.LanguageModels)),
                StatusColor = SampleUIData.graySolidColorBrush
            };

            yield return new SampleUIData
            {
                Sample = s,
                SampleName = $"{s.Name} PhiSilica",
                CachedModelsToGenerator = GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First(md => md.Key == ModelType.PhiSilica)),
                StatusColor = SampleUIData.graySolidColorBrush
            };
        }
        else
        {
            yield return new SampleUIData
            {
                Sample = s,
                SampleName = s.Name,
                CachedModelsToGenerator = GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First()),
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
            Arguments = $"build {safeProjectName}.csproj -r win-{arch} -f {Generator.DotNetVersion}-windows10.0.22621.0 /p:Configuration=Release /p:Platform={arch} /flp:logfile={logFileName}",
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

    [ClassCleanup]
    public static void Cleanup()
    {
        Directory.Delete(TmpPathProjectGenerator, true);
    }
}