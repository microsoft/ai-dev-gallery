// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class CudaDllManagerTests
{
    [TestMethod]
    public void GetCudaDllFolderPathReturnsValidPath()
    {
        // Act
        var folderPath = CudaDllManager.GetCudaDllFolderPath();

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(folderPath), "Folder path should not be null or empty");
        Assert.IsTrue(folderPath.Contains("CudaDlls"), "Folder path should contain 'CudaDlls' directory name");
    }

    [TestMethod]
    public void GetStatusMessageReflectsDllAvailability()
    {
        // Arrange
        var isAvailable = CudaDllManager.IsCudaDllAvailable();
        var message = CudaDllManager.GetStatusMessage();

        // Assert - Verify status message logic is consistent with actual DLL availability
        if (isAvailable)
        {
            Assert.IsTrue(
                message.Contains("available") || message.Contains("CUDA is available"),
                "Status message should indicate CUDA is available when DLL is present");
        }
        else
        {
            Assert.IsTrue(
                message.Contains("download") || message.Contains("failed") || message.Contains("detected"),
                "Status message should indicate download option or status when DLL is not available");
        }
    }
}