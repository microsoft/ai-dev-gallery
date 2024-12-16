// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.SourceGenerator.Models;

internal class Scenario
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Id { get; init; }
}