// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.Utils;

[JsonSerializable(typeof(List<HFSearchResult>))]
[JsonSerializable(typeof(GenAIConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}