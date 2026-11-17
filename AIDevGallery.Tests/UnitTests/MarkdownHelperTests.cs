// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class MarkdownHelperTests
{
    [TestMethod]
    [DataRow("Phi Silica")]
    [DataRow("Aion Instruct")]
    public void PreprocessMarkdownPreservesExternalModelNamesAndLinks(string modelName)
    {
        var markdown = $"# {modelName}\n\n[{modelName}](https://learn.microsoft.com/windows/ai/apis/phi-silica)\n\n" +
            $"[{modelName} LoRA](phi-silica-lora.md)\n\n`LanguageModel.CreateAsync()`\n";

        Assert.AreEqual(markdown, MarkdownHelper.PreprocessMarkdown(markdown));
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void PreprocessMarkdownPreservesExistingRegionNoticeHandling(string newline)
    {
        var markdown = $"> [!IMPORTANT]{newline}> - Phi Silica is not available in China.{newline}{newline}# Phi Silica{newline}";

        Assert.AreEqual($"# Phi Silica{newline}", MarkdownHelper.PreprocessMarkdown(markdown));
    }

    [TestMethod]
    public void PreprocessMarkdownPreservesCurrentBrandingAndOtherModels()
    {
        const string markdown = "# Aion Instruct\n\nPhi-3, Phi-4, and other local language models.\n";

        Assert.AreEqual(markdown, MarkdownHelper.PreprocessMarkdown(markdown));
    }
}