// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Tests.UnitTests;

#pragma warning disable CA1707 // Test method names contain underscores to indicate multiple test conditions

[TestClass]
public class RemoveExcludedLinesCsTests
{
    [TestMethod]
    public void RemoveExcludedLinesCs_RemovesExcludeLine()
    {
        string input = @"public void Test()
{
    int x = 5; // <exclude-line>
    int y = 10;
}";

        string expected = @"public void Test()
{
    int y = 10;
}";

        string result = RemoveExcludedLines(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RemoveExcludedLinesCs_RemovesExcludeBlock()
    {
        string input = @"public void Test()
{
    int x = 5;
    // <exclude>
    Debug.WriteLine(x);
    // </exclude>
    return x;
}";

        string expected = @"public void Test()
{
    int x = 5;
    return x;
}";

        string result = RemoveExcludedLines(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RemoveExcludedLinesCs_GenerateTextSample_BugPattern()
    {
        // Simulate the exact bug from issue #579:
        // - Line has declare variable with <exclude-line>
        // - Another block has <exclude> that uses that variable
        // - When both are removed, variable reference is broken
        string input = @"public void GenerateText(string topic)
{
    var contentStartedBeingGenerated = false; // <exclude-line>
    
    DispatcherQueue.TryEnqueue(() =>
    {
        // <exclude>
        if (!contentStartedBeingGenerated)
        {
            contentStartedBeingGenerated = true;
        }
        // </exclude>
    });
}";

        string result = RemoveExcludedLines(input);

        // After removal, the variable should not be referenced anywhere
        // and there should be no syntax errors
        Assert.IsFalse(result.Contains("contentStartedBeingGenerated"));
        Assert.IsFalse(result.Contains("// <exclude-line>"));
        Assert.IsFalse(result.Contains("// <exclude>"));
        Assert.IsFalse(result.Contains("// </exclude>"));

        // The DispatcherQueue call should still be syntactically valid
        Assert.IsTrue(result.Contains("DispatcherQueue.TryEnqueue"));
        Assert.IsTrue(result.Contains("});"));
    }

    // Exact implementation from SamplesSourceGenerator.cs (updated version with semantic awareness)
    private static string RemoveExcludedLines(string input)
    {
        List<string> lines = new(input.Split([Environment.NewLine], StringSplitOptions.None));

        // First pass: collect all variable declarations in <exclude-line> sections
        var excludeLineVariables = new HashSet<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("// <exclude-line>") || lines[i].Contains("//<exclude-line>"))
            {
                var line = lines[i];
                var varMatch = System.Text.RegularExpressions.Regex.Match(line, @"(?:var|int|bool|string|double|float|long|decimal|List<[^>]+>|Dictionary<[^>]+>)\s+(\w+)\s*=");
                if (varMatch.Success)
                {
                    excludeLineVariables.Add(varMatch.Groups[1].Value);
                }
            }
        }

        // Second pass: mark lines for removal
        var linesToRemove = new HashSet<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("//<exclude>") || lines[i].Contains("// <exclude>"))
            {
                linesToRemove.Add(i);

                while (i < lines.Count && !lines[i].Contains("//</exclude>") && !lines[i].Contains("// </exclude>"))
                {
                    i++;
                    linesToRemove.Add(i);
                }

                if (i < lines.Count && (lines[i].Contains("//</exclude>") || lines[i].Contains("// </exclude>")))
                {
                    linesToRemove.Add(i);
                }
            }
            else if (lines[i].Contains("// <exclude-line>") || lines[i].Contains("//<exclude-line>") || lines[i].Contains("SendSampleInteractedEvent"))
            {
                linesToRemove.Add(i);
            }
        }

        // Third pass: remove lines in reverse order to preserve indices
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (linesToRemove.Contains(i))
            {
                lines.RemoveAt(i);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}

#pragma warning restore CA1707