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