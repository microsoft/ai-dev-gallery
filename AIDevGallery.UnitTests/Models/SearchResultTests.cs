// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.UnitTests.Models;

[TestClass]
public class SearchResultTests
{
    [TestMethod]
    public void Constructor_DefaultValues_AllPropertiesNull()
    {
        // Act
        var searchResult = new SearchResult();

        // Assert
        Assert.IsNull(searchResult.Icon);
        Assert.IsNull(searchResult.Label);
        Assert.IsNull(searchResult.Description);
        Assert.IsNull(searchResult.Tag);
    }

    [TestMethod]
    public void Properties_CanBeSet()
    {
        // Arrange
        var searchResult = new SearchResult();
        var tagObject = new { Id = 1, Name = "Test" };

        // Act
        searchResult.Icon = "icon.png";
        searchResult.Label = "Test Label";
        searchResult.Description = "Test Description";
        searchResult.Tag = tagObject;

        // Assert
        Assert.AreEqual("icon.png", searchResult.Icon);
        Assert.AreEqual("Test Label", searchResult.Label);
        Assert.AreEqual("Test Description", searchResult.Description);
        Assert.AreEqual(tagObject, searchResult.Tag);
    }

    [TestMethod]
    public void Properties_InitializerSyntax_WorksCorrectly()
    {
        // Arrange
        var tagObject = "string-tag";

        // Act
        var searchResult = new SearchResult
        {
            Icon = "ms-appx:///icon.png",
            Label = "Sample Label",
            Description = "Sample Description with multiple words",
            Tag = tagObject
        };

        // Assert
        Assert.AreEqual("ms-appx:///icon.png", searchResult.Icon);
        Assert.AreEqual("Sample Label", searchResult.Label);
        Assert.AreEqual("Sample Description with multiple words", searchResult.Description);
        Assert.AreEqual(tagObject, searchResult.Tag);
    }

    [TestMethod]
    public void Tag_CanHoldDifferentTypes()
    {
        // Arrange
        var searchResult1 = new SearchResult { Tag = "string" };
        var searchResult2 = new SearchResult { Tag = 42 };
        var searchResult3 = new SearchResult { Tag = new { Name = "Object" } };

        // Assert
        Assert.IsInstanceOfType(searchResult1.Tag, typeof(string));
        Assert.IsInstanceOfType(searchResult2.Tag, typeof(int));
        Assert.IsNotNull(searchResult3.Tag);
    }
}