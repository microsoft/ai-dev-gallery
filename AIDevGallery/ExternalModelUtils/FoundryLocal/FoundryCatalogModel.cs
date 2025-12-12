// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal record FoundryCachedModelInfo(string Name, string? Id);

internal record FoundryDownloadResult(bool Success, string? ErrorMessage);

internal record FoundryCatalogModel
{
    public string Name { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Alias { get; init; } = default!;
    public long FileSizeMb { get; init; }
    public string License { get; init; } = default!;
    public string ModelId { get; init; } = default!;
    public Runtime? Runtime { get; init; }
}