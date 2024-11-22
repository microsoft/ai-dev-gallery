// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.ML.OnnxRuntime;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class StableDiffusionConfig
{
    public enum ExecutionProvider
    {
        DirectML = 0,
        Cuda = 1,
        Cpu = 2
    }

    // default props
    public int NumInferenceSteps { get; init; } = 15;
    public ExecutionProvider ExecutionProviderTarget { get; set; } = ExecutionProvider.Cpu;
    public double GuidanceScale { get; init; } = 7.5;
    public int Height { get; init; } = 512;
    public int Width { get; init; } = 512;
    public int DeviceId { get; set; }

    public string TokenizerModelPath { get; init; } = "cliptokenizer.onnx";
    public string TextEncoderModelPath { get; set; } = string.Empty;
    public string UnetModelPath { get; set; } = string.Empty;
    public string VaeDecoderModelPath { get; set; } = string.Empty;
    public string SafetyModelPath { get; set; } = string.Empty;

    public SessionOptions GetSessionOptionsForEp(HardwareAccelerator hardwareAccelerator)
    {
        var sessionOptions = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO
        };

        ExecutionProviderTarget =
            hardwareAccelerator == HardwareAccelerator.DML ?
            ExecutionProvider.DirectML :
            ExecutionProvider.Cpu;

        switch (ExecutionProviderTarget)
        {
            case ExecutionProvider.DirectML:
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                sessionOptions.EnableMemoryPattern = false;
                sessionOptions.AppendExecutionProvider_DML(DeviceId);
                sessionOptions.AppendExecutionProvider_CPU();
                return sessionOptions;
            case ExecutionProvider.Cpu:
                sessionOptions.AppendExecutionProvider_CPU();
                return sessionOptions;
            default:
            case ExecutionProvider.Cuda:
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                // default to CUDA, fall back on CPU if CUDA is not available.
                sessionOptions.AppendExecutionProvider_CUDA(DeviceId);
                sessionOptions.AppendExecutionProvider_CPU();

                // sessionOptions = SessionOptions.MakeSessionOptionWithCudaProvider(cudaProviderOptions);
                return sessionOptions;
        }
    }
}