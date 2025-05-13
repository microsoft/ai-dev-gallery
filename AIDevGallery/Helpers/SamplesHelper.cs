// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AIDevGallery.Helpers;

internal static partial class SamplesHelper
{
    public static List<SharedCodeEnum> GetAllSharedCode(this Sample sample, Dictionary<ModelType, ExpandedModelDetails> models)
    {
        var sharedCode = sample.SharedCode.ToList();

        bool isLanguageModel = ModelDetailsHelper.EqualOrParent(models.Keys.First(), ModelType.LanguageModels);

        if (isLanguageModel)
        {
            if (!models.Values.Any(m => m.IsApi()))
            {
                AddUnique(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory);
            }
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
            if (models.Values.Any(m => m.IsHttpApi()))
            {
                foreach (var m in models)
                {
                    if (m.Value.IsHttpApi())
                    {
                        var nugetPackages = ExternalModelHelper.GetPackageReferences(m.Value.HardwareAccelerator);
                        foreach (var nugetPackage in nugetPackages)
                        {
                            AddUnique(nugetPackage);
                        }
                    }
                }
            }
            else
            {
                AddUnique("Microsoft.ML.OnnxRuntimeGenAI.Managed");
                AddUnique("Microsoft.ML.OnnxRuntimeGenAI.DirectML");
            }
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

    [GeneratedRegex(@"(\s*)this.InitializeComponent\(\);")]
    private static partial Regex RegexInitializeComponent();

    [GeneratedRegex(@"(using .+;\s)+", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RegexUsingBlocks();

    private static string GetPromptTemplateString(PromptTemplate? promptTemplate, int spaceCount)
    {
        static string EscapeNewLines(string str)
        {
            str = str
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return str;
        }

        if (promptTemplate == null)
        {
            return "null";
        }

        StringBuilder modelPromptTemplateSb = new();
        var spaces = new string(' ', spaceCount);
        modelPromptTemplateSb.AppendLine("new LlmPromptTemplate");
        modelPromptTemplateSb.Append(spaces);
        modelPromptTemplateSb.AppendLine("{");
        if (!string.IsNullOrEmpty(promptTemplate.System))
        {
            modelPromptTemplateSb.Append(spaces);
            modelPromptTemplateSb.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    """
                        System = "{0}",
                    """,
                    EscapeNewLines(promptTemplate.System)));
        }

        if (!string.IsNullOrEmpty(promptTemplate.User))
        {
            modelPromptTemplateSb.Append(spaces);
            modelPromptTemplateSb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    """
                        User = "{0}",
                    """,
                    EscapeNewLines(promptTemplate.User)));
        }

        if (!string.IsNullOrEmpty(promptTemplate.Assistant))
        {
            modelPromptTemplateSb.Append(spaces);
            modelPromptTemplateSb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    """
                        Assistant = "{0}",
                    """,
                    EscapeNewLines(promptTemplate.Assistant)));
        }

        if (promptTemplate.Stop != null && promptTemplate.Stop.Length > 0)
        {
            modelPromptTemplateSb.Append(spaces);
            var stopStr = string.Join(", ", promptTemplate.Stop.Select(s =>
                string.Format(
                        CultureInfo.InvariantCulture,
                        """
                        "{0}"
                        """,
                        EscapeNewLines(s))));
            modelPromptTemplateSb.Append("    Stop = [ ");
            modelPromptTemplateSb.Append(stopStr);
            modelPromptTemplateSb.AppendLine("]");
        }

        modelPromptTemplateSb.Append(spaces);
        modelPromptTemplateSb.Append('}');

        return modelPromptTemplateSb.ToString();
    }

    private static (string? ChatClientLoaderString, string? ChatClientNamespace) GetChatClientLoaderString(List<SharedCodeEnum> sharedCode, string modelPath, string promptTemplate, bool isPhiSilica, ModelType modelType)
    {
        bool isLanguageModel = ModelDetailsHelper.EqualOrParent(modelType, ModelType.LanguageModels);
        if (!sharedCode.Contains(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory) && !isPhiSilica && !isLanguageModel)
        {
            return (null, null);
        }

        if (isPhiSilica)
        {
            return ("await PhiSilicaClient.CreateAsync()", null);
        }
        else if (ExternalModelHelper.IsUrlFromExternalProvider(modelPath[2..^1]))
        {
            return (ExternalModelHelper.GetIChatClientString(modelPath[2..^1]), ExternalModelHelper.GetIChatClientNamespace(modelPath[2..^1]));
        }

        return ($"await OnnxRuntimeGenAIChatClientFactory.CreateAsync({modelPath}, {promptTemplate})", null);
    }

    public static string GetCleanCSCode(this Sample sample, Dictionary<ModelType, (ExpandedModelDetails ExpandedModelDetails, string ModelPathStr)> modelInfos)
    {
        string cleanCsSource = sample.CSCode;

        string modelPathStr;
        if (modelInfos.Count > 1)
        {
            int i = 0;
            foreach (var modelInfo in modelInfos)
            {
                cleanCsSource = cleanCsSource.Replace($"sampleParams.HardwareAccelerators[{i}]", $"HardwareAccelerator.{modelInfo.Value.ExpandedModelDetails.HardwareAccelerator}");
                cleanCsSource = cleanCsSource.Replace($"sampleParams.WinMLExecutionProviderDevicePolicy", $"ExecutionProviderDevicePolicy.{modelInfo.Value.ExpandedModelDetails.ExecutionProviderDevicePolicy}");
                cleanCsSource = cleanCsSource.Replace($"sampleParams.ModelPaths[{i}]", modelInfo.Value.ModelPathStr);
                i++;
            }

            modelPathStr = modelInfos.First().Value.ModelPathStr;
        }
        else
        {
            var modelInfo = modelInfos.Values.First();
            cleanCsSource = cleanCsSource.Replace("sampleParams.HardwareAccelerator", $"HardwareAccelerator.{modelInfo.ExpandedModelDetails.HardwareAccelerator}");
            cleanCsSource = cleanCsSource.Replace($"sampleParams.WinMLExecutionProviderDevicePolicy", $"ExecutionProviderDevicePolicy.{modelInfo.ExpandedModelDetails.ExecutionProviderDevicePolicy}");
            cleanCsSource = cleanCsSource.Replace("sampleParams.ModelPath", modelInfo.ModelPathStr);
            modelPathStr = modelInfo.ModelPathStr;
        }

        List<SharedCodeEnum> sharedCode = sample.GetAllSharedCode(modelInfos.ToDictionary(m => m.Key, m => m.Value.ExpandedModelDetails));

        var search = "await sampleParams.GetIChatClientAsync()";
        int index = cleanCsSource.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (index > 0)
        {
            int newLineIndex = cleanCsSource[..index].LastIndexOf(Environment.NewLine, StringComparison.OrdinalIgnoreCase);
            var subStr = cleanCsSource[(newLineIndex + Environment.NewLine.Length)..];
            var subStrWithoutSpaces = subStr.TrimStart();
            var spaceCount = subStr.Length - subStrWithoutSpaces.Length;
            var modelInfo = modelInfos.First();

            PromptTemplate? modelPromptTemplate = null;
            if (ModelTypeHelpers.ModelDetails.TryGetValue(modelInfo.Key, out var modelDetails))
            {
                modelPromptTemplate = modelDetails.PromptTemplate;
            }
            else if (ModelTypeHelpers.ModelDetails.FirstOrDefault(mf => mf.Value.Url == modelInfo.Value.ExpandedModelDetails.Url) is var modelDetails2 && modelDetails2.Value != null)
            {
                modelPromptTemplate = modelDetails2.Value.PromptTemplate;
            }
            else if (App.ModelCache != null && App.ModelCache.GetCachedModel(modelInfo.Value.ExpandedModelDetails.Url) is var cachedModel && cachedModel != null)
            {
                modelPromptTemplate = cachedModel.Details.PromptTemplate;
            }

            bool isPhiSilica = ModelDetailsHelper.EqualOrParent(modelInfo.Key, ModelType.PhiSilica);
            if (isPhiSilica)
            {
                modelPathStr = modelPathStr.Replace($"@\"file://{ModelType.PhiSilica}\"", "string.Empty");
            }

            var promptTemplate = GetPromptTemplateString(modelPromptTemplate, spaceCount);
            var (chatClientLoaderString, chatClientNamespace) = GetChatClientLoaderString(sharedCode, modelPathStr, promptTemplate, modelInfos.Any(m => ModelDetailsHelper.EqualOrParent(m.Key, ModelType.PhiSilica)), modelInfo.Key);
            if (chatClientLoaderString != null)
            {
                cleanCsSource = cleanCsSource.Replace(search, chatClientLoaderString);
            }

            if (!string.IsNullOrEmpty(chatClientNamespace))
            {
                var matches = RegexUsingBlocks().Matches(cleanCsSource);
                var lastMatch = matches.LastOrDefault();
                if (lastMatch != null)
                {
                    var usingNamespaces = $"using {chatClientNamespace};";
                    cleanCsSource = cleanCsSource.Insert(lastMatch.Index + lastMatch.Length, usingNamespaces);
                }
            }
        }

        if (sharedCode.Contains(SharedCodeEnum.OnnxRuntimeGenAIChatClientFactory))
        {
            cleanCsSource = RegexInitializeComponent().Replace(cleanCsSource, $"$1this.InitializeComponent();$1OnnxRuntimeGenAIChatClientFactory.InitializeGenAI();");
        }

        return cleanCsSource;
    }

    public static Dictionary<ModelType, ExpandedModelDetails>? GetCacheModelDetailsDictionary(this Sample sample, ModelDetails?[] modelDetails, ExecutionProviderDevicePolicy executionProviderDevicePolicy = ExecutionProviderDevicePolicy.DEFAULT)
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

        if (selectedModelDetails.IsApi())
        {
            cachedModel = new(selectedModelDetails.Id, selectedModelDetails.Url, selectedModelDetails.Url, 0, selectedModelDetails.HardwareAccelerators.FirstOrDefault(), executionProviderDevicePolicy);
        }
        else
        {
            var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails.Url);
            if (realCachedModel == null)
            {
                return null;
            }

            cachedModel = new(selectedModelDetails.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize, selectedModelDetails.HardwareAccelerators.FirstOrDefault(), executionProviderDevicePolicy);
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
                cachedModel = new(selectedModelDetails2.Id, selectedModelDetails2.Url, selectedModelDetails2.Url, 0, selectedModelDetails2.HardwareAccelerators.FirstOrDefault(), executionProviderDevicePolicy);
            }
            else
            {
                var realCachedModel = App.ModelCache.GetCachedModel(selectedModelDetails2.Url);
                if (realCachedModel == null)
                {
                    return null;
                }

                cachedModel = new(selectedModelDetails2.Id, realCachedModel.Path, realCachedModel.Url, realCachedModel.ModelSize, selectedModelDetails2.HardwareAccelerators.FirstOrDefault(), executionProviderDevicePolicy);
            }

            var model2Type = sample.Model2Types.Any(cachedSampleItem.Contains)
                ? sample.Model2Types.First(cachedSampleItem.Contains)
                : sample.Model2Types.First();

            cachedModels.Add(model2Type, cachedModel);
        }

        return cachedModels;
    }
}