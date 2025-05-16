// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Helpers;

internal static class ModelDetailsHelper
{
    public static bool EqualOrParent(ModelType modelType, ModelType searchModelType)
    {
        if (modelType == searchModelType)
        {
            return true;
        }

        while (ModelTypeHelpers.ParentMapping.Values.Any(parent => parent.Contains(modelType)))
        {
            modelType = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(modelType)).Key;
            if (modelType == searchModelType)
            {
                return true;
            }
        }

        return false;
    }

    public static ModelDetails GetModelDetailsFromApiDefinition(ModelType modelType, ApiDefinition apiDefinition)
    {
        return new ModelDetails
        {
            Id = apiDefinition.Id,
            Icon = apiDefinition.Icon,
            Name = apiDefinition.Name,
            HardwareAccelerators = [HardwareAccelerator.WCRAPI],
            IsUserAdded = false,
            SupportedOnQualcomm = true,
            ReadmeUrl = apiDefinition.ReadmeUrl,
            Url = $"file://{modelType}",
            License = apiDefinition.License
        };
    }

    public static List<Dictionary<ModelType, List<ModelDetails>>> GetModelDetails(Sample sample)
    {
        Dictionary<ModelType, List<ModelDetails>> model1Details = [];
        foreach (ModelType modelType in sample.Model1Types)
        {
            model1Details[modelType] = GetModelDetailsForModelType(modelType);
        }

        List<Dictionary<ModelType, List<ModelDetails>>> listModelDetails = [model1Details];

        if (sample.Model2Types != null)
        {
            Dictionary<ModelType, List<ModelDetails>> model2Details = [];
            foreach (ModelType modelType in sample.Model2Types)
            {
                model2Details[modelType] = GetModelDetailsForModelType(modelType);
            }

            listModelDetails.Add(model2Details);
        }

        return listModelDetails;
    }

    public static Dictionary<ModelType, List<ModelDetails>> GetModelDetailsForModelTypes(List<ModelType> modelType)
    {
        Dictionary<ModelType, List<ModelDetails>> modelDetails = new();
        foreach (ModelType type in modelType)
        {
            if (!modelDetails.ContainsKey(type))
            {
                modelDetails[type] = GetModelDetailsForModelType(type);
            }
        }

        return modelDetails;
    }

    public static List<ModelDetails> GetModelDetailsForModelType(ModelType initialModelType)
    {
        Queue<ModelType> leafs = new();
        leafs.Enqueue(initialModelType);
        bool added = true;

        do
        {
            added = false;
            int initialCount = leafs.Count;

            for (int i = 0; i < initialCount; i++)
            {
                var leaf = leafs.Dequeue();
                if (ModelTypeHelpers.ParentMapping.TryGetValue(leaf, out List<ModelType>? values))
                {
                    if (values.Count > 0)
                    {
                        added = true;

                        foreach (var value in values)
                        {
                            leafs.Enqueue(value);
                        }
                    }
                    else
                    {
                        // Is API, just add back but don't mark as added
                        leafs.Enqueue(leaf);
                    }
                }
                else
                {
                    // Re-enqueue the leaf since it's actually a leaf node
                    leafs.Enqueue(leaf);
                }
            }
        }
        while (leafs.Count > 0 && added);

        var allModelDetails = new List<ModelDetails>();
        List<string> addedUserModels = new();
        foreach (var modelType in leafs.ToList())
        {
            if (ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out ModelDetails? modelDetails))
            {
                allModelDetails.Add(modelDetails);
            }
            else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(modelType, out ApiDefinition? apiDefinition))
            {
                allModelDetails.Add(GetModelDetailsFromApiDefinition(modelType, apiDefinition));
            }
        }

        if (initialModelType != ModelType.LanguageModels && App.AppData != null && App.AppData.TryGetUserAddedModelIds(initialModelType, out List<string>? modelIds))
        {
            foreach (string id in modelIds!)
            {
                ModelDetails? details = App.ModelCache.Models.Where(m => m.Details.Id == id).FirstOrDefault()?.Details;
                if (!addedUserModels.Contains(id) && details != null)
                {
                    allModelDetails.Add(details);
                    addedUserModels.Add(id);
                }
            }
        }
        else if (initialModelType == ModelType.LanguageModels && App.ModelCache != null)
        {
            var userAddedModels = App.ModelCache.Models.Where(m => m.Details.Id.StartsWith("useradded-local-languagemodel", System.StringComparison.OrdinalIgnoreCase)).ToList();
            allModelDetails.AddRange(userAddedModels.Select(c => c.Details));
        }

        return allModelDetails;
    }

    public static bool IsApi(this ModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI) ||
               modelDetails.IsHttpApi() ||
               modelDetails.Size == 0;
    }

    public static bool IsHttpApi(this ModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerators.Any(h => ExternalModelHelper.HardwareAccelerators.Contains(h));
    }

    public static bool IsApi(this ExpandedModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerator == HardwareAccelerator.WCRAPI ||
            modelDetails.IsHttpApi();
    }

    public static bool IsHttpApi(this ExpandedModelDetails modelDetails)
    {
        return ExternalModelHelper.HardwareAccelerators.Contains(modelDetails.HardwareAccelerator);
    }

    public static bool IsLanguageModel(this ModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.OLLAMA) ||
            modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.OPENAI) ||
            modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.FOUNDRYLOCAL) ||
            modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.LEMONADE) ||
            modelDetails.Url.StartsWith("useradded-languagemodel", System.StringComparison.InvariantCultureIgnoreCase) ||
            modelDetails.Url.StartsWith("useradded-local-languagemodel", System.StringComparison.InvariantCultureIgnoreCase) ||
            modelDetails.Url == "file://PhiSilica";
    }

    public static Visibility ShowWhenWcrApi(ModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.WCRAPI) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ShowWhenHttpApi(ModelDetails modelDetails)
    {
        return modelDetails.IsHttpApi() ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ShowWhenHttpWithSize(ModelDetails modelDetails)
    {
        return modelDetails.IsHttpApi() && modelDetails.Size != 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string GetHttpApiUrl(ModelDetails modelDetails)
    {
        return ExternalModelHelper.GetModelUrl(modelDetails) ?? string.Empty;
    }

    public static bool IsOnnxModel(this ModelDetails modelDetails)
    {
        return modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.CPU)
            || modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.DML)
            || modelDetails.HardwareAccelerators.Contains(HardwareAccelerator.QNN);
    }

    public static Visibility ShowWhenOnnxModel(ModelDetails modelDetails)
    {
        return IsOnnxModel(modelDetails) ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility ShowWhenDownloadedModel(ModelDetails modelDetails)
    {
        return IsOnnxModel(modelDetails) && !modelDetails.IsUserAdded
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public static PromptTemplate? GetTemplateFromName(string name)
    {
        switch (name.ToLower(System.Globalization.CultureInfo.InvariantCulture))
        {
            case string p when p.Contains("phi"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Phi3];
            case string d when d.Contains("deepseek"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.DeepSeekR1];
            case string l when l.Contains("llama") || l.Contains("nemotron"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Llama3];
            case string m when m.Contains("mistral"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Mistral];
            case string q when q.Contains("qwen"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Qwen];
            case string g when g.Contains("gemma"):
                return Samples.PromptTemplateHelpers.PromptTemplates[PromptTemplateType.Gemma];
            default:
                return null;
        }
    }
}