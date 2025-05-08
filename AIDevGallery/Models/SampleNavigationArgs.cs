// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Models;

internal record SampleNavigationArgs
{
    public Sample Sample { get; private set; }
    public ModelDetails? ModelDetails { get; private set; }
    public bool? OpenCodeView { get; private set; }

    public SampleNavigationArgs(Sample sample)
    {
        Sample = sample;
    }

    public SampleNavigationArgs(Sample sample, ModelDetails? modelDetails, bool? openCodeView = false)
    {
        Sample = sample;
        ModelDetails = modelDetails;
        OpenCodeView = openCodeView;
    }
}