// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(FoundryCatalogModel))]
[JsonSerializable(typeof(List<FoundryCatalogModel>))]
[JsonSerializable(typeof(FoundryDownloadResult))]
[JsonSerializable(typeof(FoundryDownloadBody))]
internal partial class FoundryJsonContext : JsonSerializerContext
{
}