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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls;

internal partial class ModelSelectionControl : UserControl
{
    public List<ModelDetails>? Models { get; private set; }
    public Scenario Scenario { get; set; }
    public ModelDetails? Selected { get; private set; }

    public static readonly DependencyProperty DownloadableModelsTitleProperty = DependencyProperty.Register(nameof(DownloadableModelsTitle), typeof(string), typeof(ModelSelectionControl), new PropertyMetadata(defaultValue: null));

    public string DownloadableModelsTitle
    {
        get => (string)GetValue(DownloadableModelsTitleProperty);
        set => SetValue(DownloadableModelsTitleProperty, value);
    }

    public static readonly DependencyProperty AvailableModelsTitleProperty = DependencyProperty.Register(nameof(AvailableModelsTitle), typeof(string), typeof(ModelSelectionControl), new PropertyMetadata(defaultValue: null));

    public string AvailableModelsTitle
    {
        get => (string)GetValue(AvailableModelsTitleProperty);
        set => SetValue(AvailableModelsTitleProperty, value);
    }

    public static readonly DependencyProperty UnavailableModelsTitleProperty = DependencyProperty.Register(nameof(UnavailableModelsTitle), typeof(string), typeof(ModelSelectionControl), new PropertyMetadata(defaultValue: null));

    public string UnavailableModelsTitle
    {
        get => (string)GetValue(UnavailableModelsTitleProperty);
        set => SetValue(UnavailableModelsTitleProperty, value);
    }

    public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(nameof(IsSelectionEnabled), typeof(bool), typeof(ModelSelectionControl), new PropertyMetadata(defaultValue: false, null));

    public bool IsSelectionEnabled
    {
        get => (bool)GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    public static readonly DependencyProperty ModelCardVisibilityProperty = DependencyProperty.Register(nameof(ModelCardVisibility), typeof(Visibility), typeof(ModelSelectionControl), new PropertyMetadata(defaultValue: Visibility.Collapsed));

    public Visibility ModelCardVisibility
    {
        get => (Visibility)GetValue(ModelCardVisibilityProperty);
        set => SetValue(ModelCardVisibilityProperty, value);
    }

    private ObservableCollection<AvailableModel> AvailableModels { get; } = [];
    private ObservableCollection<DownloadableModel> DownloadableModels { get; } = [];
    private ObservableCollection<BaseModel> UnavailableModels { get; } = [];

    public List<ModelDetails> DownloadedModels => AvailableModels.Select(a => a.ModelDetails).ToList();

    public ModelSelectionControl()
    {
        this.InitializeComponent();
        App.ModelCache.CacheStore.ModelsChanged += CacheStore_ModelsChanged;
        Unloaded += (sender, e) => App.ModelCache.CacheStore.ModelsChanged -= CacheStore_ModelsChanged;
    }

    public void SetModels(List<ModelDetails>? models, ModelDetails? initialSelectedModel = null)
    {
        Models = models;
        ResetAndLoadModelList(initialSelectedModel);
    }

    private void CacheStore_ModelsChanged(ModelCacheStore sender)
    {
        PopulateModelDetailsLists();
    }

    private void ResetAndLoadModelList(ModelDetails? selectedModel = null)
    {
        AvailableModels.Clear();
        DownloadableModels.Clear();
        UnavailableModels.Clear();

        if (Models == null || Models.Count == 0)
        {
            return;
        }

        PopulateModelDetailsLists();

        if (AvailableModels.Count > 0)
        {
            var modelIds = AvailableModels.Select(s => s.ModelDetails.Id);
            var modelOrApiUsageHistory = App.AppData.UsageHistoryV2?.FirstOrDefault(u => modelIds.Contains(u.Id));

            ModelDetails? modelToPreselect = null;

            if (selectedModel != null)
            {
                modelToPreselect = AvailableModels.Where(m => m.ModelDetails.Id == selectedModel.Id).FirstOrDefault()?.ModelDetails;
            }

            if (modelToPreselect == null && modelOrApiUsageHistory != default)
            {
                var models = AvailableModels.Where(am => am.ModelDetails.Id == modelOrApiUsageHistory.Id).ToList();
                if (models.Count > 0)
                {
                    if (modelOrApiUsageHistory.HardwareAccelerator != null)
                    {
                        var model = models.FirstOrDefault(m => m.ModelDetails.HardwareAccelerators.Contains(modelOrApiUsageHistory.HardwareAccelerator.Value));
                        if (model != null)
                        {
                            modelToPreselect = model.ModelDetails;
                        }
                    }

                    if (modelToPreselect == null)
                    {
                        modelToPreselect = models.FirstOrDefault()?.ModelDetails;
                    }
                }
            }

            if (modelToPreselect == null)
            {
                modelToPreselect = AvailableModels[0].ModelDetails;
            }

            SetSelectedModel(modelToPreselect);
        }
        else
        {
            // No downloaded models
            SetSelectedModel(null);
        }
    }

    private void SetSelectedModel(ModelDetails? modelDetails, HardwareAccelerator? accelerator = null)
    {
        if (modelDetails != null)
        {
            if (modelDetails.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible && !modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI))
            {
                if (Selected != null)
                {
                    // if the selected model is not compatible, we should not allow the user to select it, so we select the previous model
                    SetViewSelection(Selected);
                }

                return;
            }

            Selected = modelDetails;
            SetViewSelection(modelDetails);
            OnSelectedModelChanged(this, modelDetails);
        }
        else
        {
            Selected = null;

            // model not available
            OnSelectedModelChanged(this, null);
        }
    }

