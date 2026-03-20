// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Restored from pre-SDK migration (commit ba748417^) for performance benchmarking.
// This is the old CLI-based approach that spawns processes and uses HTTP API calls.
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests.FoundryLocalCli;

internal class FoundryCliUtils
{
    public static async Task<(string? Output, string? Error, int ExitCode)> RunFoundryWithArguments(string arguments)
    {
        try
        {
            using var p = new Process();
            p.StartInfo.FileName = "foundry";
            p.StartInfo.Arguments = arguments;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            string output = await p.StandardOutput.ReadToEndAsync();
            string error = await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync();

            return (output, error, p.ExitCode);
        }
        catch
        {
            return (null, null, -1);
        }
    }
}