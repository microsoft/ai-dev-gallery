// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Utils;

internal interface IExternalModelProvider
{
    string Name { get; }
    string UrlPrefix { get; }
    string Icon { get; }
    HardwareAccelerator ModelHardwareAccelerator { get; }
    List<string> NugetPackageReferences { get; }
    string ProviderDescription { get; }
    Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default);
    IChatClient? GetIChatClient(string url);
    string? IChatClientImplementationNamespace { get; }
    string? GetIChatClientString(string url);
    string? GetDetailsUrl(ModelDetails details);
    string Url { get; }
}