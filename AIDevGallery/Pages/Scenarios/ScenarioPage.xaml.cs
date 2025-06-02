// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Pages;

internal record WinMlEp(List<HardwareAccelerator> HardwareAccelerators, string Name, string ShortName, string DeviceType);

internal sealed partial class ScenarioPage : Page
{
    private readonly Dictionary<string, ExecutionProviderDevicePolicy> executionProviderDevicePolicies = new()
    {
        { "Default", ExecutionProviderDevicePolicy.DEFAULT },
        { "Max Efficency", ExecutionProviderDevicePolicy.MAX_EFFICIENCY },
        { "Max Performance", ExecutionProviderDevicePolicy.MAX_PERFORMANCE },
        { "Minimize Overall Power", ExecutionProviderDevicePolicy.MIN_OVERALL_POWER },
        { "Prefer NPU", ExecutionProviderDevicePolicy.PREFER_NPU },
        { "Prefer GPU", ExecutionProviderDevicePolicy.PREFER_GPU },
        { "Prefer CPU", ExecutionProviderDevicePolicy.PREFER_CPU },
    };

    private Scenario? scenario;
    private List<Sample>? samples;
    private Sample? sample;
    private ObservableCollection<ModelDetails?> modelDetails = new();
    private static List<WinMlEp>? supportedHardwareAccelerators;

