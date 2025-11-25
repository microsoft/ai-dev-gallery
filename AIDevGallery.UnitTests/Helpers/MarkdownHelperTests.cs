// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.UnitTests.Helpers;

[TestClass]
public class MarkdownHelperTests
{
    [TestMethod]
    public void PreprocessMarkdown_RemovesFrontMatter()
    {
        var markdown = "---\ntitle: Hello\n---\n# Content";
        var result = MarkdownHelper.PreprocessMarkdown(markdown);
        Assert.AreEqual("# Content", result.Trim());
    }

    [TestMethod]
    public void PreprocessMarkdown_RemovesPhiSilicaWarning()
    {
        var markdown = "> [!IMPORTANT]\n> - Phi Silica is not available in China.\n\nContent";
        var result = MarkdownHelper.PreprocessMarkdown(markdown);
        Assert.AreEqual("Content", result.Trim());
    }

    [TestMethod]
    public void PreprocessMarkdown_ReplacesAdmonitions()
    {
        var markdown = "> [!IMPORTANT]\n> This is important.";
        var result = MarkdownHelper.PreprocessMarkdown(markdown);
        Assert.IsTrue(result.Contains("> **ℹ️ Important:**"));

        markdown = "> [!NOTE]\n> This is a note.";
        result = MarkdownHelper.PreprocessMarkdown(markdown);
        Assert.IsTrue(result.Contains("> **❗ Note:**"));

        markdown = "> [!TIP]\n> This is a tip.";
        result = MarkdownHelper.PreprocessMarkdown(markdown);
        Assert.IsTrue(result.Contains("> **💡 Tip:**"));
    }
}