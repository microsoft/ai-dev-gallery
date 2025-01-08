// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIDevGallery.ViewModels;

internal partial class AvailableModel : ObservableObject, IModelView
{
    public ModelDetails ModelDetails { get; private set; }

    [ObservableProperty]
    public partial bool OptionsVisible { get; set; }

    public AvailableModel(ModelDetails modelDetails)
    {
        ModelDetails = modelDetails;
    }
}