// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.UnitTests.Utils;

[TestClass]
public class LicenseInfoTests
{
    [TestMethod]
    public void GetLicenseInfo_KnownLicense_ReturnsCorrectInfo()
    {
        var license = "mit";
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("MIT", info.Name);
        Assert.AreEqual("https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/mit.md", info.LicenseUrl);
    }

    [TestMethod]
    public void GetLicenseInfo_Apache2_ReturnsCorrectInfo()
    {
        var license = "apache-2.0";
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("Apache license 2.0", info.Name);
    }

    [TestMethod]
    public void GetLicenseInfo_UnknownLicense_ReturnsUnknown()
    {
        var license = "non-existent-license";
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("Unknown", info.Name);
        Assert.IsNull(info.LicenseUrl);
    }

    [TestMethod]
    public void GetLicenseInfo_NullLicense_ReturnsUnknown()
    {
        string? license = null;
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("Unknown", info.Name);
    }

    [TestMethod]
    public void GetLicenseInfo_EmptyLicense_ReturnsUnknown()
    {
        var license = string.Empty;
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("Unknown", info.Name);
    }

    [TestMethod]
    public void GetLicenseInfo_Llama3_ReturnsCorrectInfo()
    {
        var license = "llama3";
        var info = LicenseInfo.GetLicenseInfo(license);
        Assert.AreEqual("Llama 3 Community License Agreement", info.Name);
    }
}