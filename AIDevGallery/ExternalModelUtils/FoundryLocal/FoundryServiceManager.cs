// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;

internal class FoundryServiceManager()
{
    public static FoundryServiceManager? TryCreate()
    {
        if (IsAvailable())
        {
            return new FoundryServiceManager();
        }

        return null;
    }

    private static bool IsAvailable()
    {
        // run "where foundry" to check if the foundry command is available
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

    private string? GetUrl(string output)
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
        var status = await FoundryUtils.RunFoundryWithArguments("service status");

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

        var status = await FoundryUtils.RunFoundryWithArguments("service start");
        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(status.Output))
        {
            return false;
        }

        return GetUrl(status.Output) != null;
    }
}