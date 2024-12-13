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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace AIDevGallery.Pages
{
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Scenario scenario)
            {
                this.scenario = scenario;
                PopulateModelControls();
            }
            else if (e.Parameter is SampleNavigationArgs sampleArgs)
            {
                this.scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
                PopulateModelControls(sampleArgs.ModelDetails);
            }
        }

        private void PopulateModelControls(ModelDetails? initialModelToLoad = null)
        {
            if (scenario == null)
            {
                return;
            }

            samples = SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType).ToList();
            List<Dictionary<ModelType, List<ModelDetails>>>? modelsDetailsDict = null;

            if (samples.Count == 0)
            {
                return;
            }
            else if (samples.Count == 1)
            {
                modelsDetailsDict = ModelDetailsHelper.GetModelDetails(samples.First());
            }
            else
            {
                var sample = samples.First();
                var modelKey = sample.Model1Types.First(); // First only?

                if (ModelTypeHelpers.ParentMapping.Values.Any(parent => parent.Contains(modelKey)))
                {
                    var parentKey = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(modelKey)).Key;

                    var listModelDetails = new List<ModelDetails>();

                    foreach (var s in samples)
                    {
                        listModelDetails.AddRange(ModelDetailsHelper.GetModelDetails(s).First().First().Value);
                    }

                    modelsDetailsDict =
                    [
                        new Dictionary<ModelType, List<ModelDetails>>
                        {
                            [parentKey] = listModelDetails
                        }
                    ];
                }

                if (sample.Model2Types != null)
                {
                    var modelKey2 = sample.Model2Types.First(); // First only?

                    if (ModelTypeHelpers.ParentMapping.Values.Any(parent => parent.Contains(modelKey2)))
                    {
                        var parentKey2 = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(modelKey2)).Key;

                        var listModelDetails2 = new List<ModelDetails>();

                        foreach (var s in samples)
                        {
                            listModelDetails2.AddRange(ModelDetailsHelper.GetModelDetails(s).ElementAt(1).First().Value);
                        }

                        modelsDetailsDict ??= [];

                        modelsDetailsDict.Add(
                            new Dictionary<ModelType, List<ModelDetails>>
                            {
                                [parentKey2] = listModelDetails2
                            });
                    }
                }
            }

            if (modelsDetailsDict == null)
            {
                return;
            }

            var models = modelsDetailsDict.First().SelectMany(g => g.Value).ToList();

            selectedModelDetails = SelectLatestOrDefault(models);

            if (modelsDetailsDict.Count > 1)
            {
                var models2 = modelsDetailsDict.ElementAt(1).SelectMany(g => g.Value).ToList();
                selectedModelDetails2 = SelectLatestOrDefault(models2);
                modelSelectionControl2.SetModels(models2, initialModelToLoad);
            }

            modelSelectionControl.SetModels(models, initialModelToLoad);
            UpdatePlaceholderControl();
        }

        private static ModelDetails? SelectLatestOrDefault(List<ModelDetails> models)
        {
            var latestModelOrApiUsageHistory = App.AppData.UsageHistory.FirstOrDefault(id => models.Any(m => m.Id == id));

            if (latestModelOrApiUsageHistory != null)
            {
                // select most recently used if there is one
                return models.First(m => m.Id == latestModelOrApiUsageHistory);
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
                UpdatePlaceholderControl();

                VisualStateManager.GoToState(this, "NoModelSelected", true);
                return;
            }
            else
            {
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
                    selectedModelDetails.Id);
            }
        }

        private void UpdatePlaceholderControl()
        {
            if (sample == null || (sample.Model2Types == null && selectedModelDetails == null))
            {
                PlaceholderControl.SetModels(modelSelectionControl.Models);
            }
            else
            {
                PlaceholderControl.SetModels(modelSelectionControl2.Models);
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

            Dictionary<ModelType, (string Id, string Path, string Url, long ModelSize)> cachedModels = [];

            (string Id, string Path, string Url, long ModelSize) cachedModel;

            if (selectedModelDetails.Size == 0)
            {
                cachedModel = (selectedModelDetails.Id, selectedModelDetails.Url, selectedModelDetails.Url, 0);
            }
            else
            {
                var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails.Url);
                if (realCachedModel == null)
                {
                    return;
                }

                cachedModel = (selectedModelDetails.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize);
            }

            var cachedSampleItem = App.FindSampleItemById(cachedModel.Id);

            var model1Type = sample.Model1Types.Any(cachedSampleItem.Contains)
                ? sample.Model1Types.First(cachedSampleItem.Contains)
                : sample.Model1Types.First();
            cachedModels.Add(model1Type, cachedModel);

            if (sample.Model2Types != null)
            {
                if (selectedModelDetails2 == null)
                {
                    return;
                }

                if (selectedModelDetails2.Size == 0)
                {
                    cachedModel = (selectedModelDetails2.Id, selectedModelDetails2.Url, selectedModelDetails2.Url, 0);
                }
                else
                {
                    var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails2.Url);
                    if (realCachedModel == null)
                    {
                        return;
                    }

                    cachedModel = (selectedModelDetails2.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize);
                }

                var model2Type = sample.Model2Types.Any(cachedSampleItem.Contains)
                    ? sample.Model2Types.First(cachedSampleItem.Contains)
                    : sample.Model2Types.First();

                cachedModels.Add(model2Type, cachedModel);
            }

            ContentDialog? dialog = null;
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
                        var generator = new Generator();

                        dialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = "Creating Visual Studio project..",
                            Content = new ProgressRing { IsActive = true, Width = 48, Height = 48 }
                        };
                        _ = dialog.ShowAsync();

                        Dictionary<ModelType, (string CachedModelDirectoryPath, string ModelUrl)> cachedModelsToGenerator = cachedModels
                            .Select(cm => (cm.Key, (cm.Value.Path, cm.Value.Url)))
                            .ToDictionary(x => x.Key, x => (x.Item2.Path, x.Item2.Url));

                        var projectPath = await generator.GenerateAsync(
                            sample,
                            cachedModelsToGenerator,
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

        private void ModelSelectionControl_ModelCollectionChanged(object sender)
        {
            PopulateModelControls();
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
    }
}