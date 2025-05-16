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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;
internal sealed partial class OnnxPickerView : BaseModelPickerView
{
    private List<ModelDetails> models = [];
    private List<ModelType>? modelTypes;

    public ModelDetails? Selected { get; private set; }

    private ObservableCollection<AvailableModel> AvailableModels { get; } = [];
    private ObservableCollection<DownloadableModel> DownloadableModels { get; } = [];
    private ObservableCollection<BaseModel> UnavailableModels { get; } = [];

    public OnnxPickerView()
    {
        this.InitializeComponent();

        App.ModelCache.CacheStore.ModelsChanged += CacheStore_ModelsChanged;
    }

    public override Task Load(List<ModelType> types)
    {
        modelTypes = types;

        ResetAndLoadModelList();

        if (types.Contains(ModelType.LanguageModels))
        {
            AddHFModelButton.Visibility = Visibility.Visible;
        }

        // local models supported for types
        if (types.Contains(ModelType.LanguageModels) || models.IsModelsDetailsListUploadCompatible())
        {
            AddLocalModelButton.Visibility = Visibility.Visible;
        }

        return Task.CompletedTask;
    }

    private void ResetAndLoadModelList()
    {
        models.Clear();
        AvailableModels.Clear();
        DownloadableModels.Clear();
        UnavailableModels.Clear();

        if (modelTypes == null || modelTypes.Count == 0)
        {
            return;
        }

        foreach (ModelType type in modelTypes)
        {
            models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type));
        }

        if (models == null || models.Count == 0)
        {
            return;
        }

        foreach (var model in models)
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
                    AvailableModels.Add(new AvailableModel(model));
                }

                // remove if already in the downloadable list
                var existingDownloadableModel = DownloadableModels.FirstOrDefault(m => m.ModelDetails.Url == model.Url);
                if (existingDownloadableModel != null)
                {
                    DownloadableModels.Remove(existingDownloadableModel);
                }
            }
        }
    }

    private void CacheStore_ModelsChanged(ModelCacheStore sender)
    {
        DispatcherQueue.TryEnqueue(ResetAndLoadModelList);
    }

    private void ModelSelectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView modelView && modelView.SelectedItem is AvailableModel model)
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
                ModelSelectionView.SelectedIndex = AvailableModels.IndexOf(availableModel);
            }
            else
            {
                ModelSelectionView.SelectedItem = null;
            }
        }
        else
        {
            ModelSelectionView.SelectedItem = null;
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
                ResetAndLoadModelList();
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
    }

    private void AddHFModelButton_Click(object sender, RoutedEventArgs e)
    {
        AddHFModelView.Visibility = Visibility.Visible;
        ModelView.Visibility = Visibility.Collapsed;
    }

    private void AddHFModelView_CloseRequested(object sender)
    {
        AddHFModelView.Visibility = Visibility.Collapsed;
        ModelView.Visibility = Visibility.Visible;
    }

    private async void AddLocalModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (modelTypes == null)
        {
            return;
        }

        try
        {
            if (modelTypes.Contains(ModelType.LanguageModels))
            {
                await UserAddedModelUtil.OpenAddLanguageModelFlow(Content.XamlRoot);
            }
            else
            {
                await UserAddedModelUtil.OpenAddModelFlow(Content.XamlRoot, modelTypes);
            }

            ResetAndLoadModelList();
        }
        catch(Exception ex)
        {
            ShowException(ex);
        }
    }

    private async void ShowException(Exception? ex, string? optionalMessage = null)
    {
        var msg = $"Error:\n{ex?.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}";

        var errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = msg,
            IsTextSelectionEnabled = true,
        };

        ContentDialog exceptionDialog = new()
        {
            Title = "Something went wrong",
            Content = errorText,
            PrimaryButtonText = "Copy error details",
            XamlRoot = App.MainWindow.Content.XamlRoot,
            CloseButtonText = "Close",
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
        };

        var result = await exceptionDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string exceptionDetails = string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n";

            if (ex != null)
            {
                exceptionDetails += GetExceptionDetails(ex);
            }

            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(exceptionDetails);
            Clipboard.SetContent(dataPackage);
        }
    }

    private string GetExceptionDetails(Exception ex)
    {
        var innerExceptionData = ex.InnerException == null ? string.Empty :
            $"Inner Exception:\n{GetExceptionDetails(ex.InnerException)}";
        string details = $@"Type: {ex.GetType().Name}
Message: {ex.Message}
StackTrace: {ex.StackTrace}
{innerExceptionData}";
        return details;
    }
}