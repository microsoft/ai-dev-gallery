// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace AIDevGallery.Tests.Unit;

[TestClass]
public class URLHelperTests
{
    [TestMethod]
    public void IsValidUrl_ValidUrls_ReturnsTrue()
    {
        Assert.IsTrue(URLHelper.IsValidUrl("https://www.microsoft.com"));
        Assert.IsTrue(URLHelper.IsValidUrl("http://localhost:8080"));
    }

    [TestMethod]
    public void IsValidUrl_InvalidUrls_ReturnsFalse()
    {
        Assert.IsFalse(URLHelper.IsValidUrl("not a url"));
        Assert.IsFalse(URLHelper.IsValidUrl("/relative/path"));
    }

    [TestMethod]
    public void FixWcrReadmeLink_AbsoluteLink_ReturnsCorrectUrl()
    {
        var link = "/some/path";

        // We expect the result to match Path.Join behavior as per implementation
        var expected = Path.Join("https://learn.microsoft.com", link);
        var result = URLHelper.FixWcrReadmeLink(link);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FixWcrReadmeLink_RelativeLink_ReturnsCorrectUrl()
    {
        var link = "some-doc.md";

        // Implementation: Path.Join(DocsBaseUrl, WcrDocsRelativePath, link.Replace(".md", string.Empty))
        var expected = Path.Join("https://learn.microsoft.com", "/windows/ai/apis/", "some-doc");
        var result = URLHelper.FixWcrReadmeLink(link);
        Assert.AreEqual(expected, result);
    }
}