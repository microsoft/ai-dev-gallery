// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Samples.SharedCode;
using ColorCode.Common;
using ColorCode.Styling;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DXCore;

namespace AIDevGallery.Utils;

internal static class AppUtils
{
    private static readonly Guid DXCORE_ADAPTER_ATTRIBUTE_D3D12_GENERIC_ML = new(0xb71b0d41, 0x1088, 0x422f, 0xa2, 0x7c, 0x2, 0x50, 0xb7, 0xd3, 0xa9, 0x88);
    private static bool? _hasNpu;

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

    public static long StringToFileSize(string fileSizeString)
    {
        if (string.IsNullOrWhiteSpace(fileSizeString))
        {
            return 0;
        }

        // Define multipliers for various units (using base-2 for file sizes)
        var multipliers = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "B", 1L },
            { "KB", 1024L },
            { "MB", 1024L * 1024L },
            { "GB", 1024L * 1024L * 1024L },
            { "TB", 1024L * 1024L * 1024L * 1024L },
            { "PB", 1024L * 1024L * 1024L * 1024L * 1024L }
        };

        // Use regex to extract numeric and unit parts.
        // The regex expects an optional space between the number and the unit.
        var match = Regex.Match(fileSizeString.Trim(), @"^(?<number>[\d\.]+)\s*(?<unit>[a-zA-Z]+)$");
        if (!match.Success)
        {
            return 0;
        }

        string numberPart = match.Groups["number"].Value;
        string unitPart = match.Groups["unit"].Value;

        if (!double.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
        {
            return 0;
        }

        if (!multipliers.TryGetValue(unitPart, out long multiplier))
        {
            return 0;
        }

        double bytes = number * multiplier;
        if (bytes > long.MaxValue)
        {
            return 0;
        }

        return (long)bytes;
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

    public static string GetModelTypeStringFromHardwareAccelerators(List<HardwareAccelerator> hardwareAccelerators)
    {
        if (hardwareAccelerators.Count == 0)
        {
            return string.Empty;
        }

        if (hardwareAccelerators.Any(h => h == HardwareAccelerator.GPU ||
                                    h == HardwareAccelerator.DML
                                    || h == HardwareAccelerator.NPU
                                    || h == HardwareAccelerator.QNN
                                    || h == HardwareAccelerator.VitisAI
                                    || h == HardwareAccelerator.OpenVINO
                                    || h == HardwareAccelerator.CPU))
        {
            return "ONNX";
        }

        return GetHardwareAcceleratorString(hardwareAccelerators.First());
    }

    public static string GetHardwareAcceleratorString(HardwareAccelerator hardwareAccelerator)
    {
        if (ExternalModelHelper.HardwareAccelerators.Contains(hardwareAccelerator))
        {
            var name = ExternalModelHelper.GetName(hardwareAccelerator);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        switch (hardwareAccelerator)
        {
            case HardwareAccelerator.DML:
            case HardwareAccelerator.GPU:
                return "GPU";
            case HardwareAccelerator.QNN:
            case HardwareAccelerator.NPU:
                return "NPU";
            case HardwareAccelerator.ACI:
            case HardwareAccelerator.WCRAPI:
                return "Windows AI API";
            default:
                return hardwareAccelerator.ToString();
        }
    }

    public static string GetHardwareAcceleratorDescription(HardwareAccelerator hardwareAccelerator)
    {
        if (ExternalModelHelper.HardwareAccelerators.Contains(hardwareAccelerator))
        {
            var description = ExternalModelHelper.GetDescription(hardwareAccelerator);
            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }
        }

        switch (hardwareAccelerator)
        {
            default:
            case HardwareAccelerator.CPU:
                return "This model will run on CPU";
            case HardwareAccelerator.DML:
            case HardwareAccelerator.GPU:
                return "This model will run on supported GPUs with DirectML";
            case HardwareAccelerator.QNN:
            case HardwareAccelerator.NPU:
                return "This model will run on NPUs";
            case HardwareAccelerator.WCRAPI:
                return "The model used by this Windows AI API will run on NPU";
            case HardwareAccelerator.OLLAMA:
                return "The model will run locally via Ollama";
        }
    }

    public static string GetModelSourceOriginFromUrl(string url)
    {
        if (url.StartsWith("https://huggingface.co", StringComparison.OrdinalIgnoreCase))
        {
            return "This model was downloaded from Hugging Face";
        }

        if (url.StartsWith("https://github.co", StringComparison.OrdinalIgnoreCase))
        {
            return "This model was downloaded from GitHub";
        }

        if (url.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            return "This model was added by you";
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
        if (url.StartsWith("https://github", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgImageSource(new Uri($"ms-appx:///Assets/ModelIcons/GitHub{GetThemeAssetSuffix()}.svg"));
        }
        else if (url.StartsWith("local", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgImageSource(new Uri("ms-appx:///Assets/ModelIcons/Onnx.svg"));
        }
        else
        {
            if (ExternalModelHelper.IsUrlFromExternalProvider(url))
            {
                return ExternalModelHelper.GetBitmapIcon(url);
            }

            return new SvgImageSource(new Uri("ms-appx:///Assets/ModelIcons/HuggingFace.svg"));
        }
    }

    public static string GetThemeAssetSuffix()
    {
        return App.Current.RequestedTheme == ApplicationTheme.Dark ? ".dark" : ".light";
    }

    public static StyleDictionary GetCodeHighlightingStyleFromElementTheme(ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            // Adjust DefaultDark Theme to meet contrast accessibility requirements
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

    public static bool HasNpu()
    {
        if (_hasNpu.HasValue)
        {
            return _hasNpu.Value;
        }

        IDXCoreAdapterFactory adapterFactory;
        if (PInvoke.DXCoreCreateAdapterFactory(typeof(IDXCoreAdapterFactory).GUID, out var adapterFactoryObj) != HRESULT.S_OK)
        {
            throw new InvalidOperationException("Failed to create adapter factory");
        }

        adapterFactory = (IDXCoreAdapterFactory)adapterFactoryObj;

        // First try getting all GENERIC_ML devices, which is the broadest set of adapters
        // and includes both GPUs and NPUs; however, running this sample on an older build of
        // Windows may not have drivers that report GENERIC_ML.
        IDXCoreAdapterList adapterList;

        adapterFactory.CreateAdapterList([DXCORE_ADAPTER_ATTRIBUTE_D3D12_GENERIC_ML], typeof(IDXCoreAdapterList).GUID, out var adapterListObj);
        adapterList = (IDXCoreAdapterList)adapterListObj;

        // Fall back to CORE_COMPUTE if GENERIC_ML devices are not available. This is a more restricted
        // set of adapters and may filter out some NPUs.
        if (adapterList.GetAdapterCount() == 0)
        {
            adapterFactory.CreateAdapterList(
                [PInvoke.DXCORE_ADAPTER_ATTRIBUTE_D3D12_CORE_COMPUTE],
                typeof(IDXCoreAdapterList).GUID,
                out adapterListObj);
            adapterList = (IDXCoreAdapterList)adapterListObj;
        }

        if (adapterList.GetAdapterCount() == 0)
        {
            throw new InvalidOperationException("No compatible adapters found.");
        }

        // Sort the adapters by preference, with hardware and high-performance adapters first.
        ReadOnlySpan<DXCoreAdapterPreference> preferences =
        [
            DXCoreAdapterPreference.Hardware,
                DXCoreAdapterPreference.HighPerformance
        ];

        adapterList.Sort(preferences);

        List<IDXCoreAdapter> adapters = [];

        for (uint i = 0; i < adapterList.GetAdapterCount(); i++)
        {
            IDXCoreAdapter adapter;
            adapterList.GetAdapter(i, typeof(IDXCoreAdapter).GUID, out var adapterObj);
            adapter = (IDXCoreAdapter)adapterObj;

            adapter.GetPropertySize(
                DXCoreAdapterProperty.DriverDescription,
                out var descriptionSize);

            string adapterDescription;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal((int)descriptionSize);
                unsafe
                {
                    adapter.GetProperty(
                        DXCoreAdapterProperty.DriverDescription,
                        descriptionSize,
                        buffer.ToPointer());
                }

                adapterDescription = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            // Remove trailing null terminator written by DXCore.
            while (!string.IsNullOrEmpty(adapterDescription) && adapterDescription[^1] == '\0')
            {
                adapterDescription = adapterDescription[..^1];
            }

            adapters.Add(adapter);
            if (adapterDescription.Contains("NPU") || adapterDescription.Contains("AI Boost"))
            {
                _hasNpu = true;
                return true;
            }
        }

        _hasNpu = false;
        return false;
    }
}