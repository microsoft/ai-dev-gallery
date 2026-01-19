// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Unit tests for model URL parsing and manipulation classes.
// Tests URL parsing, construction, and local path mapping without network calls.
//
// Test coverage:
// - HuggingFaceUrl: Parse various URL formats, build API/resolve/tree/blob URLs, local path mapping
// - GitHubUrl: Parse various URL formats, build API/raw/blob URLs, local path mapping
// - Edge cases: whitespace handling, invalid formats, empty strings
// - Error handling: Validate proper exception throwing for malformed URLs
using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests.Utils;

[TestClass]
public class HuggingFaceUrlTests
{
    [TestMethod]
    public void BuildRepoUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";

        // Act
        var result = HuggingFaceUrl.BuildRepoUrl(organization, repo);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct", result);
    }

    [TestMethod]
    public void BuildTreeUrlWithoutPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";

        // Act
        var result = HuggingFaceUrl.BuildTreeUrl(organization, repo, reference);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/tree/main", result);
    }

    [TestMethod]
    public void BuildTreeUrlWithPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";
        var path = "onnx/model.onnx";

        // Act
        var result = HuggingFaceUrl.BuildTreeUrl(organization, repo, reference, path);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/tree/main/onnx/model.onnx", result);
    }

    [TestMethod]
    public void BuildResolveUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";
        var filePath = "model.onnx";

        // Act
        var result = HuggingFaceUrl.BuildResolveUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/resolve/main/model.onnx", result);
    }

    [TestMethod]
    public void BuildResolveUrlWithNestedPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";
        var filePath = "onnx/directml/model.onnx";

        // Act
        var result = HuggingFaceUrl.BuildResolveUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/resolve/main/onnx/directml/model.onnx", result);
    }

    [TestMethod]
    public void BuildBlobUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";
        var filePath = "README.md";

        // Act
        var result = HuggingFaceUrl.BuildBlobUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/README.md", result);
    }

    [TestMethod]
    public void BuildApiUrlWithoutPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";

        // Act
        var result = HuggingFaceUrl.BuildApiUrl(organization, repo, reference);

        // Assert
        Assert.AreEqual("https://huggingface.co/api/models/microsoft/Phi-3-mini-4k-instruct/tree/main", result);
    }

    [TestMethod]
    public void BuildApiUrlWithPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "Phi-3-mini-4k-instruct";
        var reference = "main";
        var path = "onnx";

        // Act
        var result = HuggingFaceUrl.BuildApiUrl(organization, repo, reference, path);

        // Assert
        Assert.AreEqual("https://huggingface.co/api/models/microsoft/Phi-3-mini-4k-instruct/tree/main/onnx", result);
    }

    [TestMethod]
    public void ConstructorWithResolveUrlShouldParseCorrectly()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/resolve/main/model.onnx";

        // Act
        var hfUrl = new HuggingFaceUrl(url);

        // Assert
        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("Phi-3-mini-4k-instruct", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.AreEqual("model.onnx", hfUrl.Path);
        Assert.IsTrue(hfUrl.IsFile);
    }

    [TestMethod]
    public void GetUrlRootShouldReturnCorrectUrl()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/tree/main";
        var hfUrl = new HuggingFaceUrl(url);

        // Act
        var result = hfUrl.GetUrlRoot();

        // Assert
        Assert.AreEqual("https://huggingface.co/microsoft/Phi-3-mini-4k-instruct", result);
    }

    [TestMethod]
    public void ConstructorWithModelIdOnlyShouldParseCorrectly()
    {
        // Arrange
        var modelId = "microsoft/Phi-3-mini-4k-instruct";

        // Act
        var hfUrl = new HuggingFaceUrl(modelId);

        // Assert
        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("Phi-3-mini-4k-instruct", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.IsNull(hfUrl.Path);
        Assert.IsFalse(hfUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithTreeUrlShouldParseCorrectly()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/tree/main/onnx";

        // Act
        var hfUrl = new HuggingFaceUrl(url);

        // Assert
        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("Phi-3-mini-4k-instruct", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.AreEqual("onnx", hfUrl.Path);
        Assert.IsFalse(hfUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithBlobUrlShouldParseAsFile()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/README.md";

        // Act
        var hfUrl = new HuggingFaceUrl(url);

        // Assert
        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("Phi-3-mini-4k-instruct", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.AreEqual("README.md", hfUrl.Path);
        Assert.IsTrue(hfUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithEmptyStringShouldThrowException()
    {
        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new HuggingFaceUrl(string.Empty));
    }

    [TestMethod]
    public void ConstructorWithNonHuggingFaceUrlShouldThrowException()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime";

        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new HuggingFaceUrl(url));
    }

    [TestMethod]
    public void ConstructorWithUrlHavingWhitespaceShouldTrimAndParse()
    {
        // Arrange
        var url = "  microsoft/Phi-3-mini-4k-instruct  ";

        // Act
        var hfUrl = new HuggingFaceUrl(url);

        // Assert
        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("Phi-3-mini-4k-instruct", hfUrl.Repo);
    }

    [TestMethod]
    public void ConstructorWithInvalidFormatShouldThrowException()
    {
        // Arrange - Missing organization or repo
        var url = "microsoft";

        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new HuggingFaceUrl(url));
    }

    [TestMethod]
    public void GetLocalPathWithFileUrlShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/onnx/model.onnx";
        var hfUrl = new HuggingFaceUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = hfUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--Phi-3-mini-4k-instruct\\main\\onnx", result);
    }

    [TestMethod]
    public void GetLocalPathWithDirectoryUrlShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/tree/main/onnx";
        var hfUrl = new HuggingFaceUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = hfUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--Phi-3-mini-4k-instruct\\main\\onnx", result);
    }

    [TestMethod]
    public void GetLocalPathWithNestedPathShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct/blob/main/onnx/directml/model.onnx";
        var hfUrl = new HuggingFaceUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = hfUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--Phi-3-mini-4k-instruct\\main\\onnx\\directml", result);
    }

    [TestMethod]
    public void GetLocalPathWithNoPathShouldReturnCorrectPath()
    {
        // Arrange
        var url = "microsoft/Phi-3-mini-4k-instruct";
        var hfUrl = new HuggingFaceUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = hfUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--Phi-3-mini-4k-instruct\\main", result);
    }
}

