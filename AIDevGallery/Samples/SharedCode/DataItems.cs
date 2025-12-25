// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Samples.SharedCode;

internal record class TextDataItem
{
    public string? Id { get; set; }
    public string? Value { get; set; }
}

internal record class ImageDataItem
{
    public string? Id { get; set; }
    public string? ImageSource { get; set; }
}