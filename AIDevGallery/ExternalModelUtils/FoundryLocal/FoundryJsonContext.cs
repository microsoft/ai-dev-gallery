using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(FoundryCatalogModel))]
[JsonSerializable(typeof(List<FoundryCatalogModel>))]
internal partial class FoundryJsonContext : JsonSerializerContext
{
}