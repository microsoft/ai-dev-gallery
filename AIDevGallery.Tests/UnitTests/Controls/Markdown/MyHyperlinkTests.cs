// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AIDevGallery.Tests.UnitTests.Controls.Markdown;

[TestClass]
public class MyHyperlinkTests
{
    private static MyHyperlink CreateTestHyperlink()
    {
        var linkInline = new LinkInline { Url = "https://test.com" };
        return new MyHyperlink(linkInline, null);
    }

    private static HtmlNode CreateTestHtmlNode(string href = "https://test.com")
    {
        var html = $"<a href=\"{href}\">link</a>";
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.SelectSingleNode("//a");
    }

    [DataTestMethod]
    [DataRow("Run", DisplayName = "AddChild with Run")]
    [DataRow("Span", DisplayName = "AddChild with Span")]
    [DataRow("Bold", DisplayName = "AddChild with Bold")]
    public void AddChildWithInlineElementShouldAddSuccessfully(string elementType)
    {
        // Arrange
        var hyperlink = CreateTestHyperlink();
        Microsoft.UI.Xaml.Documents.Inline inlineElement = elementType switch
        {
            "Run" => new Run { Text = "Test Text" },
            "Span" => new Span { Inlines = { new Run { Text = "Span Content" } } },
            "Bold" => new Bold { Inlines = { new Run { Text = "Bold Text" } } },
            _ => throw new ArgumentException($"Unknown element type: {elementType}")
        };
        var addChild = new TestAddChild(inlineElement);

        // Act
        hyperlink.AddChild(addChild);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.AreEqual(1, hyperlinkElement.Inlines.Count);
        Assert.AreEqual(inlineElement, hyperlinkElement.Inlines[0]);
    }

    [TestMethod]
    public void AddChildWithInlineUIContainerShouldBeSkipped()
    {
        // Arrange
        var hyperlink = CreateTestHyperlink();
        var inlineUIContainer = new InlineUIContainer();
        var addChild = new TestAddChild(inlineUIContainer);

        // Act
        hyperlink.AddChild(addChild);

        // Assert - InlineUIContainer should not be added due to WinUI limitation
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.AreEqual(0, hyperlinkElement.Inlines.Count);
    }

    [TestMethod]
    public void ConstructorWithLinkInlineShouldInitializeCorrectly()
    {
        // Arrange & Act
        var linkInline = new LinkInline { Url = "https://example.com" };
        var hyperlink = new MyHyperlink(linkInline, "https://base.com");

        // Assert
        Assert.IsNotNull(hyperlink.TextElement);
        Assert.IsInstanceOfType<Hyperlink>(hyperlink.TextElement);
        Assert.IsFalse(
            hyperlink.IsHtml,
            "LinkInline constructor should set IsHtml to false");

        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.AreEqual(new Uri("https://example.com"), hyperlinkElement.NavigateUri);
    }

    [TestMethod]
    public void ConstructorWithHtmlNodeShouldInitializeCorrectly()
    {
        // Arrange & Act
        var htmlNode = CreateTestHtmlNode("https://example.com");
        var hyperlink = new MyHyperlink(htmlNode, "https://base.com");

        // Assert
        Assert.IsNotNull(hyperlink.TextElement);
        Assert.IsInstanceOfType<Hyperlink>(hyperlink.TextElement);
        Assert.IsTrue(
            hyperlink.IsHtml,
            "HtmlNode constructor should set IsHtml to true");

        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.AreEqual(new Uri("https://example.com"), hyperlinkElement.NavigateUri);
    }

    [TestMethod]
    public void AddChildShouldHandleNestedHyperlinkGracefully()
    {
        // Arrange
        var hyperlink = CreateTestHyperlink();
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        var initialCount = hyperlinkElement.Inlines.Count;

        // Add a nested hyperlink which WinUI typically rejects
        var nestedHyperlink = new Hyperlink();
        nestedHyperlink.Inlines.Add(new Run { Text = "Nested" });
        var addChild = new TestAddChild(nestedHyperlink);

        // Act - Should not throw exception
        hyperlink.AddChild(addChild);

        // Assert - Either caught by exception handler or rejected by WinUI, but no crash
        // The count should be the same or potentially increased (implementation dependent)
        Assert.IsTrue(
            hyperlinkElement.Inlines.Count >= initialCount,
            "Should handle nested hyperlink without throwing exception");
    }

    [DataTestMethod]
    [DataRow("https://example.com/test", null, "https://example.com/test", DisplayName = "Absolute URL without base")]
    [DataRow("test/page", "https://example.com/", "https://example.com/test/page", DisplayName = "Relative URL with base")]
    [DataRow("/absolute/path", "https://example.com/other/", "https://example.com/absolute/path", DisplayName = "Absolute path with base")]
    [DataRow("https://example.com/html", "https://other.com/", "https://example.com/html", DisplayName = "Absolute URL ignores base")]
    public void NavigateUriShouldResolveUrlsCorrectly(string url, string baseUrl, string expectedUrl)
    {
        // Arrange
        var linkInline = new LinkInline { Url = url };

        // Act
        var hyperlink = new MyHyperlink(linkInline, baseUrl);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.IsNotNull(hyperlinkElement.NavigateUri);
        Assert.AreEqual(new Uri(expectedUrl), hyperlinkElement.NavigateUri);
    }