    private void SetViewSelection(ModelDetails modelDetails)
    {
        if (IsSelectionEnabled)
        {
            ModelSelectionItemsView.DeselectAll();

            var models = AvailableModels.Where(a => a.ModelDetails == modelDetails).ToList();

            if (models.Count != 0)
            {
                ModelSelectionItemsView.Select(AvailableModels.IndexOf(models.First()));
            }
        }
    }

    private void ModelSelectionItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (!IsSelectionEnabled)
        {
            return;
        }

        if (args.InvokedItem is AvailableModel model)
        {
            SetSelectedModel(model.ModelDetails);
        }
    }

    private void PopulateModelDetailsLists()
    {
        if (Models == null || Models.Count == 0)
        {
            return;
        }

        foreach (var model in Models)
        {
            if (model.IsApi())
            {
                if (model.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI))
                {
                    if (model.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
                    {
                        AvailableModels.Add(new AvailableModel(model));
                    }
                    else
                    {
                        // insert available APIs on top
                        AvailableModels.Insert(0, new AvailableModel(model));
                    }
                }
                else if (model.Compatibility.CompatibilityState == ModelCompatibilityState.Compatible)
                {
                    AvailableModels.Add(new AvailableModel(model));
                }
                else
                {
                    // UnavailableModels.Add(new DownloadableModel(model));
                }
            }
            else if (model.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
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

        SetHeaderVisibilityStates();
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
                OnModelCollectionChanged();
            }
        }
    }

    private void ModelCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            // we are in the sample view, open in app modelcard
            if (ModelCardVisibility == Visibility.Visible)
            {
                App.MainWindow.Navigate("Models", details.Id);
            }

            // we are in the in app modelcard, open browser to modelcard
            else
            {
                string? modelcardUrl = details.ReadmeUrl;
                if (string.IsNullOrEmpty(details.ReadmeUrl))
                {
                    if (details.Url.StartsWith("https://github.com", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var ghUrl = new GitHubUrl(details.Url);
                        modelcardUrl = ghUrl.GetUrlRoot();
                    }
                    else
                    {
                        var hfUrl = new HuggingFaceUrl(details.Url);
                        modelcardUrl = hfUrl.GetUrlRoot();
                    }
                }

                Process.Start(new ProcessStartInfo()
                {
                    FileName = modelcardUrl,
                    UseShellExecute = true
                });
            }
        }
    }

    private void ApiDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            // we are in the sample view, open in app modelcard
            if (ModelCardVisibility == Visibility.Visible)
            {
                App.MainWindow.Navigate("apis", details);
            }
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

    public delegate void SelectedModelChangedEventHandler(object sender, ModelDetails? modelDetails);
    public event SelectedModelChangedEventHandler? SelectedModelChanged;

    private void OnSelectedModelChanged(object sender, ModelDetails? args)
    {
        SelectedModelChanged?.Invoke(sender, args);
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
        OnModelCollectionChanged();
    }

    // We need to check the ItemsView after loading to make the indicator show up
    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (Selected != null)
        {
            SetViewSelection(Selected);
        }

        SetHeaderVisibilityStates();
    }

    private void SetHeaderVisibilityStates()
    {
        if (!string.IsNullOrEmpty(AvailableModelsTitle) && AvailableModels.Count > 0)
        {
            VisualStateManager.GoToState(this, "ShowAvailableModelsTitle", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "HideAvailableModelsTitle", true);
        }

        if (!string.IsNullOrEmpty(DownloadableModelsTitle) && DownloadableModels.Count > 0)
        {
            VisualStateManager.GoToState(this, "ShowDownloadableModelsTitle", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "HideDownloadableModelsTitle", true);
        }

        if (!string.IsNullOrEmpty(UnavailableModelsTitle) && UnavailableModels.Count > 0)
        {
            VisualStateManager.GoToState(this, "ShowUnavailableModelsTitle", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "HideUnavailableModelsTitle", true);
        }
    }

    // Event that gets triggered whenever a model has been removed or added to the cache
    public delegate void ModelCollectionChangedEventHandler(object sender);
    public event ModelCollectionChangedEventHandler? ModelCollectionChanged;

    protected virtual void OnModelCollectionChanged()
    {
        ModelCollectionChanged?.Invoke(this);
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

    public void HideDownloadDialog()
    {
        DownloadDialog?.Hide();
    }

    private void OllamaCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(OllamaHelper.GetOllamaUrl());
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void OllamaViewModelDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = $"https://ollama.com/library/{details.Name}",
                UseShellExecute = true
            });
        }
    }

    private async void AddLocalModelButton_Click(object sender, RoutedEventArgs e)
    {
        bool success = false;
        var samples = Samples.SampleDetails.Samples.Where(sample => sample.Scenario == Scenario.ScenarioType).ToList();

        if (samples != null)
        {
            success = await UserAddedModelUtil.OpenAddModelFlow(this.Content.XamlRoot, samples);
        }

        if(success)
        {
            OnModelCollectionChanged();
        }
    }

    public void DisableAddLocalModelButton()
    {
        AddLocalModelButton.Visibility = Visibility.Collapsed;
    }
}