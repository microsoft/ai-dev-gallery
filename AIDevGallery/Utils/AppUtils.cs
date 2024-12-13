// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;

namespace AIDevGallery.Utils;

internal static class AppUtils
{
    public static string GetAppVersion(int fieldCount = 4)
    {
        Package package = Package.Current;
        PackageId packageId = package.Id;
        var version = new Version(packageId.Version.Major, packageId.Version.Minor, packageId.Version.Build, packageId.Version.Revision);

        return version.ToString(fieldCount);
    }

    public static LlmPromptTemplate ToLlmPromptTemplate(this PromptTemplate template)
    {
        return new LlmPromptTemplate()
        {
            System = template.System,
            User = template.User,
            Assistant = template.Assistant,
            Stop = template.Stop
        };
    }

    public static string FileSizeToString(long bytes)
    {
        const long kiloByte = 1024;
        const long megaByte = kiloByte * 1024;
        const long gigaByte = megaByte * 1024;
        const long teraByte = gigaByte * 1024;

        if (bytes >= teraByte)
        {
            return $"{(double)bytes / teraByte:F1}TB";
        }
        else if (bytes >= gigaByte)
        {
            return $"{(double)bytes / gigaByte:F1}GB";
        }
        else if (bytes >= megaByte)
        {
            return $"{(double)bytes / megaByte:F1}MB";
        }
        else if (bytes >= kiloByte)
        {
            return $"{(double)bytes / kiloByte:F1}KB";
        }
        else
        {
            return $"{bytes} Bytes";
        }
    }

    public static string ToPerc(float perc)
    {
        return $"{Math.Round(perc, 1).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%";
    }

    public static string GetHardwareAcceleratorsString(List<HardwareAccelerator> hardwareAccelerators)
    {
        var hardwareAcceleratorsStrings = hardwareAccelerators.Select(GetHardwareAcceleratorString);
        return string.Join(", ", hardwareAcceleratorsStrings);
    }

    public static string GetHardwareAcceleratorString(HardwareAccelerator hardwareAccelerator)
    {
        switch (hardwareAccelerator)
        {
            case HardwareAccelerator.DML:
                return "GPU";
            case HardwareAccelerator.QNN:
                return "NPU";
            default:
                return hardwareAccelerator.ToString();
        }
    }

    public static string GetHardwareAcceleratorDescription(HardwareAccelerator hardwareAccelerator)
    {
        switch (hardwareAccelerator)
        {
            default:
            case HardwareAccelerator.CPU:
                return "This model will run on CPU";
            case HardwareAccelerator.DML:
                return "This model will run on supported GPUs with DirectML";
            case HardwareAccelerator.QNN:
                return "This model will run on Qualcomm NPUs";
        }
    }

    public static string GetModelSourceNameFromUrl(string url)
    {
        if (url.StartsWith("https://huggingface.co", StringComparison.InvariantCultureIgnoreCase))
        {
            return "Hugging Face";
        }

        if (url.StartsWith("https://github.co", StringComparison.InvariantCultureIgnoreCase))
        {
            return "GitHub";
        }

        return string.Empty;
    }

    public static string GetLicenseTitleFromString(string? str)
    {
        return LicenseInfo.GetLicenseInfo(str).Name;
    }

    public static string GetLicenseShortNameFromString(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return "Unknown";
        }

        return str;
    }

    public static Uri GetLicenseUrlFromModel(ModelDetails model)
    {
        var license = LicenseInfo.GetLicenseInfo(model.License);

        if (license.LicenseUrl != null)
        {
            return new Uri(license.LicenseUrl);
        }

        return new Uri(model.Url);
    }

    public static ImageSource GetModelSourceImageFromUrl(string url)
    {
        if (url.StartsWith("https://github", StringComparison.InvariantCultureIgnoreCase))
        {
            if (App.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Light)
            {
                return new SvgImageSource(new Uri("ms-appx:///Assets/ModelIcons/GitHub.light.svg"));
            }
            else
            {
                return new SvgImageSource(new Uri("ms-appx:///Assets/ModelIcons/GitHub.dark.svg"));
            }
        }
        else
        {
            return new SvgImageSource(new Uri("ms-appx:///Assets/ModelIcons/HuggingFace.svg"));
        }
    }
}