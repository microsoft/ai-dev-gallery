// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;

namespace AIDevGallery.Models;

internal record class ExpandedModelDetails(string Id, string Path, string Url, long ModelSize, HardwareAccelerator HardwareAccelerator, WinMlSampleOptions? WinMlSampleOptions = null);