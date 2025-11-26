# ⚠️ WinUI3 測試需要 MSIX 部署

## 問題

您遇到了這個錯誤：
```
System.Runtime.InteropServices.COMException (0x80040154): Class not registered
at Microsoft.UI.Xaml.Application.Start(ApplicationInitializationCallback callback)
```

## 原因

WinUI3 應用程序依賴 Windows App SDK 的 COM 組件，這些組件需要通過 MSIX 打包才能正確註冊。直接運行未打包的 .exe 文件會失敗。

## 解決方案

### 方法 1：部署 MSIX 包（推薦）

1. **在 Visual Studio 中部署**
   ```
   - 右鍵點擊 AIDevGallery 項目
   - 選擇 "Deploy"（部署）
   - 等待部署完成
   ```

2. **使用命令行部署**
   ```powershell
   # 從解決方案根目錄運行
   msbuild AIDevGallery\AIDevGallery.csproj /t:Deploy /p:Configuration=Debug /p:Platform=x64
   ```

3. **驗證部署**
   ```powershell
   # 檢查應用是否已安裝
   Get-AppxPackage | Where-Object {$_.Name -like '*AIDevGallery*'}
   
   # 應該看到類似輸出：
   # Name: e7af07c0-77d2-43e5-ab82-9cdb9daa11b3
   # Publisher: CN=nikolame
   # Architecture: X64
   # Version: 0.0.3.0
   ```

4. **運行測試**
   ```powershell
   dotnet test --filter "TestCategory=Smoke"
   ```

### 方法 2：修改測試以使用已安裝的應用（臨時方案）

如果您已經通過 Visual Studio F5 運行過應用，它可能已經部署。測試將自動檢測並使用已安裝的 MSIX 包。

### 方法 3：使用 PowerShell 腳本部署

創建 `deploy-for-testing.ps1`：
```powershell
# deploy-for-testing.ps1
param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

Write-Host "部署 AIDevGallery 用於測試..." -ForegroundColor Cyan

# 構建並部署
msbuild AIDevGallery\AIDevGallery.csproj `
    /t:Build,Deploy `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /v:minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ 部署成功！" -ForegroundColor Green
    Write-Host ""
    Write-Host "現在可以運行測試：" -ForegroundColor Yellow
    Write-Host "  dotnet test --filter 'TestCategory=Smoke'" -ForegroundColor White
} else {
    Write-Host "✗ 部署失敗" -ForegroundColor Red
    exit 1
}
```

然後運行：
```powershell
.\deploy-for-testing.ps1
```

## 為什麼不能直接運行 .exe？

WinUI3 應用程序有以下依賴：

1. **Windows App SDK 運行時** - 需要通過 MSIX 部署
2. **COM 組件註冊** - `Application.Start()` 需要註冊的 COM 類
3. **應用程序生命週期管理** - `AppInstance` API 需要包清單
4. **自包含部署** - 所有依賴項必須正確部署

這就是為什麼：
- ❌ 直接運行 `AIDevGallery.exe` 失敗
- ✅ 從開始菜單啟動（已部署）成功
- ✅ Visual Studio F5（自動部署）成功

## 測試工作流程

正確的測試流程：

```powershell
# 1. 構建應用
dotnet build AIDevGallery\AIDevGallery.csproj -c Debug /p:Platform=x64

# 2. 部署 MSIX（這一步是關鍵！）
msbuild AIDevGallery\AIDevGallery.csproj /t:Deploy /p:Configuration=Debug /p:Platform=x64

# 3. 運行測試
dotnet test --filter "TestCategory=Smoke"

# 4. 清理（可選）
# Get-AppxPackage | Where-Object {$_.Name -like '*AIDevGallery*'} | Remove-AppxPackage
```

## 驗證清單

在運行測試前，確保：

- [ ] 已構建應用程序
- [ ] 已部署 MSIX 包（通過 Deploy 或 F5）
- [ ] 可以在開始菜單中找到"AI Dev Gallery Dev"
- [ ] 可以手動啟動應用程序
- [ ] 運行 `Get-AppxPackage` 可以看到應用

## 故障排除

### 部署失敗？

```powershell
# 先卸載舊版本
Get-AppxPackage | Where-Object {$_.Name -like '*AIDevGallery*'} | Remove-AppxPackage

# 清理構建輸出
dotnet clean
Remove-Item -Recurse -Force AIDevGallery\bin, AIDevGallery\obj

# 重新構建並部署
dotnet build -c Debug /p:Platform=x64
msbuild AIDevGallery\AIDevGallery.csproj /t:Deploy /p:Configuration=Debug /p:Platform=x64
```

### 測試仍然失敗？

1. **手動測試應用啟動**
   - 從開始菜單啟動"AI Dev Gallery Dev"
   - 如果應用可以啟動，測試應該也能工作

2. **檢查部署狀態**
   ```powershell
   Get-AppxPackage | Where-Object {$_.Name -like '*e7af07c0*'} | Format-List
   ```

3. **查看詳細錯誤**
   ```powershell
   dotnet test --filter "TestCategory=Smoke" --logger "console;verbosity=detailed"
   ```

## 更新測試基礎類（未來改進）

目前測試框架會：
1. ✅ 首先嘗試查找已安裝的 MSIX 包
2. ✅ 如果找到，使用包族名稱啟動
3. ⚠️ 否則回退到 .exe（會失敗並提供清晰錯誤消息）

計劃中的改進：
- 自動部署 MSIX（需要管理員權限）
- 更好的錯誤消息
- 在測試後自動清理

---

**總結：在運行 UI 測試之前，請務必先部署 MSIX 包！**

```powershell
# 快速開始
msbuild AIDevGallery\AIDevGallery.csproj /t:Build,Deploy /p:Configuration=Debug /p:Platform=x64
dotnet test --filter "TestCategory=Smoke"
```
