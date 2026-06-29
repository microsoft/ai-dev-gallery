// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace AIDevGallery.SourceGenerator.Models;

internal class ApiDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string IconGlyph { get; init; }
    public required string Description { get; init; }
    public required string ReadmeUrl { get; init; }
    public required string License { get; init; }
    public required string SampleIdToShowInDocs { get; init; }
    public string? Category { get; init; } = null;

    public List<HardwareAccelerator>? SupportedHardwareAccelerators { get; init; } = null;

    // Optional. The API's own "supported hardware" documentation page.
    public string? SupportedHardwareUrl { get; init; } = null;
}