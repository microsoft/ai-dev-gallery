// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models;

internal class MultiModelSampleNavigationParameters(
        string sampleId,
        string[] modelIds,
        string[] modelPaths,
        HardwareAccelerator[] hardwareAccelerators,
        LlmPromptTemplate?[] promptTemplates,
        TaskCompletionSource sampleLoadedCompletionSource,
        string? preferedEP,
        CancellationToken loadingCanceledToken)
    : BaseSampleNavigationParameters(sampleLoadedCompletionSource, loadingCanceledToken)
{
    public string[] ModelPaths { get; } = modelPaths;
    public HardwareAccelerator[] HardwareAccelerators { get; } = hardwareAccelerators;

    protected override string ChatClientModelPath => ModelPaths[0];
    protected override HardwareAccelerator ChatClientHardwareAccelerator => HardwareAccelerators[0];
    protected override LlmPromptTemplate? ChatClientPromptTemplate => promptTemplates[0];

    public override string PreferedEP => preferedEP ?? "CPU";

    internal override void SendSampleInteractionEvent(string? customInfo = null)
    {
        SampleInteractionEvent.Log(sampleId, model1Id: modelIds[0], hardwareAccelerator1: HardwareAccelerators[0], model2Id: ModelPaths[1], hardwareAccelerator2: HardwareAccelerators[1], customInfo);
    }
}