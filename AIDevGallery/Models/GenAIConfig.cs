// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
}

internal class GenAISessionOptions
{
    [JsonPropertyName("provider_options")]
    public required ProviderOptions[] ProviderOptions { get; set; }
}

internal class ProviderOptions
{
    [JsonPropertyName("dml")]
    public Dml? Dml { get; set; }
    [JsonPropertyName("cuda")]
    public Cuda? Cuda { get; set; }
}

internal class Dml
{
}

internal class Cuda
{
}