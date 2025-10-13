// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private TextWriterTraceListener? _debugListener;
    private TextWriterTraceListener? _traceListener;
    private StreamWriter? _sharedLogWriter;
    private object _logWriteLock = new();

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
        // Initialize file logging for Debug/Trace output if enabled by user
        try
        {
            AppData = await AppData.GetForApp();
            if (AppData.IsLocalLoggingEnabled)
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
                var baseStream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _sharedLogWriter = new StreamWriter(baseStream) { AutoFlush = true };

                // Create prefixed listeners for Debug and Trace
                var debugWriter = new PrefixedTextWriter(_sharedLogWriter, _logWriteLock, "[Debug] ");
                var traceWriter = new PrefixedTextWriter(_sharedLogWriter, _logWriteLock, "[Trace] ");
                _debugListener = new TextWriterTraceListener(debugWriter, "DebugFileLogger");
                _traceListener = new TextWriterTraceListener(traceWriter, "TraceFileLogger");
                Debug.Listeners.Add(_debugListener);
                Trace.Listeners.Add(_traceListener);
                Debug.AutoFlush = true;
                Trace.AutoFlush = true;

                // Redirect Console to the same writer with prefix
                var consoleWriter = new PrefixedTextWriter(_sharedLogWriter, _logWriteLock, "[Console] ");
                Console.SetOut(consoleWriter);
                Console.SetError(consoleWriter);
                Debug.WriteLine($"Logging started: {System.DateTime.Now:O}");
            }
        }
        catch
        {
            // If logging initialization fails, continue without blocking app startup
        }

        await LoadSamples();
        AppActivationArguments appActivationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var activationParam = await ActivationHelper.GetActivationParam(appActivationArguments);
        MainWindow = new MainWindow(activationParam);

        MainWindow.Activate();

        MainWindow.Closed += (sender, e) =>
        {
            // Flush and close file logger
            try
            {
                if (_debugListener != null)
                {
                    Debug.WriteLine("Logging ended");
                    _debugListener.Flush();
                    _debugListener.Close();
                    Debug.Listeners.Remove(_debugListener);
                    _debugListener.Dispose();
                    _debugListener = null;
                }

                if (_traceListener != null)
                {
                    _traceListener.Flush();
                    _traceListener.Close();
                    Trace.Listeners.Remove(_traceListener);
                    _traceListener.Dispose();
                    _traceListener = null;
                }

                if (_sharedLogWriter != null)
                {
                    _sharedLogWriter.Flush();
                    _sharedLogWriter.Dispose();
                    _sharedLogWriter = null;
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

    private sealed class PrefixedTextWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly object _lock;
        private readonly string _prefix;
        private bool _atLineStart = true;

        public PrefixedTextWriter(TextWriter inner, object @lock, string prefix)
        {
            _inner = inner;
            _lock = @lock;
            _prefix = prefix;
        }

        public override System.Text.Encoding Encoding => _inner.Encoding;

        public override void Write(string? value)
        {
            if (value == null) return;
            lock (_lock)
            {
                WriteWithPrefix(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_lock)
            {
                if (value == null)
                {
                    if (_atLineStart)
                    {
                        _inner.Write(_prefix);
                    }
                    _inner.WriteLine();
                    _atLineStart = true;
                    return;
                }

                WriteWithPrefix(value + Environment.NewLine);
                _atLineStart = true;
            }
        }

        private void WriteWithPrefix(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (_atLineStart)
                {
                    _inner.Write(_prefix);
                    _atLineStart = false;
                }

                char ch = text[i++];
                _inner.Write(ch);
                if (ch == '\n')
                {
                    _atLineStart = true;
                }
                else if (ch == '\r')
                {
                    if (i < text.Length && text[i] == '\n')
                    {
                        _inner.Write(text[i++]);
                    }
                    _atLineStart = true;
                }
            }
        }
    }
}