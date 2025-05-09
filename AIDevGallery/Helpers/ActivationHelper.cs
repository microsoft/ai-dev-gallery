// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel.Activation;

namespace AIDevGallery.Helpers;

internal static class ActivationHelper
{
    public async static Task<object?> GetActivationParam(AppActivationArguments appActivationArguments)
    {
        if (appActivationArguments.Kind == ExtendedActivationKind.Protocol && appActivationArguments.Data is ProtocolActivatedEventArgs protocolArgs)
        {
            var uriComponents = protocolArgs.Uri.LocalPath.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (uriComponents?.Length > 0)
            {
                var itemId = uriComponents[0];
                string? subItemId = uriComponents.Length > 1 ? uriComponents[1] : null;

                DeepLinkActivatedEvent.Log(protocolArgs.Uri.ToString());

                if (protocolArgs.Uri.Host == "models" || protocolArgs.Uri.Host == "apis")
                {
                    var sampleModelTypes = App.FindSampleItemById(itemId);

                    if (sampleModelTypes.Count > 0)
                    {
                        return sampleModelTypes;
                    }
                }
                else if (protocolArgs.Uri.Host == "scenarios")
                {
                    Scenario? selectedScenario = App.FindScenarioById(itemId);
                    if (selectedScenario != null)
                    {
                        return selectedScenario;
                    }
                }
            }
            else if (protocolArgs.Uri.Host == "addmodel")
            {
                return await HandleAddModelCase(protocolArgs.Uri);
            }
        }
        else if (appActivationArguments.Kind == ExtendedActivationKind.ToastNotification && appActivationArguments.Data is ToastNotificationActivatedEventArgs toastArgs)
        {
            var argsSplit = toastArgs.Argument.Split('=');
            if (argsSplit.Length > 0 && argsSplit[1] != null)
            {
                var modelType = App.FindSampleItemById(argsSplit[1]);
                if (modelType.Count > 0)
                {
                    var selectedSample = ModelTypeHelpers.ParentMapping.FirstOrDefault(kv => kv.Value.Contains(modelType[0]));
                    if (selectedSample.Value != null)
                    {
                        return selectedSample.Key;
                    }
                }
            }
        }

        return null;
    }

    private static async Task<SampleNavigationArgs?> HandleAddModelCase(System.Uri uri)
    {
        var queryParams = HttpUtility.ParseQueryString(uri.Query);

        if(!queryParams.AllKeys.Contains("modelPath") || !queryParams.AllKeys.Contains("scenarioId"))
        {
            return null;
        }

        string? modelPath = queryParams["modelpath"];
        Scenario? scenario = App.FindScenarioById(queryParams["scenarioId"] ?? string.Empty);

        if(modelPath == null || scenario == null)
        {
            return null;
        }

        string adjustedPath = $"local-file:///{modelPath}";
        string directoryPath = Path.GetDirectoryName(modelPath) ?? string.Empty;
        var files = Directory.GetFiles(directoryPath);
        var config = files.Where(r => Path.GetFileName(r) == "genai_config.json").FirstOrDefault();

        ModelDetails? resultModelDetails;
        List<Sample> samples = SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType).ToList();

        if (App.ModelCache.IsModelCached(adjustedPath))
        {
            resultModelDetails = App.ModelCache.Models.Select(cm => cm.Details).Where(modelDetails => modelDetails.Url == adjustedPath).FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(config) && !string.IsNullOrEmpty(directoryPath))
        {
            HardwareAccelerator accelerator;

            try
            {
                string configContents = string.Empty;
                configContents = await File.ReadAllTextAsync(config);
                accelerator = UserAddedModelUtil.GetHardwareAcceleratorFromConfig(configContents);
            }
            catch
            {
                accelerator = HardwareAccelerator.CPU;
            }

            resultModelDetails = await UserAddedModelUtil.AddLanguageModelFromLocalFilepath(directoryPath, Path.GetFileNameWithoutExtension(modelPath), accelerator);
        }
        else
        {
            // Try Model 1 Types first
            List<ModelType> modelTypes = samples.SelectMany(s => s.Model1Types).ToList();
            resultModelDetails = await UserAddedModelUtil.AddModelFromLocalFilePath(modelPath, Path.GetFileNameWithoutExtension(modelPath), modelTypes);

            // If no matches, try Model 2 types
            if (resultModelDetails == null && samples[0].Model2Types != null)
            {
                modelTypes = samples.SelectMany(s => s.Model2Types!).ToList();
                resultModelDetails = await UserAddedModelUtil.AddModelFromLocalFilePath(modelPath, Path.GetFileNameWithoutExtension(modelPath), modelTypes);
            }
        }

        return resultModelDetails != null ? new SampleNavigationArgs(samples[0], resultModelDetails) : null;
    }
}