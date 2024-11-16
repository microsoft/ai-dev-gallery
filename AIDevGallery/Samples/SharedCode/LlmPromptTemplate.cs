// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Samples.SharedCode;

internal class LlmPromptTemplate
{
    public string? System { get; init; }
    public string? User { get; init; }
    public string? Assistant { get; init; }
    public string[]? Stop { get; init; }
}