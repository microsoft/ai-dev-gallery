// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Utils;

internal static class AIToolkitHelper
{
    private static Dictionary<AIToolkitAction, ToolkitActionInfo> aiToolkitActionInfos = new()
    {
        { AIToolkitAction.FineTuning, new ToolkitActionInfo() { DisplayName = "Fine Tuning", QueryName = "open_fine_tuning" } },
        { AIToolkitAction.PromptBuilder, new ToolkitActionInfo() { DisplayName = "Prompt Builder", QueryName = "open_prompt_builder" } },
        { AIToolkitAction.BulkRun, new ToolkitActionInfo() { DisplayName = "Bulk Run", QueryName = "open_bulk_run" } },
        { AIToolkitAction.Playground, new ToolkitActionInfo() { DisplayName = "Playground", QueryName = "open_playground" } }
    };

    public static Dictionary<AIToolkitAction, ToolkitActionInfo> AIToolkitActionInfos
    {
        get { return aiToolkitActionInfos; }
    }

    public static string CreateAiToolkitDeeplink(AIToolkitAction action, ModelDetails modelDetails)
    {
        ToolkitActionInfo? actionInfo;
        string deeplink = "vscode://ms-windows-ai-studio.windows-ai-studio/";
        string modelId = action == AIToolkitAction.FineTuning ? modelDetails.AIToolkitFinetuningId! : modelDetails.AIToolkitId!;

        if(aiToolkitActionInfos.TryGetValue(action, out actionInfo) && !string.IsNullOrEmpty(modelId))
        {
            deeplink = deeplink + $"{actionInfo.QueryName}?model_id={modelId}&track_from=AIDevGallery";
        }

        return deeplink;
    }

    public static bool ValidateForFineTuning(ModelDetails modelDetails)
    {
        return modelDetails.AIToolkitActions!.Contains(AIToolkitAction.FineTuning) && !string.IsNullOrEmpty(modelDetails.AIToolkitFinetuningId);
    }

    public static bool ValidateForGeneralToolkit(ModelDetails modelDetails)
    {
        return modelDetails.AIToolkitActions!.Where(action => action != AIToolkitAction.FineTuning).ToList().Count > 0 && !string.IsNullOrEmpty(modelDetails.AIToolkitId);
    }

    public static bool ValidateAction(ModelDetails modelDetails, AIToolkitAction action)
    {
        if(modelDetails.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
        {
            return false;
        }

        if(action == AIToolkitAction.FineTuning)
        {
            return ValidateForFineTuning(modelDetails);
        }
        else
        {
            return ValidateForGeneralToolkit(modelDetails);
        }
    }
}

internal class ToolkitActionInfo
{
    public required string DisplayName { get; set; }
    public required string QueryName { get; set; }
}