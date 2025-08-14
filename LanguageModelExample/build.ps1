Write-Host "正在构建 LanguageModelExample 项目..." -ForegroundColor Green

# 清理之前的构建
if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }

# 还原包
Write-Host "正在还原 NuGet 包..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "包还原失败！" -ForegroundColor Red
    Read-Host "按任意键继续"
    exit 1
}

# 构建项目
Write-Host "正在构建项目..." -ForegroundColor Yellow
dotnet build --configuration Release --platform x64

if ($LASTEXITCODE -eq 0) {
    Write-Host "构建成功！" -ForegroundColor Green
    Write-Host "输出目录: bin\Release\net8.0-windows10.0.26100.0\win-x64\" -ForegroundColor Cyan
} else {
    Write-Host "构建失败！" -ForegroundColor Red
}

Read-Host "按任意键继续" 