# Safe to remove after the next 1.7 or 1.8 experimental release
$directoryPackagesFile = "$($PSScriptRoot)\..\Directory.Packages.props"
$directoryPackagesFileContent = Get-Content $directoryPackagesFile -Encoding UTF8 -Raw
$directoryPackagesFileContent = $directoryPackagesFileContent -replace '1.7.250127003-experimental3', '1.7.250213007-experimental'
Set-Content $directoryPackagesFile -Value $directoryPackagesFileContent -Encoding UTF8