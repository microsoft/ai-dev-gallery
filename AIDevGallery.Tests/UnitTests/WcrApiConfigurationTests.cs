// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Unit tests for WCR API configuration completeness.
// These tests verify that WcrApiHelpers' internal registration tables
// (CompatibilityCheckers, EnsureReadyFuncs, ImageGeneratorBacked, LanguageModelBacked)
// are consistent with each other and with the source-generated ApiDefinitionDetails.
//
// Purpose: Catch configuration omissions when adding new WCR API model types.
// For example, registering a new ModelType in CompatibilityCheckers but forgetting
// to add it to EnsureReadyFuncs would cause a KeyNotFoundException at runtime.
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Windows.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class WcrApiConfigurationTests
{
    private static Dictionary<ModelType, Func<AIFeatureReadyState>> GetCompatibilityCheckers()
    {
        var field = typeof(WcrApiHelpers).GetField(
            "CompatibilityCheckers",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "CompatibilityCheckers field should exist");
        var value = field.GetValue(null) as Dictionary<ModelType, Func<AIFeatureReadyState>>;
        Assert.IsNotNull(value, "CompatibilityCheckers should not be null");
        return value;
    }

    private static HashSet<ModelType> GetImageGeneratorBacked()
    {
        var field = typeof(WcrApiHelpers).GetField(
            "ImageGeneratorBacked",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "ImageGeneratorBacked field should exist");
        var value = field.GetValue(null) as HashSet<ModelType>;
        Assert.IsNotNull(value, "ImageGeneratorBacked should not be null");
        return value;
    }

    private static HashSet<ModelType> GetLanguageModelBacked()
    {
        var field = typeof(WcrApiHelpers).GetField(
            "LanguageModelBacked",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "LanguageModelBacked field should exist");
        var value = field.GetValue(null) as HashSet<ModelType>;
        Assert.IsNotNull(value, "LanguageModelBacked should not be null");
        return value;
    }

    [TestMethod]
    public void CompatibilityCheckersAndEnsureReadyFuncsHaveSameKeys()
    {
        var checkerKeys = GetCompatibilityCheckers().Keys.ToHashSet();
        var ensureReadyKeys = WcrApiHelpers.EnsureReadyFuncs.Keys.ToHashSet();

        var missingFromEnsureReady = checkerKeys.Except(ensureReadyKeys).ToList();
        var missingFromCheckers = ensureReadyKeys.Except(checkerKeys).ToList();

        Assert.AreEqual(
            0,
            missingFromEnsureReady.Count,
            $"ModelTypes in CompatibilityCheckers but missing from EnsureReadyFuncs: {string.Join(", ", missingFromEnsureReady)}");

        Assert.AreEqual(
            0,
            missingFromCheckers.Count,
            $"ModelTypes in EnsureReadyFuncs but missing from CompatibilityCheckers: {string.Join(", ", missingFromCheckers)}");
    }

    /// <summary>
    /// APIs that intentionally do NOT use the standard GetReadyState/EnsureReadyAsync
    /// availability flow. Each exclusion must have a documented reason.
    ///
    /// - ACI APIs (SemanticSearch, KnowledgeRetrieval, AppIndexCapability):
    ///   Use AppContentIndexer.WaitForIndexCapabilitiesAsync() instead.
    ///   See ModelDetailsHelper.cs: "ACI is a subset of WCRAPIs but without
    ///   the same set of hardware restrictions."
    ///
    /// If you add a new API and this test fails, you likely need to register
    /// it in both CompatibilityCheckers and EnsureReadyFuncs in WcrApiHelpers.cs.
    /// Only add to this exclusion list if the API genuinely uses a different
    /// availability mechanism.
    /// </summary>
    private static readonly HashSet<ModelType> ApisExcludedFromStandardAvailabilityCheck = new()
    {
        ModelType.SemanticSearch,
        ModelType.KnowledgeRetrieval,
        ModelType.AppIndexCapability,
        ModelType.IndexStatistics,
    };

    [TestMethod]
    public void AllNonExcludedApiDefinitionsHaveCompatibilityCheckers()
    {
        var apiTypes = ModelTypeHelpers.ApiDefinitionDetails.Keys
            .Except(ApisExcludedFromStandardAvailabilityCheck)
            .ToHashSet();
        var checkerKeys = GetCompatibilityCheckers().Keys.ToHashSet();

        var missing = apiTypes.Except(checkerKeys).ToList();

        Assert.AreEqual(
            0,
            missing.Count,
            $"API ModelTypes missing from CompatibilityCheckers (add to checkers or to exclusion list with reason): {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void AllNonExcludedApiDefinitionsHaveEnsureReadyFuncs()
    {
        var apiTypes = ModelTypeHelpers.ApiDefinitionDetails.Keys
            .Except(ApisExcludedFromStandardAvailabilityCheck)
            .ToHashSet();
        var ensureReadyKeys = WcrApiHelpers.EnsureReadyFuncs.Keys.ToHashSet();

        var missing = apiTypes.Except(ensureReadyKeys).ToList();

        Assert.AreEqual(
            0,
            missing.Count,
            $"API ModelTypes missing from EnsureReadyFuncs (add to funcs or to exclusion list with reason): {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void ImageGeneratorBackedIsSubsetOfCompatibilityCheckers()
    {
        var imageGeneratorBacked = GetImageGeneratorBacked();
        var checkerKeys = GetCompatibilityCheckers().Keys.ToHashSet();

        var missingFromCheckers = imageGeneratorBacked.Except(checkerKeys).ToList();

        Assert.AreEqual(
            0,
            missingFromCheckers.Count,
            $"ModelTypes in ImageGeneratorBacked but missing from CompatibilityCheckers: {string.Join(", ", missingFromCheckers)}");
    }

    [TestMethod]
    public void LanguageModelBackedIsSubsetOfCompatibilityCheckers()
    {
        var languageModelBacked = GetLanguageModelBacked();
        var checkerKeys = GetCompatibilityCheckers().Keys.ToHashSet();

        var missingFromCheckers = languageModelBacked.Except(checkerKeys).ToList();

        Assert.AreEqual(
            0,
            missingFromCheckers.Count,
            $"ModelTypes in LanguageModelBacked but missing from CompatibilityCheckers: {string.Join(", ", missingFromCheckers)}");
    }

    [TestMethod]
    public void ImageGeneratorAndLanguageModelBackedDoNotOverlap()
    {
        var imageGeneratorBacked = GetImageGeneratorBacked();
        var languageModelBacked = GetLanguageModelBacked();

        var overlap = imageGeneratorBacked.Intersect(languageModelBacked).ToList();

        Assert.AreEqual(
            0,
            overlap.Count,
            $"ModelTypes should not be in both ImageGeneratorBacked and LanguageModelBacked: {string.Join(", ", overlap)}");
    }

    [TestMethod]
    public void GetStringDescriptionReturnsNonEmptyForNotSupported()
    {
        var allCheckerKeys = GetCompatibilityCheckers().Keys;

        foreach (var modelType in allCheckerKeys)
        {
            var description = WcrApiHelpers.GetStringDescription(
                modelType,
                AIFeatureReadyState.NotSupportedOnCurrentSystem);

            Assert.IsFalse(
                string.IsNullOrEmpty(description),
                $"GetStringDescription should return a non-empty message for {modelType} when NotSupportedOnCurrentSystem");
        }
    }

    [TestMethod]
    public void GetApiCompatibilityNotSupportedUsesNeutralMessaging()
    {
        foreach (var apiType in new[] { ModelType.ImageDescription, ModelType.TextRecognitionOCR, ModelType.SpeechRecognition, ModelType.VideoSuperRes })
        {
            var compatibility = ModelCompatibility.GetApiCompatibility(apiType, AIFeatureReadyState.NotSupportedOnCurrentSystem);

            Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
            Assert.AreEqual("Windows AI API unavailable", compatibility.CompatibilityIssueTitle);
            Assert.AreEqual("This Windows AI API is currently unavailable on this device.", compatibility.CompatibilityIssueDescription);
        }
    }

    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady)]
    public void GetApiCompatibilityReadyAndNotReadyRemainCompatible(AIFeatureReadyState state)
    {
        var compatibility = ModelCompatibility.GetApiCompatibility(ModelType.ImageDescription, state);

        Assert.AreEqual(ModelCompatibilityState.Compatible, compatibility.CompatibilityState);
        Assert.AreEqual(string.Empty, compatibility.CompatibilityIssueDescription);
    }

    [TestMethod]
    public void GetApiCompatibilityDisabledByUserKeepsExistingMessaging()
    {
        foreach (var (apiType, requirement) in new[]
        {
            (ModelType.ImageDescription, "A Copilot+ PC is required."),
            (ModelType.PhiSilica, "A Copilot+ PC, or a Windows 11 PC with a supported GPU, is required."),
            (ModelType.SpeechRecognition, "A Copilot+ PC, or a Windows 11 PC with a supported CPU, is required.")
        })
        {
            var compatibility = ModelCompatibility.GetApiCompatibility(apiType, AIFeatureReadyState.DisabledByUser);

            Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
            Assert.AreEqual("This device isn't supported", compatibility.CompatibilityIssueTitle);
            Assert.AreEqual($"{requirement}\nAPI is disabled by the user in the Windows settings.", compatibility.CompatibilityIssueDescription);
        }
    }

    [TestMethod]
    public void GetApiCompatibilityUnknownStateKeepsExistingMessaging()
    {
        var compatibility = ModelCompatibility.GetApiCompatibility(ModelType.ImageDescription, (AIFeatureReadyState)int.MaxValue);

        Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
        Assert.AreEqual("This device isn't supported", compatibility.CompatibilityIssueTitle);
        Assert.AreEqual("A Copilot+ PC is required.\n", compatibility.CompatibilityIssueDescription);
    }

    [TestMethod]
    public void GetApiCompatibilityNotSupportedPreservesInsiderGuidance()
    {
        foreach (var apiType in GetImageGeneratorBacked())
        {
            var compatibility = ModelCompatibility.GetApiCompatibility(apiType, AIFeatureReadyState.NotSupportedOnCurrentSystem);

            Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
            Assert.AreEqual("Windows AI API unavailable", compatibility.CompatibilityIssueTitle);
            Assert.AreEqual(
                "This Windows AI API is currently unavailable on this device. This feature is only available on devices enrolled in the Windows Insider Program (Dev or Beta channel).",
                compatibility.CompatibilityIssueDescription);
        }
    }

    [TestMethod]
    public void GetApiCompatibilityNotSupportedPreservesLanguageModelDiagnostics()
    {
        var expectedDescription = LimitedAccessFeaturesHelper.IsAILanguageModelAvailable()
            ? "This Windows AI API is currently unavailable on this device."
            : LimitedAccessFeaturesHelper.GetCurrentExtendedStatusCode();

        foreach (var apiType in GetLanguageModelBacked())
        {
            var compatibility = ModelCompatibility.GetApiCompatibility(apiType, AIFeatureReadyState.NotSupportedOnCurrentSystem);

            Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
            Assert.AreEqual("Windows AI API unavailable", compatibility.CompatibilityIssueTitle);
            Assert.AreEqual(expectedDescription, compatibility.CompatibilityIssueDescription);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void GetModelCompatibilityAvailabilityExceptionUsesNeutralMessaging()
    {
        var checkers = GetCompatibilityCheckers();
        var originalChecker = checkers[ModelType.ImageDescription];

        try
        {
            checkers[ModelType.ImageDescription] = () => throw new InvalidOperationException("Readiness check failed.");
            var model = ModelDetailsHelper.GetModelDetailsFromApiDefinition(
                ModelType.ImageDescription,
                ModelTypeHelpers.ApiDefinitionDetails[ModelType.ImageDescription]);

            var compatibility = ModelCompatibility.GetModelCompatibility(model);

            Assert.AreEqual(ModelCompatibilityState.NotCompatible, compatibility.CompatibilityState);
            Assert.AreEqual("Windows AI API unavailable", compatibility.CompatibilityIssueTitle);
            Assert.AreEqual("This Windows AI API is currently unavailable on this device.", compatibility.CompatibilityIssueDescription);
        }
        finally
        {
            checkers[ModelType.ImageDescription] = originalChecker;
        }
    }

    [TestMethod]
    public void GetHardwareRequirementInfoLanguageModelBackedMentionsGpu()
    {
        foreach (var modelType in GetLanguageModelBacked())
        {
            var (requirement, _) = WcrApiHelpers.GetHardwareRequirementInfo(modelType);

            StringAssert.Contains(
                requirement,
                "GPU",
                $"LanguageModel-backed {modelType} also runs on GPU, so its requirement should mention GPU.");
        }
    }

    [TestMethod]
    public void GetHardwareRequirementInfoSpeechAndVsrMentionCpu()
    {
        foreach (var modelType in new[] { ModelType.SpeechRecognition, ModelType.VideoSuperRes })
        {
            var (requirement, _) = WcrApiHelpers.GetHardwareRequirementInfo(modelType);

            StringAssert.Contains(
                requirement,
                "CPU",
                $"{modelType} also runs on CPU, so its requirement should mention CPU.");
        }
    }

    [TestMethod]
    public void GetHardwareRequirementInfoNpuOnlyFallsBackToDefault()
    {
        // OCR has no SupportedHardwareAccelerators in apis.json, so it is NPU-only and should
        // fall back to the generic Copilot+ PC requirement rather than mention GPU or CPU.
        var (requirement, learnMoreUri) = WcrApiHelpers.GetHardwareRequirementInfo(ModelType.TextRecognitionOCR);

        Assert.AreEqual("A Copilot+ PC is required.", requirement);
        StringAssert.Contains(learnMoreUri, "supported-hardware");
    }

    [TestMethod]
    public void GetHardwareRequirementInfoAllApisHaveValidLearnMoreUri()
    {
        foreach (var modelType in ModelTypeHelpers.ApiDefinitionDetails.Keys)
        {
            var (_, learnMoreUri) = WcrApiHelpers.GetHardwareRequirementInfo(modelType);

            Assert.IsTrue(
                Uri.TryCreate(learnMoreUri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                $"GetHardwareRequirementInfo should return a valid https URL for {modelType}, got '{learnMoreUri}'.");
        }
    }
}