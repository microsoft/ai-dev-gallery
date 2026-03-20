// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Restored from pre-SDK migration (commit ba748417^) for performance benchmarking.
// This is the old CLI-based service manager that spawns processes to manage the Foundry Local service.
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.IntegrationTests.FoundryLocalCli;

internal class FoundryCliServiceManager
{
    public static FoundryCliServiceManager? TryCreate()
    {
        if (IsAvailable())
        {
            return new FoundryCliServiceManager();
        }

        return null;
    }

    private static bool IsAvailable()
    {
        using var p = new Process();
        p.StartInfo.FileName = "where";
        p.StartInfo.Arguments = "foundry";
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
        p.WaitForExit();
        return p.ExitCode == 0;
    }

    private static string? GetUrl(string output)
    {
        var match = Regex.Match(output, @"https?:\/\/[^\/]+:\d+");
        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }

    public async Task<string?> GetServiceUrl()
    {
        var status = await FoundryCliUtils.RunFoundryWithArguments("service status");

        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return null;
        }

        return GetUrl(status.Output);
    }

    public async Task<bool> IsRunning()
    {
        var url = await GetServiceUrl();
        return url != null;
    }

    public async Task<bool> StartService()
    {
        if (await IsRunning())
        {
            return true;
        }

        var status = await FoundryCliUtils.RunFoundryWithArguments("service start");
        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return false;
        }

        return GetUrl(status.Output) != null;
    }
}