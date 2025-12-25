// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDevGallery.Models;

internal class GenAIConfig
{
    [JsonPropertyName("model")]
    public required GenAIModelInfo Model { get; set; }
}

internal class GenAIModelInfo
{
    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; }
    [JsonPropertyName("decoder")]
    public required Decoder Decoder { get; set; }
    [JsonPropertyName("vocab_size")]
    public int VocabSize { get; set; }
}

internal class Decoder
{
    [JsonPropertyName("session_options")]
    public required GenAISessionOptions SessionOptions { get; set; }
    [JsonPropertyName("pipeline")]
    public PipelineItem[]? Pipeline { get; set; }
}

internal class GenAISessionOptions
{
    [JsonPropertyName("provider_options")]
    public required ProviderOptions[] ProviderOptions { get; set; }
}

internal class PipelineItem
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Stages { get; set; }
}

internal class PipelineStage
{
    [JsonPropertyName("session_options")]
    public GenAISessionOptions? SessionOptions { get; set; }
}

internal class ProviderOptions
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public bool HasProvider(string name)
    {
        if (ExtensionData == null)
        {
            return false;
        }

        return ExtensionData.Keys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public Dictionary<string, string>? GetProviderOptions(string name)
    {
        if (ExtensionData == null)
        {
            return null;
        }

        var key = ExtensionData.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (key == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(ExtensionData[key].GetRawText(), AIDevGallery.Utils.SourceGenerationContext.Default.DictionaryStringString);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}