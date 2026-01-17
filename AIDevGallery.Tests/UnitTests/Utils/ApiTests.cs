// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Unit tests for API utility classes that build and validate URLs.
// Tests focus on URL construction logic without making actual HTTP calls.
//
// Test coverage:
// - HuggingFace API URL building and validation
// - GitHub API URL building and validation
// - Input validation and error handling
// - URL format conversion (blob to resolve/raw)
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests.Utils;

/// <summary>
/// Mock HTTP handler for testing API calls
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

[TestClass]
public class HuggingFaceApiTests
{
    [TestMethod]
    public async Task GetContentsOfTextFileWithInvalidModelIdShouldThrowArgumentException()
    {
        // Arrange
        var invalidModelId = "invalid-model-id-without-slash";
        var filePath = "README.md";

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await HuggingFaceApi.GetContentsOfTextFile(invalidModelId, filePath));
    }

    [TestMethod]
    public void GetContentsOfTextFileBuildsCorrectUrl()
    {
        // Arrange
        var modelId = "microsoft/Phi-3-mini-4k-instruct";
        var filePath = "model.onnx";
        var commitOrBranch = "main";

        // Act - Build the URL that would be used
        var parts = modelId.Split('/');
        var actualUrl = HuggingFaceUrl.BuildResolveUrl(parts[0], parts[1], commitOrBranch, filePath);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/resolve/main/model.onnx", actualUrl);
    }

    [TestMethod]
    public void GetContentsOfTextFileWithFileUrlBuildsCorrectResolveUrl()
    {
        // Arrange
        var fileUrl = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/README.md";
        var url = new HuggingFaceUrl(fileUrl);

        // Act - Build the resolve URL that would be used
        var actualUrl = HuggingFaceUrl.BuildResolveUrl(url.Organization, url.Repo, url.Ref, url.Path!);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/resolve/main/README.md", actualUrl);
    }
}

[TestClass]
public class GithubApiTests
{
    [TestMethod]
    public void GetContentsOfTextFileBuildsCorrectRawUrl()
    {
        // Arrange
        var fileUrl = "https://github.com/microsoft/onnxruntime/blob/main/README.md";
        var url = new GitHubUrl(fileUrl);

        // Act - Build the URL that would be used
        var actualUrl = GitHubUrl.BuildRawUrl(url.Organization, url.Repo, url.Ref, url.Path!);

        // Assert
        Assert.AreEqual("https://raw.githubusercontent.com/microsoft/onnxruntime/main/README.md", actualUrl);
    }

    [TestMethod]
    public async Task GetContentsOfTextFileWithInvalidUrlShouldThrowException()
    {
        // Arrange
        var invalidUrl = "https://invalid-domain.com/some/path";

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await GithubApi.GetContentsOfTextFile(invalidUrl));
    }

    [TestMethod]
    public void GetContentsOfTextFileWithNestedPathBuildsCorrectUrl()
    {
        // Arrange
        var fileUrl = "https://github.com/microsoft/onnxruntime/blob/v1.16.0/docs/execution-providers/DirectML-ExecutionProvider.md";
        var url = new GitHubUrl(fileUrl);

        // Act
        var actualUrl = GitHubUrl.BuildRawUrl(url.Organization, url.Repo, url.Ref, url.Path!);

        // Assert
        Assert.AreEqual("https://raw.githubusercontent.com/microsoft/onnxruntime/v1.16.0/docs/execution-providers/DirectML-ExecutionProvider.md", actualUrl);
    }
}