[TestClass]
public class GitHubUrlTests
{
    [TestMethod]
    public void BuildRepoUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";

        // Act
        var result = GitHubUrl.BuildRepoUrl(organization, repo);

        // Assert
        Assert.AreEqual("https://github.com/microsoft/onnxruntime", result);
    }

    [TestMethod]
    public void BuildApiUrlWithoutPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";
        var reference = "main";

        // Act
        var result = GitHubUrl.BuildApiUrl(organization, repo, reference);

        // Assert
        Assert.AreEqual("https://api.github.com/repos/microsoft/onnxruntime/contents?ref=main", result);
    }

    [TestMethod]
    public void BuildApiUrlWithPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";
        var reference = "main";
        var path = "docs/execution-providers";

        // Act
        var result = GitHubUrl.BuildApiUrl(organization, repo, reference, path);

        // Assert
        Assert.AreEqual("https://api.github.com/repos/microsoft/onnxruntime/contents/docs/execution-providers?ref=main", result);
    }

    [TestMethod]
    public void BuildRawUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";
        var reference = "main";
        var filePath = "README.md";

        // Act
        var result = GitHubUrl.BuildRawUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://raw.githubusercontent.com/microsoft/onnxruntime/main/README.md", result);
    }

    [TestMethod]
    public void BuildRawUrlWithNestedPathShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";
        var reference = "v1.16.0";
        var filePath = "docs/execution-providers/DirectML-ExecutionProvider.md";

        // Act
        var result = GitHubUrl.BuildRawUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://raw.githubusercontent.com/microsoft/onnxruntime/v1.16.0/docs/execution-providers/DirectML-ExecutionProvider.md", result);
    }

    [TestMethod]
    public void BuildBlobUrlShouldReturnCorrectUrl()
    {
        // Arrange
        var organization = "microsoft";
        var repo = "onnxruntime";
        var reference = "main";
        var filePath = "LICENSE";

        // Act
        var result = GitHubUrl.BuildBlobUrl(organization, repo, reference, filePath);

        // Assert
        Assert.AreEqual("https://github.com/microsoft/onnxruntime/blob/main/LICENSE", result);
    }

    [TestMethod]
    public void GetUrlRootShouldReturnCorrectUrl()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/tree/main/docs";
        var ghUrl = new GitHubUrl(url);

        // Act
        var result = ghUrl.GetUrlRoot();

        // Assert
        Assert.AreEqual("https://github.com/microsoft/onnxruntime", result);
    }

    [TestMethod]
    public void ConstructorWithValidUrlShouldParseCorrectly()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/tree/main/docs";

        // Act
        var ghUrl = new GitHubUrl(url);

        // Assert
        Assert.AreEqual("microsoft", ghUrl.Organization);
        Assert.AreEqual("onnxruntime", ghUrl.Repo);
        Assert.AreEqual("main", ghUrl.Ref);
        Assert.AreEqual("docs", ghUrl.Path);
        Assert.IsFalse(ghUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithBlobUrlShouldParseAsFile()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/blob/main/README.md";

        // Act
        var ghUrl = new GitHubUrl(url);

        // Assert
        Assert.AreEqual("microsoft", ghUrl.Organization);
        Assert.AreEqual("onnxruntime", ghUrl.Repo);
        Assert.AreEqual("main", ghUrl.Ref);
        Assert.AreEqual("README.md", ghUrl.Path);
        Assert.IsTrue(ghUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithInvalidUrlShouldThrowException()
    {
        // Arrange
        var url = "https://invalid-url.com/repo/name";

        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new GitHubUrl(url));
    }

    [TestMethod]
    public void ConstructorWithRepoUrlOnlyShouldParseCorrectly()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime";

        // Act
        var ghUrl = new GitHubUrl(url);

        // Assert
        Assert.AreEqual("microsoft", ghUrl.Organization);
        Assert.AreEqual("onnxruntime", ghUrl.Repo);
        Assert.AreEqual("main", ghUrl.Ref);
        Assert.IsNull(ghUrl.Path);
        Assert.IsFalse(ghUrl.IsFile);
    }

    [TestMethod]
    public void ConstructorWithUrlHavingWhitespaceShouldTrimAndParse()
    {
        // Arrange
        var url = "  https://github.com/microsoft/onnxruntime  ";

        // Act
        var ghUrl = new GitHubUrl(url);

        // Assert
        Assert.AreEqual("microsoft", ghUrl.Organization);
        Assert.AreEqual("onnxruntime", ghUrl.Repo);
    }

    [TestMethod]
    public void ConstructorWithEmptyStringShouldThrowException()
    {
        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new GitHubUrl(string.Empty));
    }

    [TestMethod]
    public void ConstructorWithNonGitHubUrlShouldThrowException()
    {
        // Arrange
        var url = "https://huggingface.co/microsoft/phi-3";

        // Act & Assert
        Assert.ThrowsExactly<System.ArgumentException>(() => new GitHubUrl(url));
    }

    [TestMethod]
    public void GetLocalPathWithFileUrlShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/blob/main/docs/README.md";
        var ghUrl = new GitHubUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = ghUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--onnxruntime\\main\\docs", result);
    }

    [TestMethod]
    public void GetLocalPathWithDirectoryUrlShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/tree/main/docs";
        var ghUrl = new GitHubUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = ghUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--onnxruntime\\main\\docs", result);
    }

    [TestMethod]
    public void GetLocalPathWithNestedPathShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime/blob/main/docs/execution-providers/DirectML.md";
        var ghUrl = new GitHubUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = ghUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--onnxruntime\\main\\docs\\execution-providers", result);
    }

    [TestMethod]
    public void GetLocalPathWithNoPathShouldReturnCorrectPath()
    {
        // Arrange
        var url = "https://github.com/microsoft/onnxruntime";
        var ghUrl = new GitHubUrl(url);
        var cacheRoot = "C:\\Cache";

        // Act
        var result = ghUrl.GetLocalPath(cacheRoot);

        // Assert
        Assert.AreEqual("C:\\Cache\\microsoft--onnxruntime\\main", result);
    }
}