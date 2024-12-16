// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.SourceGenerator.Models;

internal interface IModelGroup
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Icon { get; init; }
    public int? Order { get; init; }
}