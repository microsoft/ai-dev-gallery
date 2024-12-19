// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace AIDevGallery.SourceGenerator.Models;

internal class ModelFamily
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? DocsUrl { get; init; }
    public int? Order { get; init; }
    public required Dictionary<string, Model> Models { get; init; }
    public string ReadmeUrl { get; init; } = null!;
}