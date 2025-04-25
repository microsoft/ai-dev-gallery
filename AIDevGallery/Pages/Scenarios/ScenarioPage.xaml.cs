// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace AIDevGallery.Pages;

internal sealed partial class ScenarioPage : Page
{
    private Scenario? scenario;
    private List<Sample>? samples;
    private Sample? sample;
    private ModelDetails? selectedModelDetails;
    private ModelDetails? selectedModelDetails2;

    public ScenarioPage()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Scenario scenario)
        {
            this.scenario = scenario;
            await PopulateModelControls();
        }
        else if (e.Parameter is SampleNavigationArgs sampleArgs)
        {
            this.scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
            await PopulateModelControls(sampleArgs.ModelDetails);
        }

        if(this.scenario != null)
        {
            modelSelectionControl.Scenario = this.scenario;
            modelSelectionControl2.Scenario = this.scenario;
            ModelSelectionPlaceholderControl.Scenario = this.scenario;
        }
    }

    private async Task PopulateModelControls(ModelDetails? initialModelToLoad = null)
    {
        if (scenario == null)
        {
            return;
        }

        samples = SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType).ToList();

        if (samples.Count == 0)
        {
            return;
        }

        List<ModelDetails> modelDetailsList = new();
        List<ModelDetails> modelDetailsList2 = new();

        foreach (var s in samples)
        {
            var models = ModelDetailsHelper.GetModelDetails(s);

            // Model1Types
            if (models.Count > 0)
            {
                modelDetailsList.AddRange(models.First().Values.SelectMany(list => list).ToList());

                // Model2Types
                if (models.Count > 1)
                {
                    modelDetailsList2.AddRange(models[1].Values.SelectMany(list => list).ToList());
                }
            }

            if(!modelDetailsList.IsModelsDetailsListUploadCompatible())
            {
                modelSelectionControl.DisableAddLocalModelButton();
                ModelSelectionPlaceholderControl.DisableAddLocalModelButton();
            }

            if (!modelDetailsList2.IsModelsDetailsListUploadCompatible())
            {
                modelSelectionControl2.DisableAddLocalModelButton();
            }

            if (s.Model1Types.Contains(ModelType.LanguageModels))
            {
                // add external models
                var externalModels = await ExternalModelHelper.GetAllModelsAsync();
                modelDetailsList.AddRange(externalModels);
            }
        }

        if (modelDetailsList.Count == 0)
        {
            return;
        }

        if (modelDetailsList2.Count > 0)
        {
            modelDetailsList2 = modelDetailsList2.DistinctBy(m => m.Id).ToList();
            selectedModelDetails2 = SelectLatestOrDefault(modelDetailsList2);
            modelSelectionControl2.SetModels(modelDetailsList2, initialModelToLoad);
        }

        modelDetailsList = modelDetailsList.DistinctBy(m => m.Id).ToList();
        selectedModelDetails = SelectLatestOrDefault(modelDetailsList);
        modelSelectionControl.SetModels(modelDetailsList, initialModelToLoad);
        UpdateModelSelectionPlaceholderControl();
    }

    private static ModelDetails? SelectLatestOrDefault(List<ModelDetails> models)
    {
        var latestModelOrApiUsageHistory = App.AppData.UsageHistoryV2?.FirstOrDefault(u => models.Any(m => m.Id == u.Id));

        if (latestModelOrApiUsageHistory != default)
        {
            // select most recently used if there is one
            return models.First(m => m.Id == latestModelOrApiUsageHistory.Id);
        }

        return models.FirstOrDefault();
    }

    private async void ModelSelectionControl_SelectedModelChanged(object sender, ModelDetails? modelDetails)
    {
        ModelDropDown.HideFlyout();
        ModelDropDown2.HideFlyout();

        if (samples == null)
        {
            return;
        }

        if ((ModelSelectionControl)sender == modelSelectionControl)
        {
            selectedModelDetails = modelDetails;
        }
        else
        {
            selectedModelDetails2 = modelDetails;
        }

        if (selectedModelDetails != null)
        {
            foreach (var s in samples)
            {
                if (selectedModelDetails.IsHttpApi())
                {
                    if (s.Model1Types.Contains(ModelType.LanguageModels) || (s.Model2Types != null && s.Model2Types.Contains(ModelType.LanguageModels)))
                    {
                        sample = s;
                        break;
                    }
                }

                var extDict = ModelDetailsHelper.GetModelDetails(s).FirstOrDefault(dict => dict.Values.Any(listOfmd => listOfmd.Any(md => md.Id == selectedModelDetails.Id)))?.Values;
                if (extDict != null)
                {
                    var dict = extDict.FirstOrDefault(listOfmd => listOfmd.Any(md => md.Id == selectedModelDetails.Id));
                    if (dict != null)
                    {
                        sample = s;
                        break;
                    }
                }
            }
        }
        else
        {
            sample = null;
        }

        if (sample == null)
        {
            return;
        }

        if ((sample.Model2Types == null && selectedModelDetails == null) ||
            (sample.Model2Types != null && (selectedModelDetails == null || selectedModelDetails2 == null)))
        {
            UpdateModelSelectionPlaceholderControl();

            VisualStateManager.GoToState(this, "NoModelSelected", true);
            return;
        }
        else
        {
            ModelSelectionPlaceholderControl.HideDownloadDialog();
            VisualStateManager.GoToState(this, "ModelSelected", true);
            ModelDropDown2.Visibility = Visibility.Collapsed;

            ModelDropDown.Model = selectedModelDetails;
            List<ModelDetails> models = [selectedModelDetails!];

            if (sample.Model2Types != null)
            {
                models.Add(selectedModelDetails2!);
                ModelDropDown2.Model = selectedModelDetails2;
                ModelDropDown2.Visibility = Visibility.Visible;
            }

            await SampleContainer.LoadSampleAsync(sample, models);

            await App.AppData.AddMru(
                new MostRecentlyUsedItem()
                {
                    Type = MostRecentlyUsedItemType.Scenario,
                    ItemId = scenario!.Id,
                    Icon = scenario.Icon,
                    Description = scenario.Description,
                    SubItemId = selectedModelDetails!.Id,
                    DisplayName = scenario.Name
                },
                selectedModelDetails.Id,
                selectedModelDetails.HardwareAccelerators.First());
        }
    }

    private void UpdateModelSelectionPlaceholderControl()
    {
        if (sample == null || (sample.Model2Types == null && selectedModelDetails == null))
        {
            ModelSelectionPlaceholderControl.SetModels(modelSelectionControl.Models);
        }
        else
        {
            ModelSelectionPlaceholderControl.SetModels(modelSelectionControl2.Models);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText("aidevgallery://scenarios/" + scenario!.Id);
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void CodeToggle_Click(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton btn)
        {
            if (sample != null)
            {
                ToggleCodeButtonEvent.Log(sample.Name ?? string.Empty, btn.IsChecked == true);
            }

            if (btn.IsChecked == true)
            {
                SampleContainer.ShowCode();
            }
            else
            {
                SampleContainer.HideCode();
            }
        }
    }

    private async void ExportSampleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            sample == null ||
            selectedModelDetails == null ||
            (sample.Model2Types != null && selectedModelDetails2 == null))
        {
            return;
        }

        var cachedModels = sample.GetCacheModelDetailsDictionary([selectedModelDetails, selectedModelDetails2]);

        if (cachedModels == null)
        {
            return;
        }

        ContentDialog? dialog = null;
        var generator = new Generator();
        try
        {
            var totalSize = cachedModels.Sum(cm => cm.Value.ModelSize);
            if (totalSize == 0)
            {
                copyRadioButtons.Visibility = Visibility.Collapsed;
            }
            else
            {
                copyRadioButtons.Visibility = Visibility.Visible;
                ModelExportSizeTxt.Text = AppUtils.FileSizeToString(totalSize);
            }

            var output = await ExportDialog.ShowAsync();

            if (output == ContentDialogResult.Primary)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    dialog = new ContentDialog
                    {
                        XamlRoot = this.XamlRoot,
                        Title = "Creating Visual Studio project..",
                        Content = new ProgressRing { IsActive = true, Width = 48, Height = 48 }
                    };
                    _ = dialog.ShowAsync();

                    var projectPath = await generator.GenerateAsync(
                        sample,
                        cachedModels,
                        copyRadioButton.IsChecked == true && copyRadioButtons.Visibility == Visibility.Visible,
                        folder.Path,
                        CancellationToken.None);

                    dialog.Closed += async (_, _) =>
                    {
                        var confirmationDialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = "Project exported",
                            Content = new TextBlock
                            {
                                Text = "The project has been successfully exported to the selected folder.",
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            PrimaryButtonText = "Open folder",
                            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                            CloseButtonText = "Close"
                        };

                        var shouldOpenFolder = await confirmationDialog.ShowAsync();
                        if (shouldOpenFolder == ContentDialogResult.Primary)
                        {
                            await Windows.System.Launcher.LaunchFolderPathAsync(projectPath);
                        }
                    };
                    dialog.Hide();
                    dialog = null;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            generator.CleanUp();
            dialog?.Hide();

            var message = "Please try again, or report this issue.";
            if (ex is IOException)
            {
                message = ex.Message;
            }

            var errorDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Error while exporting project",
                Content = new TextBlock
                {
                    Text = $"An error occurred while exporting the project. {message}",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                PrimaryButtonText = "Copy details",
                CloseButtonText = "Close"
            };

            var result = await errorDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(ex.ToString());
                Clipboard.SetContentWithOptions(dataPackage, null);
            }
        }
    }

    private async void ModelSelectionControl_ModelCollectionChanged(object sender)
    {
        await PopulateModelControls();
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

    private async void AddLocalModelButton_Click(object sender, RoutedEventArgs e)
    {
        bool success = await UserAddedModelUtil.OpenAddModelFlow(this.Content.XamlRoot, samples);

        if(success)
        {
            ContentDialog failedToUploadDialog = new()
            {
                Title = "Failed to add model",
                Content = "Could not upload model. Double check that your model has a matching format/dimensionality to the other models in this scenario.",
                XamlRoot = this.Content.XamlRoot,
                CloseButtonText = "Close"
            };

            await failedToUploadDialog.ShowAsync();
        }
        else
        {
            PopulateModelControls();
        }
    }
}