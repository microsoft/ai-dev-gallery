// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.SourceGenerator.Models;

internal class Model
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Description { get; init; }
    [JsonConverter(typeof(SingleOrListOfHardwareAcceleratorConverter))]
    [JsonPropertyName("HardwareAccelerator")]
    public required List<HardwareAccelerator> HardwareAccelerators { get; init; }
    public bool? SupportedOnQualcomm { get; init; }
    public long? Size { get; init; }
    public string? Icon { get; init; }
    public string? ParameterSize { get; init; }
    public string? PromptTemplate { get; init; }
    public required string License { get; init; }
    [JsonConverter(typeof(SingleOrListOfStringConverter))]
    [JsonPropertyName("FileFilter")]
    public List<string>? FileFilters { get; init; }
}