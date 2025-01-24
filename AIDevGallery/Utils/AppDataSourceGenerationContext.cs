// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.Utils;

[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppData))]
[JsonSerializable(typeof(List<CachedModel>))]
internal partial class AppDataSourceGenerationContext : JsonSerializerContext
{
}