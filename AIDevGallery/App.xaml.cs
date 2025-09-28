// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry;
using AIDevGallery.Services;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private LocalHttpServer? _localHttpServer;
	private TextWriterTraceListener? _fileTraceListener;
    /// <summary>
    /// Gets, or initializes, the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    internal static MainWindow MainWindow { get; private set; } = null!;
    internal static ModelCache ModelCache { get; private set; } = null!;
    internal static ModelDownloadQueue ModelDownloadQueue { get; private set; } = null!;
    internal static AppData AppData { get; private set; } = null!;
    internal static List<SearchResult> SearchIndex { get; private set; } = null!;

    internal App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
		// Initialize file logging for Debug/Trace output
		try
		{
			var logsFolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
			var logsDir = Path.Combine(logsFolderPath, "Logs");
			Directory.CreateDirectory(logsDir);

			// Prune logs older than 24 hours
			var retention = System.TimeSpan.FromHours(24);
			try
			{
				foreach (var file in Directory.EnumerateFiles(logsDir, "AIDevGallery_*.log"))
				{
					var lastWriteUtc = File.GetLastWriteTimeUtc(file);
					if ((System.DateTime.UtcNow - lastWriteUtc) > retention)
					{
						File.Delete(file);
					}
				}
			}
			catch
			{
				// Ignore retention cleanup errors
			}

			// Create a timestamped log file for this session
			var logFileName = $"AIDevGallery_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
			var logPath = Path.Combine(logsDir, logFileName);
			var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			_fileTraceListener = new TextWriterTraceListener(stream, "FileLogger");
			Debug.Listeners.Add(_fileTraceListener);
			Trace.Listeners.Add(_fileTraceListener);
			Debug.AutoFlush = true;
			Trace.AutoFlush = true;
			Debug.WriteLine($"Logging started: {System.DateTime.Now:O}");
		}
		catch
		{
			// If logging initialization fails, continue without blocking app startup
		}

        await LoadSamples();
        try
        {
            _localHttpServer = await LocalHttpServer.StartAsync(System.Threading.CancellationToken.None);
        }
        catch
        {
            // Swallow errors to avoid blocking app startup; server is optional for core UI.
        }
        AppActivationArguments appActivationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var activationParam = await ActivationHelper.GetActivationParam(appActivationArguments);
        MainWindow = new MainWindow(activationParam);

        MainWindow.Activate();

		MainWindow.Closed += async (sender, e) =>
        {
            if (_localHttpServer != null)
            {
                await _localHttpServer.DisposeAsync();
                _localHttpServer = null;
            }

			// Flush and close file logger
			try
			{
				if (_fileTraceListener != null)
				{
					Debug.WriteLine("Logging ended");
					_fileTraceListener.Flush();
					_fileTraceListener.Close();
					Debug.Listeners.Remove(_fileTraceListener);
					Trace.Listeners.Remove(_fileTraceListener);
					_fileTraceListener.Dispose();
					_fileTraceListener = null;
				}
			}
			catch
			{
				// ignore logging disposal errors
			}
        };
    }

    internal static List<ModelType> FindSampleItemById(string id)
    {
        foreach (var sample in SampleDetails.Samples)
        {
            if (sample.Id == id)
            {
                return sample.Model1Types;
            }
        }

        foreach (var modelFamily in ModelTypeHelpers.ModelFamilyDetails)
        {
            if (modelFamily.Value.Id == id)
            {
                return [modelFamily.Key];
            }
        }

        foreach (var modelGroup in ModelTypeHelpers.ModelGroupDetails)
        {
            if (modelGroup.Value.Id == id)
            {
                return [modelGroup.Key];
            }
        }

        foreach (var modelDetails in ModelTypeHelpers.ModelDetails)
        {
            if (modelDetails.Value.Id == id)
            {
                return [modelDetails.Key];
            }
        }

        foreach (var apiDefinition in ModelTypeHelpers.ApiDefinitionDetails)
        {
            if (apiDefinition.Value.Id == id)
            {
                return [apiDefinition.Key];
            }
        }

        return [];
    }

    internal static Scenario? FindScenarioById(string id)
    {
        foreach (var category in ScenarioCategoryHelpers.AllScenarioCategories)
        {
            var foundScenario = category.Scenarios.FirstOrDefault(scenario => scenario.Id == id);
            if (foundScenario != null)
            {
                return foundScenario;
            }
        }

        return null;
    }

    private async Task LoadSamples()
    {
        AppData = await AppData.GetForApp();
        TelemetryFactory.Get<ITelemetry>().IsDiagnosticTelemetryOn = AppData.IsDiagnosticDataEnabled;
        ModelCache = await ModelCache.CreateForApp(AppData);
        ModelDownloadQueue = new ModelDownloadQueue();

        GenerateSearchIndex();
    }

    private void GenerateSearchIndex()
    {
        SearchIndex = [];
        foreach (ScenarioCategory category in ScenarioCategoryHelpers.AllScenarioCategories)
        {
            foreach (Scenario scenario in category.Scenarios)
            {
                SearchIndex.Add(new SearchResult() { Label = scenario.Name, Icon = scenario.Icon!, Description = scenario.Description!, Tag = scenario });
            }
        }

        List<ModelType> rootModels = [.. ModelTypeHelpers.ModelGroupDetails.Keys];
        rootModels.AddRange(ModelTypeHelpers.ModelFamilyDetails.Keys);

        foreach (var key in rootModels)
        {
            if (ModelTypeHelpers.ParentMapping.TryGetValue(key, out List<ModelType>? innerItems))
            {
                if (innerItems?.Count > 0)
                {
                    foreach (var childNavigationItem in innerItems)
                    {
                        if (ModelTypeHelpers.ModelGroupDetails.TryGetValue(childNavigationItem, out var modelGroup))
                        {
                            SearchIndex.Add(new SearchResult() { Label = modelGroup.Name, Icon = modelGroup.Icon, Description = modelGroup.Name!, Tag = childNavigationItem });
                        }
                        else if (ModelTypeHelpers.ModelFamilyDetails.TryGetValue(childNavigationItem, out var modelFamily))
                        {
                            SearchIndex.Add(new SearchResult() { Label = modelFamily.Name, Description = modelFamily.Description, Tag = childNavigationItem });
                        }
                        else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(childNavigationItem, out var apiDefinition))
                        {
                            SearchIndex.Add(new SearchResult() { Label = apiDefinition.Name, Icon = apiDefinition.Icon, Description = apiDefinition.Name!, Tag = childNavigationItem });
                        }
                    }
                }
            }
        }
    }
}