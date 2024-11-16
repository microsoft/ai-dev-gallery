// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Utils;

internal class MostRecentlyUsedItem
{
    public required string DisplayName { get; set; }
    public string? SubItemId { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public required string ItemId { get; set; }
    public required MostRecentlyUsedItemType Type { get; set; }
}

internal enum MostRecentlyUsedItemType
{
    Model,
    Scenario
}