// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIDevGallery.Utils;

[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<GitHubModelFileDetails>))]
[JsonSerializable(typeof(List<HuggingFaceModelFileDetails>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}