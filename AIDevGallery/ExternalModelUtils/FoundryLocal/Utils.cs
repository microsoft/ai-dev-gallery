// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AIDevGallery.ExternalModelUtils.FoundryLocal;
internal class Utils
{
    public async static Task<(string? Output, string? Error, int ExitCode)> RunFoundryWithArguments(string arguments)
    {
        try
        {
            using (var p = new Process())
            {
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
        }
        catch
        {
            return (null, null, -1);
        }
    }
}