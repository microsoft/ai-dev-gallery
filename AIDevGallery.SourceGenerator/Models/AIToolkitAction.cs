// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace AIDevGallery.SourceGenerator.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AIToolkitAction>))]
internal enum AIToolkitAction
{
    FineTuning,
    PromptBuilder,
    BulkRun,
    Playground
}