// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AIDevGallery.Pages;
internal sealed partial class WCROverview : Page
{
    private ObservableCollection<ApiDefinition> wcrAPIs = new();

    public WCROverview()
    {
        this.InitializeComponent();
        SetupAPIs();
    }

    private void SetupAPIs()
    {
        wcrAPIs.Clear();
        if (ModelTypeHelpers.ParentMapping.TryGetValue(ModelType.WCRAPIs, out List<ModelType>? innerItems))
        {
            foreach (var item in innerItems)
            {
                if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(item, out var apiDefinition))
                {
                    wcrAPIs.Add(apiDefinition);
                }
            }
        }
    }

    private void APIViewer_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        // TO DO: there must be a better way to do this?
        if (args.InvokedItem is ApiDefinition api && ModelTypeHelpers.ParentMapping.TryGetValue(ModelType.WCRAPIs, out List<ModelType>? innerItems))
        {
            foreach (var item in innerItems)
            {
                if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(item, out var apiDefinition))
                {
                    if (apiDefinition == api)
                    {
                        App.MainWindow.Navigate("apis", item);
                    }
                }
            }
        }
    }
}