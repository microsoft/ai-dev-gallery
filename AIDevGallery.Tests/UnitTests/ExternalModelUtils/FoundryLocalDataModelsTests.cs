// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils.FoundryLocal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests;

/// <summary>
/// Tests for FoundryLocal data models and utility types.
/// </summary>
public class FoundryLocalDataModelsTests
{
    [TestClass]
    public class FoundryCatalogModelTests
    {
        [TestMethod]
        public void FoundryCatalogModelAllPropertiesCanBeSetAndRetrieved()
        {
            // Arrange & Act
            var model = new FoundryCatalogModel
            {
                Name = "phi-3.5-mini-instruct",
                DisplayName = "Phi-3.5 Mini Instruct",
                Alias = "phi-3.5-mini",
                FileSizeMb = 2500,
                License = "MIT",
                ModelId = "microsoft/phi-3.5-mini",
                Task = "chat-completion"
            };

            // Assert
            Assert.AreEqual("phi-3.5-mini-instruct", model.Name);
            Assert.AreEqual("Phi-3.5 Mini Instruct", model.DisplayName);
            Assert.AreEqual("phi-3.5-mini", model.Alias);
            Assert.AreEqual(2500, model.FileSizeMb);
            Assert.AreEqual("MIT", model.License);
            Assert.AreEqual("microsoft/phi-3.5-mini", model.ModelId);
            Assert.AreEqual("chat-completion", model.Task);
        }

        [TestMethod]
        public void FoundryCatalogModelDefaultValuesAreZeroOrNull()
        {
            // Arrange & Act
            var model = new FoundryCatalogModel();

            // Assert
            Assert.AreEqual(0, model.FileSizeMb);
            Assert.IsTrue(string.IsNullOrEmpty(model.Name));
            Assert.IsTrue(string.IsNullOrEmpty(model.DisplayName));
            Assert.IsTrue(string.IsNullOrEmpty(model.Alias));
        }
    }

    [TestClass]
    public class FoundryCachedModelInfoTests
    {
        [TestMethod]
        public void FoundryCachedModelInfoConstructorSetsProperties()
        {
            // Arrange & Act
            var modelInfo = new FoundryCachedModelInfo("phi-3.5-mini-instruct", "phi-3.5-mini");

            // Assert
            Assert.AreEqual("phi-3.5-mini-instruct", modelInfo.Name);
            Assert.AreEqual("phi-3.5-mini", modelInfo.Id);
        }

        [TestMethod]
        public void FoundryCachedModelInfoIsRecordSupportsValueEquality()
        {
            // Arrange
            var info1 = new FoundryCachedModelInfo("test-model", "test-id");
            var info2 = new FoundryCachedModelInfo("test-model", "test-id");
            var info3 = new FoundryCachedModelInfo("different-model", "different-id");

            // Assert - Record types support value-based equality
            Assert.AreEqual(info1, info2, "Same values should be equal");
            Assert.AreNotEqual(info1, info3, "Different values should not be equal");
        }
    }

    [TestClass]
    public class FoundryDownloadResultTests
    {
        [TestMethod]
        public void FoundryDownloadResultSuccessfulDownloadHasNoErrorMessage()
        {
            // Arrange & Act
            var result = new FoundryDownloadResult(true, null);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void FoundryDownloadResultFailedDownloadHasErrorMessage()
        {
            // Arrange
            var errorMsg = "Network timeout";

            // Act
            var result = new FoundryDownloadResult(false, errorMsg);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(errorMsg, result.ErrorMessage);
        }

        [TestMethod]
        public void FoundryDownloadResultSuccessWithWarningBothSuccessAndMessage()
        {
            // Arrange - Important: download can succeed but have warnings
            var warningMsg = "Model loaded but some features unavailable";

            // Act
            var result = new FoundryDownloadResult(true, warningMsg);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(warningMsg, result.ErrorMessage);
        }
    }

    [TestClass]
    public class ModelTaskTypesTests
    {
        [TestMethod]
        public void ModelTaskTypesChatCompletionHasCorrectValue()
        {
            // Arrange & Act
            var chatCompletion = ModelTaskTypes.ChatCompletion;

            // Assert
            Assert.AreEqual("chat-completion", chatCompletion);
        }

        [TestMethod]
        public void ModelTaskTypesAutomaticSpeechRecognitionHasCorrectValue()
        {
            // Arrange & Act
            var asr = ModelTaskTypes.AutomaticSpeechRecognition;

            // Assert
            Assert.AreEqual("automatic-speech-recognition", asr);
        }
    }
}