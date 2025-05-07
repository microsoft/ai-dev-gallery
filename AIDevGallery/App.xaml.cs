// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets, or initializes, the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    internal static MainWindow MainWindow { get; private set; } = null!;
    internal static ModelCache ModelCache { get; private set; } = null!;
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
        await LoadSamples();
        AppActivationArguments appActivationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var activationParam = ActivationHelper.GetActivationParam(appActivationArguments);
        MainWindow = new MainWindow(activationParam);

        MainWindow.Activate();
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
        GenerateSearchIndex();
        _ = ExternalModelHelper.InitializeAsync();
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