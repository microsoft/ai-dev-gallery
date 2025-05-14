// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using Microsoft.ML.OnnxRuntime;
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
        string? preferedEP,
        ExecutionProviderDevicePolicy? executionProviderDevicePolicy,
        CancellationToken loadingCanceledToken)
    : BaseSampleNavigationParameters(sampleLoadedCompletionSource, loadingCanceledToken)
{
    public string ModelPath { get; } = modelPath;
    public HardwareAccelerator HardwareAccelerator { get; } = hardwareAccelerator;
    public string SampleId => sampleId;

    protected override string ChatClientModelPath => ModelPath;
    protected override HardwareAccelerator ChatClientHardwareAccelerator => HardwareAccelerator;
    protected override LlmPromptTemplate? ChatClientPromptTemplate => promptTemplate;

    public override ExecutionProviderDevicePolicy WinMLExecutionProviderDevicePolicy => executionProviderDevicePolicy ?? ExecutionProviderDevicePolicy.DEFAULT;

    public override string PreferedEP => preferedEP ?? "CPU";

    internal override void SendSampleInteractionEvent(string? customInfo = null)
    {
        SampleInteractionEvent.Log(sampleId, modelId, HardwareAccelerator, null, null, customInfo);
    }
}