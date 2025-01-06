// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.SourceGenerator.Models;

[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Dictionary<string, ScenarioCategory>))]
[JsonSerializable(typeof(Dictionary<string, ModelFamily>))]
[JsonSerializable(typeof(Dictionary<string, ModelGroup>))]
[JsonSerializable(typeof(Dictionary<string, ApiGroup>))]
[JsonSerializable(typeof(Dictionary<string, PromptTemplate>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}