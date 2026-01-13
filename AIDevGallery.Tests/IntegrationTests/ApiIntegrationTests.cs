// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Integration tests for API utilities that make actual HTTP calls.
// These tests verify end-to-end connectivity and content retrieval.
//
// Test coverage:
// - HuggingFace API: Fetch file contents, search models (real HTTP requests)
// - GitHub API: Fetch file contents from repositories (real HTTP requests)
//
// Note: Tests may be marked as Inconclusive if network is unavailable.
// These should not run in regular CI builds, only in integration test suites.
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests;

[TestClass]
public class HuggingFaceApiIntegrationTests
{
    [TestMethod]
    public async Task GetContentsOfTextFileWithValidModelIdAndPathShouldReturnContent()
    {
        // Note: This is an integration test that makes a real HTTP call
        var modelId = "microsoft/Phi-3-mini-4k-instruct";
        var filePath = "README.md";
        var commitOrBranch = "main";

        try
        {
            var result = await HuggingFaceApi.GetContentsOfTextFile(modelId, filePath, commitOrBranch);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0, "README content should not be empty");
        }
        catch (HttpRequestException ex)
        {
            Assert.Inconclusive($"Network request failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task GetContentsOfTextFileWithFileUrlShouldReturnContent()
    {
        // Note: This is an integration test that makes a real HTTP call
        var fileUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/README.md";

        try
        {
            var result = await HuggingFaceApi.GetContentsOfTextFile(fileUrl);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0, "README content should not be empty");
        }
        catch (HttpRequestException ex)
        {
            Assert.Inconclusive($"Network request failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task FindModelsWithValidQueryShouldReturnResults()
    {
        // Note: This is an integration test that makes a real HTTP call
        var query = "phi-3";

        try
        {
            var result = await HuggingFaceApi.FindModels(query);
            Assert.IsNotNull(result);

            // Result could be empty if no models match, so we just check it's not null
        }
        catch (HttpRequestException ex)
        {
            Assert.Inconclusive($"Network request failed: {ex.Message}");
        }
    }
}

[TestClass]
public class GithubApiIntegrationTests
{
    [TestMethod]
    public async Task GetContentsOfTextFileWithValidUrlShouldReturnContent()
    {
        // Note: This is an integration test that makes a real HTTP call
        var fileUrl = "https://github.com/microsoft/onnxruntime/blob/main/README.md";

        try
        {
            var result = await GithubApi.GetContentsOfTextFile(fileUrl);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0, "README content should not be empty");
        }
        catch (HttpRequestException ex)
        {
            Assert.Inconclusive($"Network request failed: {ex.Message}");
        }
    }
}