    public ScenarioPage()
    {
        this.InitializeComponent();
        this.Loaded += (s, e) =>
        BackgroundShadow.Receivers.Add(ShadowCastGrid);
        App.MainWindow.ModelPicker.SelectedModelsChanged += ModelOrApiPicker_SelectedModelsChanged;
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

            if (sampleArgs.OpenCodeView.HasValue && sampleArgs.OpenCodeView.Value)
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

    private static async Task<List<WinMlEp>> GetSupportedHardwareAccelerators()
    {
        if (supportedHardwareAccelerators != null)
        {
            return supportedHardwareAccelerators;
        }

        OrtEnv.Instance();
        Microsoft.Windows.AI.MachineLearning.Infrastructure infrastructure = new();

        try
        {
            await infrastructure.DownloadPackagesAsync();
        }
        catch (Exception)
        {
        }

        await infrastructure.RegisterExecutionProviderLibrariesAsync();

        supportedHardwareAccelerators = [new([HardwareAccelerator.CPU], "CPU", "CPU", "CPU")];

        foreach (string device in WinMLHelpers.GetEpDeviceMap().Keys)
        {
            switch(device)
            {
                case "VitisAIExecutionProvider":
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.VitisAI, HardwareAccelerator.NPU], "VitisAIExecutionProvider", "VitisAI", "NPU"));
                    break;

                case "OpenVINOExecutionProvider":
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.OpenVINO, HardwareAccelerator.NPU], "OpenVINOExecutionProvider", "OpenVINO", "NPU"));
                    break;

                case "QNNExecutionProvider":
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.QNN, HardwareAccelerator.NPU], "QNNExecutionProvider", "QNN", "NPU"));
                    break;

                case "DmlExecutionProvider":
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.DML, HardwareAccelerator.GPU], "DmlExecutionProvider", "DML", "GPU"));
                    break;

                case "NvTensorRTRTXExecutionProvider":
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.NvTensorRT, HardwareAccelerator.GPU], "NvTensorRTRTXExecutionProvider", "NvTensorRT", "GPU"));
                    break;
            }
        }

        return supportedHardwareAccelerators;
    }

    private async void HandleModelSelectionChanged(List<ModelDetails?> selectedModels)
    {
        if (selectedModels.Contains(null) || selectedModels.Count == 0)
        {
            // user needs to select a model
            VisualStateManager.GoToState(this, "NoModelSelected", true);
            return;
        }

        VisualStateManager.GoToState(this, "PageLoading", true);

        modelDetails.Clear();
        selectedModels.ForEach(modelDetails.Add);

        if (selectedModels.Any(m => m != null && m.IsOnnxModel() && string.IsNullOrEmpty(m.ParameterSize)))
        {
            var delayTask = Task.Delay(1000);
            var supportedHardwareAcceleratorsTask = GetSupportedHardwareAccelerators();

            if (await Task.WhenAny(delayTask, supportedHardwareAcceleratorsTask) == delayTask)
            {
                VisualStateManager.GoToState(this, "PageLoadingWithMessage", true);
            }

            var supportedHardwareAccelerators = await supportedHardwareAcceleratorsTask;

            HashSet<WinMlEp> eps = [supportedHardwareAccelerators[0]];

            DeviceComboBox.Items.Clear();

            foreach (var hardwareAccelerator in selectedModels.SelectMany(m => m!.HardwareAccelerators).Distinct())
            {
                foreach (var ep in supportedHardwareAccelerators.Where(ep => ep.HardwareAccelerators.Contains(hardwareAccelerator)))
                {
                    eps.Add(ep);
                }
            }

            foreach (var ep in eps)
            {
                DeviceComboBox.Items.Add(ep);
            }

            UpdateWinMLFlyout();

            WinMlModelOptionsButton.Visibility = Visibility.Visible;
        }
        else
        {
            WinMlModelOptionsButton.Visibility = Visibility.Collapsed;
        }

        if (selectedModels.Count == 1)
        {
            // add the second model with null
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
            SampleContainer.ShowFooter = true;
        }
        else
        {
            SampleContainer.ShowFooter = false;
            LoadSample(viableSamples[0]);
        }
    }

    private void UpdateWinMLFlyout()
    {
        var options = App.AppData.WinMLSampleOptions;
        if (options.Policy != null)
        {
            var key = executionProviderDevicePolicies.FirstOrDefault(kvp => kvp.Value == options.Policy).Key;
            ExecutionPolicyComboBox.SelectedItem = key;
            WinMlModelOptionsButtonText.Text = key;
            DeviceComboBox.SelectedIndex = 0;
            segmentedControl.SelectedIndex = 0;
        }
        else if (options.EpName != null)
        {
            var selectedDevice = DeviceComboBox.Items.Where(i => (i as WinMlEp)?.Name == options.EpName).FirstOrDefault();
            if (selectedDevice != null)
            {
                DeviceComboBox.SelectedItem = selectedDevice;
            }
            else
            {
                DeviceComboBox.SelectedIndex = 0;
            }

            ExecutionPolicyComboBox.SelectedIndex = 0;
            CompileModelCheckBox.IsChecked = options.CompileModel;
            WinMlModelOptionsButtonText.Text = (DeviceComboBox.SelectedItem as WinMlEp)?.ShortName;
            segmentedControl.SelectedIndex = 1;
        }

        // in case already saved options do not apply to this sample
        _ = UpdateSampleOptions();
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
        _ = SampleContainer.LoadSampleAsync(sample, [.. modelDetails], App.AppData.WinMLSampleOptions);
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

        _ = Generator.AskGenerateAndOpenAsync(sample, modelDetails.Where(m => m != null).Select(m => m!), App.AppData.WinMLSampleOptions, XamlRoot);
    }

    private void ActionButtonsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate if the modelselectors collide with the export/code buttons
        if ((ActionsButtonHolderPanel.ActualWidth + ButtonsPanel.ActualWidth) >= e.NewSize.Width)
        {
            VisualStateManager.GoToState(this, "NarrowLayout", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "WideLayout", true);
        }
    }

    private void ModelBtn_Click(object sender, RoutedEventArgs e)
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

        LoadSample(selectedSample);
    }

    private async void ApplySampleOptions(object sender, RoutedEventArgs e)
    {
        WinMLOptionsFlyout.Hide();
        await UpdateSampleOptions();
        LoadSample(sample);
    }

    private async Task UpdateSampleOptions()
    {
        var oldOptions = App.AppData.WinMLSampleOptions;

        if (segmentedControl.SelectedIndex == 0)
        {
            var key = (ExecutionPolicyComboBox.SelectedItem as string) ?? executionProviderDevicePolicies.Keys.First();
            WinMlModelOptionsButtonText.Text = key;
            App.AppData.WinMLSampleOptions = new WinMlSampleOptions(executionProviderDevicePolicies[key], null, false);
        }
        else
        {
            var device = (DeviceComboBox.SelectedItem as WinMlEp) ?? (DeviceComboBox.Items.First() as WinMlEp);
            WinMlModelOptionsButtonText.Text = device!.ShortName;
            App.AppData.WinMLSampleOptions = new WinMlSampleOptions(null, device.Name, CompileModelCheckBox.IsChecked!.Value);
        }

        if (oldOptions == App.AppData.WinMLSampleOptions)
        {
            return;
        }

        await App.AppData.SaveAsync();
    }

    private void WinMLOptionsFlyout_Opening(object sender, object e)
    {
        UpdateWinMLFlyout();
    }
}