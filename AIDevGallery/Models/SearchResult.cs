// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Models;

internal class SearchResult
{
    public string Icon { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Description { get; set; } = null!;
    public object Tag { get; set; } = null!;
}