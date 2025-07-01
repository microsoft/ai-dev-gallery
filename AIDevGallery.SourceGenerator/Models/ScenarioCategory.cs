// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace AIDevGallery.SourceGenerator.Models;

internal class ScenarioCategory
{
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Description { get; init; }
    public required Dictionary<string, Scenario> Scenarios { get; init; }
}