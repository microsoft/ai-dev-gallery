// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class ModelUrlTests
{
    [TestMethod]
    public void HuggingFaceUrl_ValidUrl_ParsesCorrectly()
    {
        var url = "https://huggingface.co/microsoft/phi-2";
        var hfUrl = new HuggingFaceUrl(url);

        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("phi-2", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.IsFalse(hfUrl.IsFile);
        Assert.AreEqual("https://huggingface.co/microsoft/phi-2/tree/main", hfUrl.FullUrl);
    }

    [TestMethod]
    public void HuggingFaceUrl_ValidUrlWithRef_ParsesCorrectly()
    {
        var url = "https://huggingface.co/microsoft/phi-2/tree/custom-ref";
        var hfUrl = new HuggingFaceUrl(url);

        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("phi-2", hfUrl.Repo);
        Assert.AreEqual("custom-ref", hfUrl.Ref);
        Assert.IsFalse(hfUrl.IsFile);
    }

    [TestMethod]
    public void HuggingFaceUrl_ValidFileUrl_ParsesCorrectly()
    {
        var url = "https://huggingface.co/microsoft/phi-2/blob/main/README.md";
        var hfUrl = new HuggingFaceUrl(url);

        Assert.AreEqual("microsoft", hfUrl.Organization);
        Assert.AreEqual("phi-2", hfUrl.Repo);
        Assert.AreEqual("main", hfUrl.Ref);
        Assert.IsTrue(hfUrl.IsFile);
        Assert.AreEqual("README.md", hfUrl.Path);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void HuggingFaceUrl_InvalidUrl_ThrowsException()
    {
        new HuggingFaceUrl("https://google.com");
    }

    [TestMethod]
    public void GitHubUrl_ValidUrl_ParsesCorrectly()
    {
        var url = "https://github.com/microsoft/AI-Dev-Gallery";
        var ghUrl = new GitHubUrl(url);

        Assert.AreEqual("microsoft", ghUrl.Organization);
        Assert.AreEqual("AI-Dev-Gallery", ghUrl.Repo);
        Assert.AreEqual("https://github.com/microsoft/AI-Dev-Gallery", ghUrl.FullUrl);
    }

    [TestMethod]
    public void UrlHelpers_GetFullUrl_ReturnsCorrectUrl()
    {
        var hfUrl = "https://huggingface.co/microsoft/phi-2";
        var fullUrl = UrlHelpers.GetFullUrl(hfUrl);
        Assert.AreEqual("https://huggingface.co/microsoft/phi-2/tree/main", fullUrl);

        var ghUrl = "https://github.com/microsoft/AI-Dev-Gallery";
        var fullGhUrl = UrlHelpers.GetFullUrl(ghUrl);
        Assert.AreEqual(ghUrl, fullGhUrl);
    }
}