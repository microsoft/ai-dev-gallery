// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace AIDevGallery.SourceGenerator.Models;

internal class PromptTemplate
{
    [JsonPropertyName("system")]
    public string? System { get; init; }
    [JsonPropertyName("user")]
    public required string User { get; init; }
    [JsonPropertyName("assistant")]
    public string? Assistant { get; init; }
    [JsonPropertyName("stop")]
    public required string[] Stop { get; init; }
}