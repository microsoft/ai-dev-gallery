// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.SourceGenerator.Models;

internal class ApiDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string ReadmeUrl { get; init; }
    public required string License { get; init; }
}