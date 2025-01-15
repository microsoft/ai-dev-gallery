// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using ColorCode.Common;
using ColorCode.Styling;
using Microsoft.UI.Xaml;
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
            default:
            case HardwareAccelerator.CPU:
                return "CPU";
            case HardwareAccelerator.DML:
                return "GPU";
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
                return "This model will run on GPU with DirectML";
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

    public static StyleDictionary GetCodeHighlightingStyleFromElementTheme(ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            StyleDictionary darkStyles = StyleDictionary.DefaultDark;
            darkStyles[ScopeName.Comment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDocComment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDocTag].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlComment].Foreground = StyleDictionary.BrightGreen;
            darkStyles[ScopeName.XmlDelimiter].Foreground = StyleDictionary.White;
            darkStyles[ScopeName.Keyword].Foreground = "#FF41D6FF";
            darkStyles[ScopeName.String].Foreground = "#FFFFB100";
            darkStyles[ScopeName.XmlAttributeValue].Foreground = "#FF41D6FF";
            darkStyles[ScopeName.XmlAttributeQuotes].Foreground = "#FF41D6FF";
            return darkStyles;
        }
        else
        {
            StyleDictionary lightStyles = StyleDictionary.DefaultLight;
            lightStyles[ScopeName.XmlDocComment].Foreground = "#FF006828";
            lightStyles[ScopeName.XmlDocTag].Foreground = "#FF006828";
            lightStyles[ScopeName.Comment].Foreground = "#FF006828";
            lightStyles[ScopeName.XmlAttribute].Foreground = "#FFB5004D";
            lightStyles[ScopeName.XmlName].Foreground = "#FF400000";
            return lightStyles;
        }
    }
}