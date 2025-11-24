// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.ViewModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;

internal record FoundryCatalogModelGroup(string Alias, string License, IEnumerable<FoundryCatalogModelDetails> Details, IEnumerable<DownloadableModel> Models);
internal record FoundryCatalogModelDetails(string ExecutionProvider, long SizeInBytes);
internal record FoundryModelPair(string Name, ModelDetails ModelDetails, Model? FoundryModel);
internal sealed partial class FoundryLocalPickerView : BaseModelPickerView
{
    private ObservableCollection<FoundryModelPair> AvailableModels { get; } = [];
    private ObservableCollection<FoundryCatalogModelGroup> CatalogModels { get; } = [];
    private string FoundryLocalUrl => FoundryLocalModelProvider.Instance?.Url ?? string.Empty;

    public FoundryLocalPickerView()
    {
        this.InitializeComponent();

        App.ModelDownloadQueue.ModelDownloadCompleted += ModelDownloadQueue_ModelDownloadCompleted;
    }

    private void ModelDownloadQueue_ModelDownloadCompleted(object? sender, Utils.ModelDownloadCompletedEventArgs e)
    {
        _ = Load([]);
    }

    public override async Task Load(List<ModelType> types)
    {
        VisualStateManager.GoToState(this, "ShowLoading", true);

        if (!await FoundryLocalModelProvider.Instance.IsAvailable())
        {
            VisualStateManager.GoToState(this, "ShowNotAvailable", true);
            return;
        }

        AvailableModels.Clear();
        CatalogModels.Clear();

        foreach (var model in await FoundryLocalModelProvider.Instance.GetModelsAsync(ignoreCached: true) ?? [])
        {
            if (model.ProviderModelDetails is Model foundryModel)
            {
                AvailableModels.Add(new(model.Name, model, foundryModel));
            }
            else
            {
                AvailableModels.Add(new(model.Name, model, null));
            }
        }

        var catalogModelsDict = FoundryLocalModelProvider.Instance.GetAllModelsInCatalog().ToDictionary(m => m.Name, m => m);

        var catalogModels = catalogModelsDict.Values
            .GroupBy(m => m.Name.Split('-').FirstOrDefault() ?? m.Name) // Group by model family
            .OrderByDescending(g => g.Key);

        foreach (var modelGroup in catalogModels)
        {
            var firstModel = modelGroup.FirstOrDefault(m => !AvailableModels.Any(cm => cm.ModelDetails.Name == m.Name));
            if (firstModel == null)
            {
                continue;
            }

            CatalogModels.Add(new FoundryCatalogModelGroup(
                modelGroup.Key,
                firstModel.License ?? "unknown",
                modelGroup.Select(m => new FoundryCatalogModelDetails("CPU", m.Size)), // Simplified for now
                modelGroup.Where(m => !AvailableModels.Any(cm => cm.ModelDetails.Name == m.Name))
                .Select(m => new DownloadableModel(m))));
        }

        VisualStateManager.GoToState(this, "ShowModels", true);
    }

    private void CopyModelName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is FoundryModelPair pair)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(pair.ModelDetails.Name);
            Clipboard.SetContentWithOptions(dataPackage, null);
        }
    }

    private void ModelSelectionItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is FoundryModelPair pair && pair.FoundryModel is not null)
        {
            OnSelectedModelChanged(this, pair.ModelDetails);
        }
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        if (modelDetails != null)
        {
            var modelToSelect = AvailableModels.FirstOrDefault(m => m.ModelDetails.Name == modelDetails.Name);

            if (modelToSelect != null)
            {
                DispatcherQueue.TryEnqueue(() => ModelSelectionItemsView.Select(AvailableModels.IndexOf(modelToSelect)));
            }
            else
            {
                DispatcherQueue.TryEnqueue(() => ModelSelectionItemsView.DeselectAll());
            }
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => ModelSelectionItemsView.DeselectAll());
        }
    }

    private void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadableModel downloadableModel)
        {
            downloadableModel.StartDownload();
        }
    }

    internal static string GetExecutionProviderTextFromModel(ModelDetails model)
    {
        var foundryModel = model.ProviderModelDetails as Model;
        if (foundryModel == null)
        {
            return string.Empty;
        }

        return $"Download CPU variant"; // Simplified for now
    }

    internal static string GetShortExectionProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return provider;
        }

        var shortprovider = provider.Split(
            "ExecutionProvider",
            System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries).FirstOrDefault();

        return string.IsNullOrWhiteSpace(shortprovider) ? provider : shortprovider;
    }

    internal static string GetExecutionProviderName(Model? model)
    {
        // For now, return a default value since we need to check the actual SDK properties
        return "CPU";
    }

    internal static string GetShortExecutionProviderFromModel(Model? model)
    {
        if (model == null)
        {
            return string.Empty;
        }

        return GetShortExectionProvider(GetExecutionProviderName(model));
    }

    private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(FoundryLocalUrl);
        Clipboard.SetContentWithOptions(dataPackage, null);
    }
}