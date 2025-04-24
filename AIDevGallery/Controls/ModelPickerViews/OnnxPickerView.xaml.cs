// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;
internal sealed partial class OnnxPickerView : BaseModelPickerView
{
    private List<ModelDetails>? Models { get; set; }
    public ModelDetails? Selected { get; private set; }

    private ObservableCollection<AvailableModel> AvailableModels { get; } = [];
    private ObservableCollection<DownloadableModel> DownloadableModels { get; } = [];
    private ObservableCollection<BaseModel> UnavailableModels { get; } = [];

    public OnnxPickerView()
    {
        this.InitializeComponent();

        App.ModelCache.CacheStore.ModelsChanged += CacheStore_ModelsChanged;
        Unloaded += (sender, e) => App.ModelCache.CacheStore.ModelsChanged -= CacheStore_ModelsChanged;
    }

    public override void Load(List<ModelType> types)
    {
        Models = Models ?? new();

        foreach (ModelType type in types)
        {
            Models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type));
        }

        ResetAndLoadModelList(); // TODO: initialSelectedModel);
    }

    private void ResetAndLoadModelList()
    {
        AvailableModels.Clear();
        DownloadableModels.Clear();
        UnavailableModels.Clear();

        if (Models == null || Models.Count == 0)
        {
            return;
        }

        PopulateModelDetailsLists();

        //if (AvailableModels.Count > 0)
        //{
        //    var modelIds = AvailableModels.Select(s => s.ModelDetails.Id);
        //    var modelOrApiUsageHistory = App.AppData.UsageHistoryV2?.FirstOrDefault(u => modelIds.Contains(u.Id));

        //    ModelDetails? modelToPreselect = null;

        //    if (selectedModel != null)
        //    {
        //        modelToPreselect = AvailableModels.Where(m => m.ModelDetails.Id == selectedModel.Id).FirstOrDefault()?.ModelDetails;
        //    }

        //    if (modelToPreselect == null && modelOrApiUsageHistory != default)
        //    {
        //        var models = AvailableModels.Where(am => am.ModelDetails.Id == modelOrApiUsageHistory.Id).ToList();
        //        if (models.Count > 0)
        //        {
        //            if (modelOrApiUsageHistory.HardwareAccelerator != null)
        //            {
        //                var model = models.FirstOrDefault(m => m.ModelDetails.HardwareAccelerators.Contains(modelOrApiUsageHistory.HardwareAccelerator.Value));
        //                if (model != null)
        //                {
        //                    modelToPreselect = model.ModelDetails;
        //                }
        //            }

        //            if (modelToPreselect == null)
        //            {
        //                modelToPreselect = models.FirstOrDefault()?.ModelDetails;
        //            }
        //        }
        //    }

        //    if (modelToPreselect == null)
        //    {
        //        modelToPreselect = AvailableModels[0].ModelDetails;
        //    }

        //    SetSelectedModel(modelToPreselect);
        //}
        //else
        //{
        //    // No downloaded models
        //    SetSelectedModel(null);
        //}
    }

    private void PopulateModelDetailsLists()
    {
        if (Models == null || Models.Count == 0)
        {
            return;
        }

        foreach (var model in Models)
        {
            if (!model.IsOnnxModel())
            {
                continue;
            }

            if (model.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
            {
                // UnavailableModels.Add(new DownloadableModel(model));
            }
            else if (!App.ModelCache.IsModelCached(model.Url))
            {
                // Needs to be in the downloads list
                var existingDownloadableModel = DownloadableModels.FirstOrDefault(m => m.ModelDetails.Url == model.Url);
                if (existingDownloadableModel == null)
                {
                    DownloadableModels.Add(new DownloadableModel(model));
                }

                // remove if already in the availablelist
                var existingAvailableModel = AvailableModels.FirstOrDefault(m => m?.ModelDetails.Url == model.Url);
                if (existingAvailableModel != null)
                {
                    AvailableModels.Remove(existingAvailableModel);
                }
            }
            else
            {
                // needs to be in the available list
                var existingAvailableModel = AvailableModels.FirstOrDefault(m => m?.ModelDetails.Url == model.Url);
                if (existingAvailableModel == null)
                {
                    foreach (var hardwareAccelerator in model.HardwareAccelerators)
                    {
                        var modelDetails = new ModelDetails
                        {
                            Id = model.Id,
                            Name = model.Name,
                            Url = model.Url,
                            Description = model.Description,
                            HardwareAccelerators = [hardwareAccelerator],
                            SupportedOnQualcomm = model.SupportedOnQualcomm,
                            Size = model.Size,
                            Icon = model.Icon,
                            ParameterSize = model.ParameterSize,
                            IsUserAdded = model.IsUserAdded,
                            PromptTemplate = model.PromptTemplate,
                            ReadmeUrl = model.ReadmeUrl,
                            License = model.License,
                            FileFilters = model.FileFilters
                        };

                        if (modelDetails.Compatibility.CompatibilityState == ModelCompatibilityState.Compatible)
                        {
                            AvailableModels.Add(new AvailableModel(modelDetails));
                        }
                        else
                        {
                            // UnavailableModels.Add(new DownloadableModel(modelDetails));
                        }
                    }
                }

                // remove if already in the downloadable list
                var existingDownloadableModel = DownloadableModels.FirstOrDefault(m => m.ModelDetails.Url == model.Url);
                if (existingDownloadableModel != null)
                {
                    DownloadableModels.Remove(existingDownloadableModel);
                }
            }
        }

        //SetHeaderVisibilityStates();
    }

    //private void SetSelectedModel(ModelDetails? modelDetails, HardwareAccelerator? accelerator = null)
    //{
    //    if (modelDetails != null)
    //    {
    //        if (modelDetails.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible && !modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI))
    //        {
    //            if (Selected != null)
    //            {
    //                // if the selected model is not compatible, we should not allow the user to select it, so we select the previous model
    //                SetViewSelection(Selected);
    //            }

    //            return;
    //        }

    //        Selected = modelDetails;
    //        SetViewSelection(modelDetails);
    //        OnSelectedModelChanged(this, modelDetails);
    //    }
    //    else
    //    {
    //        Selected = null;

    //        // model not available
    //        OnSelectedModelChanged(this, null);
    //    }
    //}

    private void CacheStore_ModelsChanged(ModelCacheStore sender)
    {
        PopulateModelDetailsLists();
    }

    private void ModelSelectionItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is AvailableModel model)
        {
            OnSelectedModelChanged(this, model.ModelDetails);
        }
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null)
        {
            var availableModel = AvailableModels.FirstOrDefault(m => m.ModelDetails.Id == modelDetails.Id);
            if (availableModel != null)
            {
                ModelSelectionItemsView.Select(AvailableModels.IndexOf(availableModel));
            }
            else
            {
                ModelSelectionItemsView.DeselectAll();
            }
        }
        else
        {
            ModelSelectionItemsView.DeselectAll();
        }
    }

    private void StopPropagatingHandler(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void ItemContainer_GotFocus(object sender, RoutedEventArgs e)
    {
        var item = sender as FrameworkElement;
        var focusedModel = item?.Tag as IModelView;

        ShowOptionsButtonForFocusedModel(focusedModel);
    }

    private void ItemContainer_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var item = sender as FrameworkElement;
        var focusedModel = item?.Tag as IModelView;

        ShowOptionsButtonForFocusedModel(focusedModel);
    }

    private void ShowOptionsButtonForFocusedModel(IModelView? focusedModel)
    {
        List<IModelView> models = AvailableModels.Cast<IModelView>()
            .Concat(DownloadableModels.Cast<IModelView>())
            .Concat(UnavailableModels.Cast<IModelView>())
            .ToList();

        foreach (var model in models)
        {
            if (focusedModel == model)
            {
                focusedModel.OptionsVisible = true;
            }
            else
            {
                model.OptionsVisible = false;
            }
        }
    }

    private void OpenModelFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var cachedModel = App.ModelCache.GetCachedModel(details.Url);
            if (cachedModel != null)
            {
                var path = cachedModel.Path;
                if (path != null)
                {
                    if (cachedModel.IsFile)
                    {
                        path = Path.GetDirectoryName(path);
                    }

                    OpenModelFolderEvent.Log(cachedModel.Url);

                    Process.Start("explorer.exe", path!);
                }
            }
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            ContentDialog deleteDialog = new()
            {
                Title = "Delete model",
                Content = "Are you sure you want to delete this model? You can download it again from this page.",
                PrimaryButtonText = "Yes",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                CloseButtonText = "No"
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await App.ModelCache.DeleteModelFromCache(details.Url);
                //OnModelCollectionChanged(); // TODO
            }
        }
    }

    private void ModelCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            App.MainWindow.Navigate("Models", details.Id);
        }
    }

    private void ApiDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            App.MainWindow.Navigate("apis", details);
        }
    }

    private void CopyModelPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var dataPackage = new DataPackage();
            var modelCache = App.ModelCache.Models.FirstOrDefault(m => m.Details.Id == details.Id);
            if (modelCache != null)
            {
                dataPackage.SetText(modelCache.Path);
                Clipboard.SetContentWithOptions(dataPackage, null);
            }
        }
    }

    private void ViewLicense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var license = LicenseInfo.GetLicenseInfo(details.License);
            string licenseUrl;
            if (license.LicenseUrl != null)
            {
                licenseUrl = license.LicenseUrl;
            }
            else
            {
                licenseUrl = details.Url;
            }

            Process.Start(new ProcessStartInfo()
            {
                FileName = licenseUrl,
                UseShellExecute = true
            });
        }
    }

    private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            var downloadSource = downloadableModel.ModelDetails.Url.StartsWith("https://github.com", StringComparison.InvariantCultureIgnoreCase) ? "GitHub" : "Hugging Face";
            var license = LicenseInfo.GetLicenseInfo(downloadableModel.ModelDetails.License);

            ModelNameTxt.Text = downloadableModel.ModelDetails.Name;
            ModelSourceTxt.Text = downloadSource;
            ModelLicenseLink.NavigateUri = new Uri(license.LicenseUrl ?? downloadableModel.ModelDetails.Url);
            ModelLicenseLabel.Text = license.Name;

            if (downloadableModel.Compatibility.CompatibilityState != ModelCompatibilityState.Compatible)
            {
                WarningInfoBar.Message = downloadableModel.Compatibility.CompatibilityIssueDescription;
                WarningInfoBar.IsOpen = true;
            }

            AgreeCheckBox.IsChecked = false;

            var output = await DownloadDialog.ShowAsync();

            if (output == ContentDialogResult.Primary)
            {
                App.ModelCache.DownloadQueue.ModelDownloadCompleted += DownloadQueue_ModelDownloadCompleted;
                downloadableModel.StartDownload();
            }
        }
    }

    private void DownloadQueue_ModelDownloadCompleted(object? sender, ModelDownloadCompletedEventArgs e)
    {
        App.ModelCache.DownloadQueue.ModelDownloadCompleted -= DownloadQueue_ModelDownloadCompleted;
        //OnModelCollectionChanged(); // TODO
    }
}