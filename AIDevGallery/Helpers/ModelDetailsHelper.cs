// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Helpers;

internal static class ModelDetailsHelper
{
    public static ModelFamily? GetFamily(this ModelDetails modelDetails)
    {
        if (ModelTypeHelpers.ModelDetails.Any(md => md.Value.Url == modelDetails.Url))
        {
            var myKey = ModelTypeHelpers.ModelDetails.FirstOrDefault(md => md.Value.Url == modelDetails.Url).Key;

            if (ModelTypeHelpers.ParentMapping.Values.Any(parent => parent.Contains(myKey)))
            {
                var parentKey = ModelTypeHelpers.ParentMapping.FirstOrDefault(parent => parent.Value.Contains(myKey)).Key;
                var parent = ModelTypeHelpers.ModelFamilyDetails[parentKey];
                return parent;
            }
        }

        return null;
    }

    public static ModelDetails GetModelDetailsFromApiDefinition(ModelType modelType, ApiDefinition apiDefinition)
    {
        return new ModelDetails
        {
            Id = apiDefinition.Id,
            Icon = apiDefinition.Icon,
            Name = apiDefinition.Name,
            HardwareAccelerators = [HardwareAccelerator.DML], // Switch to QNN(?) once PR #41 merges
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
            model1Details[modelType] = GetSamplesForModelType(modelType);
        }

        List<Dictionary<ModelType, List<ModelDetails>>> listModelDetails = [model1Details];

        if (sample.Model2Types != null)
        {
            Dictionary<ModelType, List<ModelDetails>> model2Details = [];
            foreach (ModelType modelType in sample.Model2Types)
            {
                model2Details[modelType] = GetSamplesForModelType(modelType);
            }

            listModelDetails.Add(model2Details);
        }

        return listModelDetails;

        static List<ModelDetails> GetSamplesForModelType(ModelType initialModelType)
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

            if (initialModelType == ModelType.LanguageModels && App.ModelCache != null)
            {
                var userAddedModels = App.ModelCache.Models.Where(m => m.Details.IsUserAdded).ToList();
                allModelDetails.AddRange(userAddedModels.Select(c => c.Details));
            }

            return allModelDetails;
        }
    }
}