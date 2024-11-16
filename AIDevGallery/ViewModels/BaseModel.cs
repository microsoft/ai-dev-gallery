// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIDevGallery.ViewModels;

internal partial class BaseModel : ObservableObject, IModelView
{
    public ModelDetails ModelDetails { get; }
    public ModelCompatibility Compatibility { get; init; }

    [ObservableProperty]
    private bool optionsVisible;

    public BaseModel(ModelDetails modelDetails)
    {
        ModelDetails = modelDetails;
        Compatibility = ModelDetails.Compatibility;
    }
}