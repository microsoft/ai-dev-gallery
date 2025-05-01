// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal sealed partial class ScenarioPage : Page
{
    private Scenario? scenario;
    private List<Sample>? samples;
    private Sample? sample;
    private ObservableCollection<ModelDetails?> modelDetails = new();

    public ScenarioPage()
    {
        this.InitializeComponent();
        this.Loaded += (s, e) => App.MainWindow.ModelPicker.SelectedModelsChanged += ModelOrApiPicker_SelectedModelsChanged;
        this.Unloaded += (s, e) => App.MainWindow.ModelPicker.SelectedModelsChanged -= ModelOrApiPicker_SelectedModelsChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        VisualStateManager.GoToState(this, "PageLoading", true);
        base.OnNavigatedTo(e);
        _ = LoadPage(e.Parameter);
    }

    private async Task LoadPage(object parameter)
    {
        if (parameter is Scenario scenario)
        {
            this.scenario = scenario;
            await LoadPicker();
        }
        else if (parameter is SampleNavigationArgs sampleArgs)
        {
            this.scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
            await LoadPicker(sampleArgs.ModelDetails);

            if (sampleArgs.OpenCodeView is not null & true)
            {
                CodeToggle.IsChecked = true;
                HandleCodePane();
            }
        }

        samples = SampleDetails.Samples.Where(sample => sample.Scenario == this.scenario!.ScenarioType).ToList();
    }

    private async Task LoadPicker(ModelDetails? initialModelToLoad = null)
    {
        if (scenario == null)
        {
            return;
        }

        samples = [.. SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType)];

        if (samples.Count == 0)
        {
            return;
        }

        List<List<ModelType>> modelDetailsList = [samples.SelectMany(s => s.Model1Types).ToList()];

        // assume if first sample has two models, then all of them should need two models
        if (samples[0].Model2Types != null)
        {
            modelDetailsList.Add(samples.SelectMany(s => s.Model2Types!).ToList());
        }

        var preSelectedModels = await App.MainWindow.ModelPicker.Load(modelDetailsList, initialModelToLoad);
        HandleModelSelectionChanged(preSelectedModels);

        if (preSelectedModels.Contains(null) || preSelectedModels.Count == 0)
        {
            // user needs to select a model if one is not selected at first
            App.MainWindow.ModelPicker.Show(preSelectedModels);
            return;
        }
    }

    private void HandleModelSelectionChanged(List<ModelDetails?> selectedModels)
    {
        if (selectedModels.Contains(null) || selectedModels.Count == 0)
        {
            // user needs to select a model
            VisualStateManager.GoToState(this, "NoModelSelected", true);
            return;
        }

        modelDetails.Clear();
        selectedModels.ForEach(modelDetails.Add);

        if (selectedModels.Count == 1)
        {
            // padd the second model with null
            selectedModels = [selectedModels[0], null];
        }

        List<Sample> viableSamples = samples!.Where(s =>
            IsModelFromTypes(s.Model1Types, selectedModels[0]) &&
            IsModelFromTypes(s.Model2Types, selectedModels[1])).ToList();

        if (viableSamples.Count == 0)
        {
            // this should never happen
            App.MainWindow.ModelPicker.Show(selectedModels);
            return;
        }

        if (viableSamples.Count > 1)
        {
            SampleSelection.Items.Clear();
            foreach (var sample in viableSamples)
            {
                SampleSelection.Items.Add(sample);
            }

            SampleSelection.SelectedItem = viableSamples[0];
            SampleSelection.Visibility = Visibility.Visible;
        }
        else
        {
            SampleSelection.Visibility = Visibility.Collapsed;
            LoadSample(viableSamples[0]);
        }
    }

    private void LoadSample(Sample? sampleToLoad)
    {
        sample = sampleToLoad;

        if (sample == null)
        {
            return;
        }

        VisualStateManager.GoToState(this, "ModelSelected", true);

        // TODO: don't load sample if model is not cached, but still let code to be seen
        //       this would probably be handled in the SampleContainer
        _ = SampleContainer.LoadSampleAsync(sample, [.. modelDetails]);
        _ = App.AppData.AddMru(
            new MostRecentlyUsedItem()
            {
                Type = MostRecentlyUsedItemType.Scenario,
                ItemId = scenario!.Id,
                Icon = scenario.Icon,
                Description = scenario.Description,
                SubItemId = modelDetails[0]!.Id,
                DisplayName = scenario.Name
            },
            modelDetails.Select(m => (m!.Id, m.HardwareAccelerators.First())).ToList());
    }

    private bool IsModelFromTypes(List<ModelType>? types, ModelDetails? model)
    {
        if (types == null && model == null)
        {
            return true;
        }

        if (types == null || model == null)
        {
            return false;
        }

        if (types.Contains(ModelType.LanguageModels) && model.IsLanguageModel())
        {
            return true;
        }

        List<string> modelIds = [];

        foreach (var type in types)
        {
            modelIds.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type).Select(m => m.Id));
            if (App.AppData.TryGetUserAddedModelIds(type, out var ids))
            {
                modelIds.AddRange(ids!);
            }
        }

        return modelIds.Any(id => id == model.Id);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText("aidevgallery://scenarios/" + scenario!.Id);
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void CodeToggle_Click(object sender, RoutedEventArgs args)
    {
        HandleCodePane();
    }

    private void HandleCodePane()
    {
        if (sample != null)
        {
            ToggleCodeButtonEvent.Log(sample.Name ?? string.Empty, CodeToggle.IsChecked == true);
        }

        if (CodeToggle.IsChecked == true)
        {
            SampleContainer.ShowCode();
        }
        else
        {
            SampleContainer.HideCode();
        }
    }

    private void ExportSampleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || sample == null)
        {
            return;
        }

        _ = Generator.AskGenerateAndOpenAsync(sample, modelDetails.Where(m => m != null).Select(m => m!), XamlRoot);
    }

    private void ActionButtonsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate if the modelselectors collide with the export/code buttons
        if ((ModelPanel.ActualWidth + ButtonsPanel.ActualWidth) >= e.NewSize.Width)
        {
            VisualStateManager.GoToState(this, "NarrowLayout", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "WideLayout", true);
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindow.ModelPicker.Show(modelDetails.ToList());
    }

    private void ModelOrApiPicker_SelectedModelsChanged(object sender, List<ModelDetails?> modelDetails)
    {
        HandleModelSelectionChanged(modelDetails);
    }

    private void SampleSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedSample = e.AddedItems
            .OfType<Sample>()
            .ToList().FirstOrDefault();

        if (selectedSample != sample)
        {
            LoadSample(selectedSample);
        }
    }
}