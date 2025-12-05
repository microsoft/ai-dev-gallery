// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Utils;

/// <summary>
/// Provides standard execution provider names used by ONNX Runtime.
/// These constants match the EpName values returned by OrtEpDevice.
/// </summary>
internal static class ExecutionProviderNames
{
    /// <summary>
    /// CPU Execution Provider - always available
    /// </summary>
    public const string CPU = "CPUExecutionProvider";

    /// <summary>
    /// DirectML Execution Provider - for DirectX 12 GPU acceleration on Windows
    /// </summary>
    public const string DML = "DmlExecutionProvider";

    /// <summary>
    /// Qualcomm Neural Network (QNN) Execution Provider - for Qualcomm NPU
    /// </summary>
    public const string QNN = "QNNExecutionProvider";

    /// <summary>
    /// OpenVINO Execution Provider - supports CPU, GPU, and NPU on Intel hardware
    /// </summary>
    public const string OpenVINO = "OpenVINOExecutionProvider";

    /// <summary>
    /// Vitis AI Execution Provider - for AMD/Xilinx NPU acceleration
    /// </summary>
    public const string VitisAI = "VitisAIExecutionProvider";

    /// <summary>
    /// CUDA Execution Provider - for NVIDIA GPU acceleration
    /// </summary>
    public const string CUDA = "CUDAExecutionProvider";

    /// <summary>
    /// TensorRT Execution Provider - for optimized inference on NVIDIA GPUs
    /// </summary>
    public const string TensorRT = "TensorrtExecutionProvider";

    /// <summary>
    /// NVIDIA TensorRT RTX Execution Provider
    /// </summary>
    public const string NvTensorRTRTX = "NvTensorRTRTXExecutionProvider";
}