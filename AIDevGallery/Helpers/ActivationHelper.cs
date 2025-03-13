// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using Microsoft.Windows.AppLifecycle;
using System.Linq;
using Windows.ApplicationModel.Activation;
using Windows.Data.Xml.Dom;

namespace AIDevGallery.Helpers;

internal static class ActivationHelper
{
    public static object? GetActivationParam(AppActivationArguments appActivationArguments)
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
}