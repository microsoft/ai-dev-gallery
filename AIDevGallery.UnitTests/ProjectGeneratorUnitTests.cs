// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using FluentAssertions;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.UnitTests;

[TestClass]
public class ProjectGenerator
{
    private readonly Generator generator = new();
    private static readonly string TmpPath = Path.Combine(Path.GetTempPath(), "AIDevGalleryTests");
    private static readonly string TmpPathProjectGenerator = Path.Combine(TmpPath, "ProjectGenerator");
    private static readonly string TmpPathLogs = Path.Combine(TmpPath, "Logs");

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

    private class SampleUIData : INotifyPropertyChanged
    {
        private Brush? statusColor;

        public required string SampleName { get; init; }
        public required Sample Sample { get; init; }
        public required Dictionary<ModelType, (string CachedModelDirectoryPath, string ModelUrl, HardwareAccelerator HardwareAccelerator)> CachedModelsToGenerator { get; init; }
        public Brush? StatusColor
        {
            get => statusColor;
            set => SetProperty(ref statusColor, value);
        }

        private void SetProperty(ref Brush? field, Brush? value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [TestMethod]
    public async Task GenerateForAllSamples()
    {
        List<SampleUIData> source = null!;
        ListView listView = null!;
        SolidColorBrush green = null!;
        SolidColorBrush red = null!;
        SolidColorBrush yellow = null!;
        TaskCompletionSource taskCompletionSource = new();

        UITestMethodAttribute.DispatcherQueue?.TryEnqueue(() =>
        {
            source = SampleDetails.Samples.SelectMany(s => GetAllForSample(s)).ToList();

            green = new SolidColorBrush(Colors.Green);
            red = new SolidColorBrush(Colors.Red);
            yellow = new SolidColorBrush(Colors.Yellow);

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

        await Parallel.ForEachAsync(source, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (item, ct) =>
        {
            listView.DispatcherQueue.TryEnqueue(() =>
            {
                item.StatusColor = yellow;
            });

            var success = await GenerateForSample(item, ct);

            TestContext.WriteLine($"Built {item.SampleName} with status {success}");
            Debug.WriteLine($"Built {item.SampleName} with status {success}");

            listView.DispatcherQueue.TryEnqueue(() =>
            {
                item.StatusColor = success ? green : red;
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
                StatusColor = new SolidColorBrush(Colors.LightGray)
            };

            yield return new SampleUIData
            {
                Sample = s,
                SampleName = $"{s.Name} PhiSilica",
                CachedModelsToGenerator = GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First(md => md.Key == ModelType.PhiSilica)),
                StatusColor = new SolidColorBrush(Colors.LightGray)
            };
        }
        else
        {
            yield return new SampleUIData
            {
                Sample = s,
                SampleName = s.Name,
                CachedModelsToGenerator = GetModelsToGenerator(s, modelsDetails, modelsDetails[0].First()),
                StatusColor = new SolidColorBrush(Colors.LightGray)
            };
        }

        static Dictionary<ModelType, (string CachedModelDirectoryPath, string ModelUrl, HardwareAccelerator HardwareAccelerator)> GetModelsToGenerator(Sample s, List<Dictionary<ModelType, List<ModelDetails>>> modelsDetails, KeyValuePair<ModelType, List<ModelDetails>> keyValuePair)
        {
            Dictionary<ModelType, (string CachedModelDirectoryPath, string ModelUrl, HardwareAccelerator HardwareAccelerator)> cachedModelsToGenerator = new();

            ModelDetails modelDetails1 = keyValuePair.Value.First();
            cachedModelsToGenerator[keyValuePair.Key] = (modelDetails1.Url, modelDetails1.Url, modelDetails1.HardwareAccelerators.First());

            if (s.Model2Types != null && modelsDetails.Count > 1)
            {
                ModelDetails modelDetails2 = modelsDetails[1].Values.First().First();
                cachedModelsToGenerator[s.Model2Types.First()] = (modelDetails2.Url, modelDetails2.Url, modelDetails2.HardwareAccelerators.First());
            }

            return cachedModelsToGenerator;
        }
    }

    private async Task<bool> GenerateForSample(SampleUIData sampleUIData, CancellationToken cancellationToken)
    {
        var outputPath = Path.Join(TmpPathProjectGenerator, sampleUIData.SampleName);
        var projectPath = await generator.GenerateAsync(sampleUIData.Sample, sampleUIData.CachedModelsToGenerator, false, outputPath, cancellationToken);

        var safeProjectName = Path.GetFileName(projectPath);
        string logFileName = $"build_{safeProjectName}.log";

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