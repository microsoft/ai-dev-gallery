# This script unstubs the telemetry at build time and replaces the stubbed file with a reference internal nuget package

#
# Unstub managed telemetry
#

Remove-Item "$($PSScriptRoot)\..\AIDevGallery\Telemetry\TelemetryEventSource.cs"

$projFile = "$($PSScriptRoot)\..\AIDevGallery\AIDevGallery.csproj"
$projFileContent = Get-Content $projFile -Encoding UTF8 -Raw

$xml = [xml]$projFileContent
$xml.PreserveWhitespace = $true

$defineConstantsNode = $xml.SelectSingleNode("//DefineConstants")
if ($defineConstantsNode -ne $null) {
    $defineConstantsNode.ParentNode.RemoveChild($defineConstantsNode)
    $xml.Save($projFile)
}

if ($projFileContent.Contains('Microsoft.Telemetry.Inbox.Managed')) {
    Write-Output "Project file already contains a reference to the internal package."
    return;
}

$packageReferenceNode = $xml.CreateElement("PackageReference");
$packageReferenceNode.SetAttribute("Include", "Microsoft.Telemetry.Inbox.Managed")
$itemGroupNode = $xml.CreateElement("ItemGroup")
$itemGroupNode.AppendChild($packageReferenceNode)
$xml.DocumentElement.AppendChild($itemGroupNode)
$xml.Save($projFile)

# Safe to remove after the next 1.7 or 1.8 experimental release
$directoryPackagesFile = "$($PSScriptRoot)\..\Directory.Packages.props"
$directoryPackagesFileContent = Get-Content $directoryPackagesFile -Encoding UTF8 -Raw
$directoryPackagesFileContent = $directoryPackagesFileContent -replace '1.7.250127003-experimental3', '1.7.250213007-experimental'
Set-Content $directoryPackagesFile -Value $directoryPackagesFileContent -Encoding UTF8