// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;

namespace AIDevGallery.Models;

internal record class ExpandedModelDetails(string Id, string Path, string Url, long ModelSize, HardwareAccelerator HardwareAccelerator, ExecutionProviderDevicePolicy ExecutionProviderDevicePolicy);