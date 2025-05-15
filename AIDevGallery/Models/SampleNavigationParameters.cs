// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models;

internal class SampleNavigationParameters(
        string sampleId,
        string modelId,
        string modelPath,
        HardwareAccelerator hardwareAccelerator,
        LlmPromptTemplate? promptTemplate,
        TaskCompletionSource sampleLoadedCompletionSource,
        WinMlSampleOptions? winMlSampleOptions,
        CancellationToken loadingCanceledToken)
    : BaseSampleNavigationParameters(sampleLoadedCompletionSource, loadingCanceledToken)
{
    public string ModelPath { get; } = modelPath;
    public HardwareAccelerator HardwareAccelerator { get; } = hardwareAccelerator;
    public string SampleId => sampleId;

    protected override string ChatClientModelPath => ModelPath;
    protected override HardwareAccelerator ChatClientHardwareAccelerator => HardwareAccelerator;
    protected override LlmPromptTemplate? ChatClientPromptTemplate => promptTemplate;

    // temporary until samples are moved away
    public string PreferedEP => "CPU";

    public override WinMlSampleOptions WinMlSampleOptions => winMlSampleOptions ?? new WinMlSampleOptions(null, null, false);

    internal override void SendSampleInteractionEvent(string? customInfo = null)
    {
        SampleInteractionEvent.Log(sampleId, modelId, HardwareAccelerator, null, null, customInfo);
    }
}