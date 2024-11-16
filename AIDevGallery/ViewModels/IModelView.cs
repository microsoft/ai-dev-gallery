// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;

namespace AIDevGallery.ViewModels;

internal interface IModelView
{
    public bool OptionsVisible { get; set; }
    public ModelDetails ModelDetails { get; }
}