// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Tests.TestInfra;

/// <summary>
/// Central configuration for test infrastructure.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// The MSIX package identity name GUID from Package.appxmanifest.
    /// Can be overridden via environment variable TEST_PACKAGE_IDENTITY_NAME for local development.
    /// Default: e7af07c0-77d2-43e5-ab82-9cdb9daa11b3
    /// </summary>
    public static readonly string MsixPackageIdentityName =
        Environment.GetEnvironmentVariable("TEST_PACKAGE_IDENTITY_NAME")
        ?? "e7af07c0-77d2-43e5-ab82-9cdb9daa11b3";
}