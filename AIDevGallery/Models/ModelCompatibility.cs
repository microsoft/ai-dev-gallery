// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using System;
using Utils;

namespace AIDevGallery.Models;

internal class ModelCompatibility
{
    private const double BytesInGB = 1024 * 1024 * 1024;

    public ModelCompatibilityState CompatibilityState { get; init; }
    public string CompatibilityIssueDescription { get; init; } = string.Empty;

    private ModelCompatibility()
    {
    }

    public static ModelCompatibility GetModelCompatibility(ModelDetails modelDetails)
    {
        string description = string.Empty;
        ModelCompatibilityState compatibility;

        // check if WCR API
        if (modelDetails.Url.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
        {
            var apiKey = modelDetails.Url.Substring(7);
            if (WcrCompatibilityChecker.GetApiAvailability(apiKey) != WcrApiAvailability.NotSupported)
            {
                compatibility = ModelCompatibilityState.Compatible;
            }
            else
            {
                compatibility = ModelCompatibilityState.NotCompatible;
                description = "This Windows Copilot Runtime API requires a Copilot+ PC and a Windows 11 Insider Preview Build 26120.3073 (Dev and Beta Channels).";
            }
        }
        else if (modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.CPU) ||
            (modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.QNN) && DeviceUtils.IsArm64()))
        {
            compatibility = ModelCompatibilityState.Compatible;
        }
        else if (modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.DML) && !DeviceUtils.IsArm64())
        {
            var vram = DeviceUtils.GetVram();
            var minimumSizeNeeded = Math.Round((float)(modelDetails.Size / BytesInGB), 1);
            var vramInGb = Math.Round(vram / BytesInGB, 1);

            // we want at least 2GB more than model size for good performance
            if (modelDetails.Size + (2 * BytesInGB) < vram)
            {
                compatibility = ModelCompatibilityState.Compatible;
            }

            // we want at least 1GB more than model size for some breathing room
            else if (modelDetails.Size + BytesInGB < vram)
            {
                compatibility = ModelCompatibilityState.NotRecomended;
                description = $"This model is not recomended for your device. We recommend minimum {minimumSizeNeeded + 2}GB of dedicated GPU memory. Your GPU has {vramInGb}GB";
            }
            else
            {
                compatibility = ModelCompatibilityState.NotCompatible;
                description = $"This model will not work on your device because it requires minimum {minimumSizeNeeded + 1}GB of dedicate GPU memory.";
                if (vram == 0)
                {
                    description += " We could not find a dedicated GPU.";
                }
                else
                {
                    description += $" Your GPU has {vramInGb}GB.";
                }
            }
        }
        else if (modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.DML) && DeviceUtils.IsArm64())
        {
            compatibility = ModelCompatibilityState.NotCompatible;
            description = $"This model is not currently supported on Qualcomm devices.";
        }
        else if (modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.QNN) && !DeviceUtils.IsArm64())
        {
            compatibility = ModelCompatibilityState.NotCompatible;
            description = $"This model is not supported on your device and requires a Qualcomm NPU.";
        }
        else
        {
            compatibility = ModelCompatibilityState.NotCompatible;
            description = $"This model is not supported on your device.";
        }

        return new ModelCompatibility
        {
            CompatibilityState = compatibility,
            CompatibilityIssueDescription = description
        };
    }
}

internal enum ModelCompatibilityState
{
    Compatible,
    NotRecomended,
    NotCompatible
}