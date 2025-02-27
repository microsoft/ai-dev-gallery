// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Helpers;

internal static class SamplesHelper
{
    public static List<SharedCodeEnum> GetAllSharedCode(this Sample sample, Dictionary<ModelType, ExpandedModelDetails> models)
    {
        var sharedCode = sample.SharedCode.ToList();

        bool isLanguageModel = ModelDetailsHelper.EqualOrParent(models.Keys.First(), ModelType.LanguageModels);

        if (isLanguageModel)
        {
            AddUnique(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory);
        }

        if (sharedCode.Contains(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory))
        {
            AddUnique(SharedCodeEnum.LlmPromptTemplate);
        }

        if (models.Any(m => ModelDetailsHelper.EqualOrParent(m.Key, ModelType.PhiSilica)))
        {
            AddUnique(SharedCodeEnum.PhiSilicaClient);
        }

        if (sharedCode.Contains(SharedCodeEnum.DeviceUtils))
        {
            AddUnique(SharedCodeEnum.NativeMethods);
        }

        return sharedCode;

        void AddUnique(SharedCodeEnum sharedCodeEnumToAdd)
        {
            if (!sharedCode.Contains(sharedCodeEnumToAdd))
            {
                sharedCode.Add(sharedCodeEnumToAdd);
            }
        }
    }

    public static List<string> GetAllNugetPackageReferences(this Sample sample, Dictionary<ModelType, ExpandedModelDetails> models)
    {
        var packageReferences = sample.NugetPackageReferences.ToList();

        var modelTypes = sample.Model1Types.Concat(sample.Model2Types ?? Enumerable.Empty<ModelType>())
                .Where(models.ContainsKey);

        bool isLanguageModel = modelTypes.Any(modelType => ModelDetailsHelper.EqualOrParent(modelType, ModelType.LanguageModels));

        if (isLanguageModel)
        {
            AddUnique("Microsoft.ML.OnnxRuntimeGenAI.Managed");
            AddUnique("Microsoft.ML.OnnxRuntimeGenAI.DirectML");
        }

        var sharedCode = sample.GetAllSharedCode(models);

        if (sharedCode.Contains(SharedCodeEnum.NativeMethods))
        {
            AddUnique("Microsoft.Windows.CsWin32");
        }

        return packageReferences;

        void AddUnique(string packageNameToAdd)
        {
            if (!packageReferences.Any(packageName => packageName == packageNameToAdd))
            {
                packageReferences.Add(packageNameToAdd);
            }
        }
    }

    public static Dictionary<ModelType, ExpandedModelDetails>? GetCacheModelDetailsDictionary(this Sample sample, ModelDetails?[] modelDetails)
    {
        if (modelDetails.Length == 0 || modelDetails.Length > 2)
        {
            throw new ArgumentException(modelDetails.Length == 0 ? "No model details provided" : "More than 2 model details provided");
        }

        var selectedModelDetails = modelDetails[0];
        var selectedModelDetails2 = modelDetails.Length > 1 ? modelDetails[1] : null;

        if (selectedModelDetails == null)
        {
            return null;
        }

        Dictionary<ModelType, ExpandedModelDetails> cachedModels = [];

        ExpandedModelDetails cachedModel;

        if (selectedModelDetails.Size == 0)
        {
            cachedModel = new(selectedModelDetails.Id, selectedModelDetails.Url, selectedModelDetails.Url, 0, selectedModelDetails.HardwareAccelerators.FirstOrDefault());
        }
        else
        {
            var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails.Url);
            if (realCachedModel == null)
            {
                return null;
            }

            cachedModel = new(selectedModelDetails.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize, selectedModelDetails.HardwareAccelerators.FirstOrDefault());
        }

        var cachedSampleItem = App.FindSampleItemById(cachedModel.Id);

        var model1Type = sample.Model1Types.Any(cachedSampleItem.Contains)
            ? sample.Model1Types.First(cachedSampleItem.Contains)
            : sample.Model1Types.First();
        cachedModels.Add(model1Type, cachedModel);

        if (sample.Model2Types != null)
        {
            if (selectedModelDetails2 == null)
            {
                return null;
            }

            if (selectedModelDetails2.Size == 0)
            {
                cachedModel = new(selectedModelDetails2.Id, selectedModelDetails2.Url, selectedModelDetails2.Url, 0, selectedModelDetails2.HardwareAccelerators.FirstOrDefault());
            }
            else
            {
                var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails2.Url);
                if (realCachedModel == null)
                {
                    return null;
                }

                cachedModel = new(selectedModelDetails2.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize, selectedModelDetails2.HardwareAccelerators.FirstOrDefault());
            }

            var model2Type = sample.Model2Types.Any(cachedSampleItem.Contains)
                ? sample.Model2Types.First(cachedSampleItem.Contains)
                : sample.Model2Types.First();

            cachedModels.Add(model2Type, cachedModel);
        }

        return cachedModels;
    }
}