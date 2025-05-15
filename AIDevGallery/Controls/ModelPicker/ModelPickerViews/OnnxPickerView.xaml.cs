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
using Windows.Storage.Pickers;

namespace AIDevGallery.Controls.ModelPickerViews;
internal sealed partial class OnnxPickerView : BaseModelPickerView
{
    private List<ModelDetails> Models { get; set; } = new();
    private List<ModelType>? ModelTypes { get; set; }
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
        ModelTypes = types;

        if (types.Contains(ModelType.LanguageModels))
        {
            AddHFModelButton.Visibility = Visibility.Visible;
        }

        // local models supported for types
        // TODO: check which models support it
        if (types.Contains(ModelType.LanguageModels) || true)
        {
            AddLocalModelButton.Visibility = Visibility.Visible;
        }

        ResetAndLoadModelList();

        return Task.CompletedTask;
    }

    private void ResetAndLoadModelList()
    {
        Models.Clear();
        AvailableModels.Clear();
        DownloadableModels.Clear();
        UnavailableModels.Clear();

        if (ModelTypes == null || ModelTypes.Count == 0)
        {
            return;
        }

        foreach (ModelType type in ModelTypes)
        {
            Models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type));
        }

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
        ResetAndLoadModelList();
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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();

        if (folder != null)
        {
            var files = Directory.GetFiles(folder.Path);
            var config = files.Where(r => Path.GetFileName(r) == "genai_config.json").FirstOrDefault();

            if (string.IsNullOrEmpty(config) || App.ModelCache.Models.Any(m => m.Path == folder.Path))
            {
                var message = string.IsNullOrEmpty(config) ?
                    "The folder does not contain a model you can add. Ensure \"genai_config.json\" is present in the selected directory" :
                    "This model is already added";

                ContentDialog confirmFolderDialog = new()
                {
                    Title = "Can't add model",
                    Content = message,
                    XamlRoot = this.Content.XamlRoot,
                    CloseButtonText = "OK"
                };

                await confirmFolderDialog.ShowAsync();
                return;
            }

            HardwareAccelerator accelerator = HardwareAccelerator.CPU;

            try
            {
                string configContents = string.Empty;
                configContents = await File.ReadAllTextAsync(config);
                accelerator = UserAddedModelUtilsTemp.GetHardwareAcceleratorFromConfig(configContents);
            }
            catch (Exception ex)
            {
                ContentDialog confirmFolderDialog = new()
                {
                    Title = "Can't read genai_config.json",
                    Content = ex.Message,
                    XamlRoot = this.Content.XamlRoot,
                    CloseButtonText = "OK"
                };

                await confirmFolderDialog.ShowAsync();
                return;
            }

            var nameTextBox = new TextBox()
            {
                Text = Path.GetFileName(folder.Path),
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10),
                Header = "Model name"
            };

            ContentDialog nameModelDialog = new()
            {
                Title = "Add model",
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock()
                        {
                            Text = $"Adding ONNX model from \n \"{folder.Path}\"",
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        nameTextBox
                    }
                },
                XamlRoot = this.Content.XamlRoot,
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Add",
                DefaultButton = ContentDialogButton.Primary,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            string modelName = nameTextBox.Text;

            nameTextBox.TextChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(nameTextBox.Text))
                {
                    nameModelDialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    modelName = nameTextBox.Text;
                    nameModelDialog.IsPrimaryButtonEnabled = true;
                }
            };

            var result = await nameModelDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(folder.Path);
            long dirSize = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));

            var details = new ModelDetails()
            {
                Id = "useradded-local-languagemodel-" + Guid.NewGuid().ToString(),
                Name = modelName,
                Url = $"local-file:///{folder.Path}",
                Description = "Localy added GenAI Model",
                HardwareAccelerators = [accelerator],
                IsUserAdded = true,
                PromptTemplate = ModelDetailsHelper.GetTemplateFromName(folder.Path),
                Size = dirSize,
                ReadmeUrl = null,
                License = "unknown"
            };

            await App.ModelCache.AddLocalModelToCache(details, folder.Path);
        }
    }
}