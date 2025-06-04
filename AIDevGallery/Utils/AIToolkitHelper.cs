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
        { AIToolkitAction.PromptBuilder, new ToolkitActionInfo() { DisplayName = "Agent (Prompt) Builder", QueryName = "open_prompt_builder" } },
        { AIToolkitAction.Playground, new ToolkitActionInfo() { DisplayName = "Playground", QueryName = "open_playground" } }
    };

    public static Dictionary<AIToolkitAction, ToolkitActionInfo> AIToolkitActionInfos
    {
        get { return aiToolkitActionInfos; }
    }

    public static string CreateAiToolkitDeeplink(this ModelDetails modelDetails, AIToolkitAction action)
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

    public static bool ValidateForFineTuning(this ModelDetails modelDetails)
    {
        return modelDetails.AIToolkitActions != null && modelDetails.AIToolkitActions.Contains(AIToolkitAction.FineTuning) && !string.IsNullOrEmpty(modelDetails.AIToolkitFinetuningId);
    }

    public static bool ValidateForGeneralToolkit(this ModelDetails modelDetails)
    {
        return modelDetails.AIToolkitActions != null && modelDetails.AIToolkitActions.Where(action => action != AIToolkitAction.FineTuning).ToList().Count > 0 && !string.IsNullOrEmpty(modelDetails.AIToolkitId);
    }

    public static bool ValidateAction(this ModelDetails modelDetails, AIToolkitAction action)
    {
        if(modelDetails.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
        {
            return false;
        }

        if(action == AIToolkitAction.FineTuning)
        {
            return modelDetails.ValidateForFineTuning();
        }
        else
        {
            return modelDetails.ValidateForGeneralToolkit();
        }
    }

    public static Dictionary<ModelDetails, List<AIToolkitAction>> GetValidatedToolkitModelDetailsToActionListDict(List<ModelDetails> modelDetailsList)
    {
        var validatedDetailsActionListMap = new Dictionary<ModelDetails, List<AIToolkitAction>>();

        foreach (ModelDetails details in modelDetailsList)
        {
            if (details.AIToolkitActions == null)
            {
                continue;
            }

            var actionsList = new List<AIToolkitAction>();
            foreach (AIToolkitAction action in details.AIToolkitActions)
            {
                if(details.ValidateAction(action))
                {
                    actionsList.Add(action);
                }
            }

            if(actionsList.Count > 0)
            {
                validatedDetailsActionListMap.Add(details, actionsList);
            }
        }

        return validatedDetailsActionListMap;
    }
}

internal class ToolkitActionInfo
{
    public required string DisplayName { get; init; }
    public required string QueryName { get; init; }
}