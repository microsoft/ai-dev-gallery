// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AIDevGallery.Utils;
#pragma warning restore IDE0130 // Namespace does not match folder structure

#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable CA1707 // Identifiers should not contain underscores
internal class HFSearchResult
{
#pragma warning disable IDE1006 // Naming Styles
    public required string _id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("author")]
    public string? Author { get; set; }
    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }
    [JsonPropertyName("likes")]
    public int Likes { get; set; }
    [JsonPropertyName("trendingScore")]
    public double TrendingScore { get; set; }
    [JsonPropertyName("_private")]
    public bool Private { get; set; }
    [JsonPropertyName("sha")]
    public string? Sha { get; set; }
    [JsonPropertyName("config")]
    public Config? Config { get; set; }
    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }
    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }
    [JsonPropertyName("pipeline_tag")]
    public string? PipelineTag { get; set; }
    [JsonPropertyName("library_name")]
    public string? LibraryName { get; set; }
    [JsonPropertyName("inference")]
    public string? Inference { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("modelId")]
    public string? ModelId { get; set; }
    [JsonPropertyName("siblings")]
    public Sibling[]? Siblings { get; set; }
    public string? Name => Id!.Split('/').LastOrDefault();
}

internal class Config
{
    [JsonPropertyName("architectures")]
    public string[]? Architectures { get; set; }
    [JsonPropertyName("auto_map")]
    public AutoMap? AutoMap { get; set; }
    [JsonPropertyName("model_type")]
    public string? ModelType { get; set; }
}

internal class AutoMap
{
    public string? AutoConfig { get; set; }
    public string? AutoModelForCausalLM { get; set; }
}

internal class Sibling
{
    [JsonPropertyName("rfilename")]
    public required string RFilename { get; set; }
}