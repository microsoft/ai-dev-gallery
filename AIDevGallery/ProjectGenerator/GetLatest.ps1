function DownloadFiles($apiUrl, $downloadFolder) {
    $files = Invoke-RestMethod -Uri $apiUrl -Method Get

    # Excluded files
    $excludedFiles = @("MainWindow.xaml", "MainWindow.xaml.cs")

    $files = $files | Where-Object { $_.name -notin $excludedFiles }
    $files = $files | Where-Object { $_.name -notlike "WinUI.Desktop.Cs.SingleProjectPackagedApp*" }
    foreach ($file in $files) {
        if ($file.type -eq "file") {
            $url = $file.download_url
            $fileName = Split-Path -Path $url -Leaf
            $relativePath = $file.path -replace '^.*?/([^/]+)$', '$1'
            $downloadPath = Join-Path -Path $downloadFolder -ChildPath $relativePath

            if (-not (Test-Path -Path $downloadFolder)) {
                New-Item -ItemType Directory -Path $downloadFolder | Out-Null
            }

            Invoke-WebRequest -Uri $url -OutFile $downloadPath
        } elseif ($file.type -eq "dir") {
            $folderUrl = $file.url
            $folderName = Split-Path -Path $folderUrl -Leaf
            $cleanFolderName = $folderName -replace '\?.*$', ''
            $subDownloadFolder = Join-Path -Path $downloadFolder -ChildPath $cleanFolderName
            
            DownloadFiles -apiUrl $folderUrl -downloadFolder $subDownloadFolder
        }
    }
}

$downloadFolder = "Template"

$apiUrl = "https://api.github.com/repos/microsoft/WindowsAppSDK/contents/dev/VSIX/ProjectTemplates/Desktop/CSharp/SingleProjectPackagedApp"
DownloadFiles -apiUrl $apiUrl -downloadFolder $downloadFolder