    [DataTestMethod]
    [DataRow("not_a_valid_url", null, DisplayName = "Invalid URL without base")]
    [DataRow("relative/path", null, DisplayName = "Relative path without base")]
    [DataRow("/absolute/path", null, DisplayName = "Absolute path without base")]
    [DataRow("test.html", "invalid-base-url", DisplayName = "Relative URL with invalid base")]
    public void NavigateUriShouldBeNullWhenUrlCannotBeResolved(string url, string baseUrl)
    {
        // Arrange
        var linkInline = new LinkInline { Url = url };

        // Act
        var hyperlink = new MyHyperlink(linkInline, baseUrl);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.IsNull(
            hyperlinkElement.NavigateUri,
            $"NavigateUri should be null when URL '{url}' cannot be resolved with base '{baseUrl}'");
    }

    [TestMethod]
    public void HtmlNodeWithMissingHrefShouldUseDefaultValue()
    {
        // Arrange
        var html = "<a>link without href</a>";
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var htmlNode = doc.DocumentNode.SelectSingleNode("//a");

        // Act
        var hyperlink = new MyHyperlink(htmlNode, null);

        // Assert - Should not throw and should use default "#" value
        Assert.IsNotNull(hyperlink);
        Assert.IsTrue(hyperlink.IsHtml);

        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;

        // HtmlAgilityPack returns "#" as default, which is not a valid absolute URI
        Assert.IsNull(
            hyperlinkElement.NavigateUri,
            "NavigateUri should be null for default '#' href");
    }

    [TestMethod]
    public void LinkInlineWithEmptyOrNullUrlShouldNotThrow()
    {
        // Arrange & Act & Assert - Should not throw
        var linkInlineEmpty = new LinkInline { Url = string.Empty };
        var hyperlinkEmpty = new MyHyperlink(linkInlineEmpty, null);
        Assert.IsNotNull(hyperlinkEmpty);
        Assert.IsFalse(hyperlinkEmpty.IsHtml);

        var linkInlineNull = new LinkInline { Url = null };
        var hyperlinkNull = new MyHyperlink(linkInlineNull, null);
        Assert.IsNotNull(hyperlinkNull);
        Assert.IsFalse(hyperlinkNull.IsHtml);
    }

    [TestMethod]
    public void ConstructorShouldUseGetDynamicUrlWhenAvailable()
    {
        // Arrange
        var staticUrl = "https://static.com";
        var dynamicUrl = "https://dynamic.com";
        var linkInline = new LinkInline
        {
            Url = staticUrl,
            GetDynamicUrl = () => dynamicUrl
        };

        // Act
        var hyperlink = new MyHyperlink(linkInline, null);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.IsNotNull(hyperlinkElement.NavigateUri);
        Assert.AreEqual(
            new Uri(dynamicUrl),
            hyperlinkElement.NavigateUri,
            "Should use GetDynamicUrl result instead of static Url");
    }

    [TestMethod]
    public void ConstructorShouldHandleNullGetDynamicUrlResult()
    {
        // Arrange
        var staticUrl = "https://static.com";
        var linkInline = new LinkInline
        {
            Url = staticUrl,
            GetDynamicUrl = () => null! // Returns null
        };

        // Act
        var hyperlink = new MyHyperlink(linkInline, null);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.IsNotNull(hyperlinkElement.NavigateUri);
        Assert.AreEqual(
            new Uri(staticUrl),
            hyperlinkElement.NavigateUri,
            "Should fallback to static Url when GetDynamicUrl returns null");
    }

    [DataTestMethod]
    [DataRow("mailto:test@example.com", "mailto:test@example.com", DisplayName = "Mailto URL")]
    [DataRow("tel:+1234567890", "tel:+1234567890", DisplayName = "Tel URL")]
    [DataRow("ftp://ftp.example.com", "ftp://ftp.example.com/", DisplayName = "FTP URL")]
    public void NavigateUriShouldHandleSpecialUrlSchemes(string url, string expectedUrl)
    {
        // Arrange
        var linkInline = new LinkInline { Url = url };

        // Act
        var hyperlink = new MyHyperlink(linkInline, null);

        // Assert
        var hyperlinkElement = (Hyperlink)hyperlink.TextElement;
        Assert.IsNotNull(
            hyperlinkElement.NavigateUri,
            $"Should handle {url} scheme");
        Assert.AreEqual(new Uri(expectedUrl), hyperlinkElement.NavigateUri);
    }

    // Test helper class to wrap TextElement for IAddChild interface
    private class TestAddChild : IAddChild
    {
        public TestAddChild(TextElement textElement)
        {
            TextElement = textElement;
        }

        public TextElement TextElement { get; }

        public void AddChild(IAddChild child)
        {
            // Not needed for these tests
            throw new NotImplementedException();
        }
    }
}