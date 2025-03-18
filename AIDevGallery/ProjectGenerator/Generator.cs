// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.Build.Construction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AIDevGallery.ProjectGenerator;

internal partial class Generator
{
    private readonly string templatePath = Path.Join(Package.Current.InstalledLocation.Path, "ProjectGenerator", "Template");
    private string generatedProjectPath = string.Empty;

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex SafeNameRegex();

    private static string ToSafeVariableName(string input)
    {
        // Replace invalid characters with an underscore
        string safeName = SafeNameRegex().Replace(input, "_");

        // Ensure the name does not start with a digit
        if (safeName.Length > 0 && char.IsDigit(safeName[0]))
        {
            safeName = "_" + safeName;
        }

        // If the name is empty or only contains invalid characters, return a default name
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "MySampleApp";
        }

        return safeName;
    }

    internal const string DotNetVersion = "net9.0";

    internal async Task<string> GenerateAsync(Sample sample, Dictionary<ModelType, ExpandedModelDetails> models, bool copyModelLocally, string outputPath, CancellationToken cancellationToken)
    {
        var projectName = $"{sample.Name}Sample";
        string safeProjectName = ToSafeVariableName(projectName);
        string guid9 = Guid.NewGuid().ToString();
        string slnProjGuid = Guid.NewGuid().ToString();
        string xmlEscapedPublisher = "MyTestPublisher";
        string xmlEscapedPublisherDistinguishedName = $"CN={xmlEscapedPublisher}";

        outputPath = Path.Join(outputPath, safeProjectName);
        var dirIndexCount = 1;
        while (Directory.Exists(outputPath))
        {
            outputPath = Path.Join(Path.GetDirectoryName(outputPath), $"{safeProjectName}_{dirIndexCount}");
            dirIndexCount++;
        }

        generatedProjectPath = outputPath;

        var modelTypes = sample.Model1Types.Concat(sample.Model2Types ?? Enumerable.Empty<ModelType>())
                .Where(models.ContainsKey);

        if (copyModelLocally)
        {
            long sumTotalSize = 0;
            foreach (var modelType in modelTypes)
            {
                if (!models.TryGetValue(modelType, out var modelInfo))
                {
                    throw new ArgumentException($"Model type {modelType} not found in the models dictionary", nameof(models));
                }

                if (modelInfo.Path.Contains("file://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var cachedModelDirectoryAttributes = File.GetAttributes(modelInfo.Path);

                if (cachedModelDirectoryAttributes.HasFlag(FileAttributes.Directory))
                {
                    sumTotalSize += Directory.GetFiles(modelInfo.Path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                }
                else
                {
                    sumTotalSize += new FileInfo(modelInfo.Path).Length;
                }
            }

            var availableSpace = DriveInfo.GetDrives().First(d => d.RootDirectory.FullName == Path.GetPathRoot(outputPath)).AvailableFreeSpace;
            if (sumTotalSize > availableSpace)
            {
                throw new IOException("Not enough disk space to copy the model files.");
            }
        }

        Directory.CreateDirectory(outputPath);

        Dictionary<ModelType, (ExpandedModelDetails ExpandedModelDetails, string ModelPathStr)> modelInfos = [];
        List<string> modelIds = [];
        foreach (var modelType in modelTypes)
        {
            if (!models.TryGetValue(modelType, out var modelInfo))
            {
                throw new ArgumentException($"Model type {modelType} not found in the models dictionary", nameof(models));
            }

            if (ModelTypeHelpers.ModelDetails.TryGetValue(modelType, out var modelDetails))
            {
                modelIds.Add(modelDetails.Id);
            }
            else if (ModelTypeHelpers.ModelDetails.FirstOrDefault(mf => mf.Value.Url == modelInfo.Url) is var modelDetails2 && modelDetails2.Value != null)
            {
                modelIds.Add(modelDetails2.Value.Id);
            }
            else if (ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(modelType, out var apiDefinitionDetails))
            {
                modelIds.Add(apiDefinitionDetails.Id);
            }
            else if (App.ModelCache.GetCachedModel(modelInfo.Url) is var cachedModel && cachedModel != null)
            {
                if (cachedModel.Details.IsUserAdded)
                {
                    modelIds.Add("UserAdded");
                }
            }

            string modelPathStr;

            if (copyModelLocally && !modelInfo.Path.Contains("file://", StringComparison.OrdinalIgnoreCase))
            {
                var modelPath = Path.GetFileName(modelInfo.Path);
                var cachedModelDirectoryAttributes = File.GetAttributes(modelInfo.Path);

                if (cachedModelDirectoryAttributes.HasFlag(FileAttributes.Directory))
                {
                    var modelDirectory = Directory.CreateDirectory(Path.Join(outputPath, "Models", modelPath));
                    foreach (var file in Directory.GetFiles(modelInfo.Path, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var filePath = Path.Join(modelDirectory.FullName, Path.GetRelativePath(modelInfo.Path, file));
                        var directory = Path.GetDirectoryName(filePath);
                        if (directory != null && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await CopyFileAsync(file, filePath, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    var modelDirectory = Directory.CreateDirectory(Path.Join(outputPath, "Models"));
                    await CopyFileAsync(modelInfo.Path, Path.Join(modelDirectory.FullName, modelPath), cancellationToken).ConfigureAwait(false);
                }

                modelPathStr = $"System.IO.Path.Join(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, \"Models\", @\"{modelPath}\")";
                modelInfo = modelInfo with { Path = modelPath };
            }
            else
            {
                modelPathStr = $"@\"{modelInfo.Path}\"";
            }

            modelInfos.Add(modelType, new(modelInfo, modelPathStr));
        }

        SampleProjectGeneratedEvent.Log(sample.Id, modelIds.First(), modelIds.Count > 1 ? modelIds.Last() : string.Empty, copyModelLocally);

        string[] extensions = [".manifest", ".xaml", ".cs", ".appxmanifest", ".csproj", ".ico", ".png", ".json", ".pubxml", ".sln", ".vsconfig"];

        // Get all files from the template directory with the allowed extensions
        var files = Directory.GetFiles(templatePath, "*.*", SearchOption.AllDirectories).Where(file => extensions.Any(file.EndsWith));

        var renames = new Dictionary<string, string>
        {
            { "Package-managed.appxmanifest", "Package.appxmanifest" },
            { "ProjectTemplate.csproj", $"{safeProjectName}.csproj" },
            { "SolutionTemplate.sln", $"{safeProjectName}.sln" }
        };

        await AddFilesFromSampleAsync(sample, outputPath, modelInfos, cancellationToken);

        foreach (var file in files)
        {
            var relativePath = file[(templatePath.Length + 1)..];

            var fileName = Path.GetFileName(file);
            if (renames.TryGetValue(fileName, out var newName))
            {
                relativePath = relativePath.Replace(fileName, newName);
            }

            var outputPathFile = Path.Join(outputPath, relativePath);

            // Create the directory if it doesn't exist
            var directory = Path.GetDirectoryName(outputPathFile);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // if image file, just copy
            if (Path.GetExtension(file) is ".ico" or ".png")
            {
                File.Copy(file, outputPathFile);
                continue;
            }
            else
            {
                // Read the file
                var content = await File.ReadAllTextAsync(file, cancellationToken);

                // Replace the variables
                content = content.Replace("$projectname$", projectName);
                content = content.Replace("$safeprojectname$", SamplesConstants.BaseNamespace);
                content = content.Replace("$projectFileName$", safeProjectName);
                content = content.Replace("$projectGuid$", slnProjGuid);
                content = content.Replace("$guid9$", guid9);
                content = content.Replace("$XmlEscapedPublisherDistinguishedName$", xmlEscapedPublisherDistinguishedName);
                content = content.Replace("$XmlEscapedPublisher$", xmlEscapedPublisher);
                content = content.Replace("$DotNetVersion$", DotNetVersion);

                // Write the file
                await File.WriteAllTextAsync(outputPathFile, content, cancellationToken);
            }
        }

        // Add Asset Files
        foreach (string assetFilename in sample.AssetFilenames)
        {
            string fullAssetPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets", assetFilename);
            string fullOutputAssetPath = Path.Combine(outputPath, "Assets", assetFilename);
            if (File.Exists(fullAssetPath))
            {
                File.Copy(fullAssetPath, fullOutputAssetPath);
            }
        }

        var csproj = Path.Join(outputPath, $"{safeProjectName}.csproj");

        List<string> packageReferences = sample.GetAllNugetPackageReferences(models);
        packageReferences.Add("Microsoft.WindowsAppSDK");
        packageReferences.Add("Microsoft.Windows.SDK.BuildTools");

        // Add NuGet references
        if (packageReferences.Count > 0 || copyModelLocally)
        {
            var project = ProjectRootElement.Open(csproj);
            var itemGroup = project.AddItemGroup();

            static void AddPackageReference(ProjectItemGroupElement itemGroup, string packageName)
            {
                if (itemGroup.Items.Any(i => i.ItemType == "PackageReference" && i.Include == packageName))
                {
                    return;
                }

                var packageReferenceItem = itemGroup.AddItem("PackageReference", packageName);

                if (packageName == "Microsoft.Windows.CsWin32")
                {
                    packageReferenceItem.AddMetadata("PrivateAssets", "all", true);
                }
                else if (packageName == "Microsoft.AI.DirectML" ||
                            packageName == "Microsoft.ML.OnnxRuntime.DirectML" ||
                            packageName == "Microsoft.ML.OnnxRuntimeGenAI.DirectML")
                {
                    packageReferenceItem.Condition = "$(Platform) == 'x64'";
                }
                else if (packageName == "Microsoft.ML.OnnxRuntime.Qnn" ||
                            packageName == "Microsoft.ML.OnnxRuntimeGenAI.QNN" ||
                            packageName == "Microsoft.ML.OnnxRuntimeGenAI")
                {
                    packageReferenceItem.Condition = "$(Platform) == 'ARM64'";
                }

                var versionStr = PackageVersionHelpers.PackageVersions[packageName];
                packageReferenceItem.AddMetadata("Version", versionStr, true);
            }

            foreach (var packageName in packageReferences)
            {
                if (packageName == "Microsoft.ML.OnnxRuntime.DirectML")
                {
                    AddPackageReference(itemGroup, "Microsoft.AI.DirectML");
                    AddPackageReference(itemGroup, "Microsoft.ML.OnnxRuntime.Qnn");
                }
                else if (packageName == "Microsoft.ML.OnnxRuntimeGenAI.DirectML")
                {
                    AddPackageReference(itemGroup, "Microsoft.AI.DirectML");
                    AddPackageReference(itemGroup, "Microsoft.ML.OnnxRuntime.Qnn");
                    AddPackageReference(itemGroup, "Microsoft.ML.OnnxRuntimeGenAI.QNN");
                    AddPackageReference(itemGroup, "Microsoft.ML.OnnxRuntimeGenAI.Managed");
                }

                AddPackageReference(itemGroup, packageName);
            }

            if (copyModelLocally)
            {
                var modelContentItemGroup = project.AddItemGroup();
                foreach (var modelInfo in modelInfos)
                {
                    if (modelInfo.Value.ExpandedModelDetails.Path.Contains("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var cachedModelDirectoryAttributes = File.GetAttributes(modelInfo.Value.ExpandedModelDetails.Path);
                    if (!cachedModelDirectoryAttributes.HasFlag(FileAttributes.Directory))
                    {
                        modelContentItemGroup.AddItem("Content", @$"Models\{modelInfo.Value.ExpandedModelDetails.Path}");
                    }
                    else
                    {
                        modelContentItemGroup.AddItem("Content", @$"Models\{modelInfo.Value.ExpandedModelDetails.Path}\**");
                    }
                }
            }

            project.Save();
        }

        // Fix PublishProfiles. This shouldn't be necessary once the templates are fixed
        foreach (var file in Directory.GetFiles(outputPath, "*.pubxml", SearchOption.AllDirectories))
        {
            var pubxml = ProjectRootElement.Open(file);
            var firstPg = pubxml.PropertyGroups.FirstOrDefault();
            firstPg ??= pubxml.AddPropertyGroup();

            if (!firstPg.Children.Any(p => p.ElementName == "RuntimeIdentifier"))
            {
                var runtimeIdentifier = Path.GetFileNameWithoutExtension(file).Split('-').Last();
                firstPg.AddProperty("RuntimeIdentifier", $"win-{runtimeIdentifier}");
            }

            pubxml.Save();
        }

        // Styles
        List<string> styles = [];
        foreach (var file in Directory.GetFiles(Path.Join(outputPath, "Utils"), "*.xaml", SearchOption.TopDirectoryOnly))
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            if (!content.StartsWith("<ResourceDictionary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            styles.Add(file);
        }

        if (styles.Count > 0)
        {
            var appXamlPath = Path.Join(outputPath, "App.xaml");
            var appXaml = await File.ReadAllTextAsync(appXamlPath, cancellationToken);
            appXaml = appXaml.Replace(
                "                <!--  Other merged dictionaries here  -->",
                string.Join(Environment.NewLine, styles.Select(s => $"                <ResourceDictionary Source=\"{Path.Join("Utils", Path.GetFileName(s))}\" />")));
            await File.WriteAllTextAsync(appXamlPath, appXaml, cancellationToken);
        }

        return outputPath;
    }

    private static async Task CopyFileAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var destinationStream = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddFilesFromSampleAsync(
        Sample sample,
        string outputPath,
        Dictionary<ModelType, (ExpandedModelDetails ExpandedModelDetails, string ModelPathStr)> modelInfos,
        CancellationToken cancellationToken)
    {
        List<SharedCodeEnum> sharedCode = sample.GetAllSharedCode(modelInfos.ToDictionary(m => m.Key, m => m.Value.ExpandedModelDetails));

        foreach (var sharedCodeEnum in sharedCode)
        {
            string directory = outputPath;

            if (sharedCodeEnum != SharedCodeEnum.NativeMethods)
            {
                if (!Directory.Exists(Path.Join(outputPath, "Utils")))
                {
                    Directory.CreateDirectory(Path.Join(outputPath, "Utils"));
                }

                directory = Path.Join(outputPath, "Utils");
            }

            var fileName = SharedCodeHelpers.GetName(sharedCodeEnum);
            var source = SharedCodeHelpers.GetSource(sharedCodeEnum);

            await File.WriteAllTextAsync(Path.Join(directory, fileName), source, cancellationToken);
        }

        if (!string.IsNullOrEmpty(sample.XAMLCode))
        {
            await File.WriteAllTextAsync(Path.Join(outputPath, $"Sample.xaml"), sample.XAMLCode, cancellationToken);
        }

        if (!string.IsNullOrEmpty(sample.CSCode))
        {
            await File.WriteAllTextAsync(Path.Join(outputPath, $"Sample.xaml.cs"), sample.GetCleanCSCode(modelInfos), cancellationToken);
        }
    }

    internal void CleanUp()
    {
        if(!string.IsNullOrEmpty(generatedProjectPath) && Directory.Exists(generatedProjectPath))
        {
            Directory.Delete(generatedProjectPath, true);
            generatedProjectPath = string.Empty;
        }
    }
}