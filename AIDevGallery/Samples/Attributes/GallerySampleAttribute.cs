// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System;

namespace AIDevGallery.Samples.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
internal sealed class GallerySampleAttribute : Attribute
{
    public required ModelType[] Model1Types { get; init; }
    public ModelType[]? Model2Types { get; init; }
    public required string? Name { get; init; }
    public required string Id { get; init; }
    public required string Icon { get; init; }
    public ScenarioType Scenario { get; init; }
    public SharedCodeEnum[]? SharedCode { get; init; }
    public string[]? NugetPackageReferences { get; init; }
    public string[]? AssetFilenames { get; init; }
}