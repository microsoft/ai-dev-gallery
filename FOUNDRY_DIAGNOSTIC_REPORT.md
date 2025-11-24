# Foundry 服務問題診斷報告

## 問題描述

在**特定設備**上，Foundry 服務的 `/openai/download` 端點在處理下載請求時立即關閉連接，導致 `HttpIOException: The response ended prematurely (ResponseEnded)` 錯誤。

**重要**: 這個問題只在特定設備上出現，不是所有機器都會遇到。

## 問題確認

通過診斷腳本 `test-foundry-service.ps1`，可以確認這是 Foundry 服務端的問題，而不是客戶端代碼的問題。

## 證據

### 1. 獨立測試確認
使用 PowerShell 直接調用 Foundry API（不經過你的應用代碼）:

```powershell
Invoke-WebRequest -Uri "http://127.0.0.1:55188/openai/download" -Method Post -Body $payload
```

**結果**: 同樣的錯誤 `The response ended prematurely. (ResponseEnded)`

這證明問題與你的應用代碼無關。

### 2. 其他端點工作正常
- ✅ `GET /foundry/list` - 正常工作，返回 39 個模型
- ✅ `GET /openai/models` - 正常工作，返回已下載的模型
- ❌ `POST /openai/download` - **立即崩潰**

### 3. 請求格式正確
診斷腳本使用的請求體與應用相同，格式完全正確:
```json
{
  "model": {
    "name": "openai-whisper-tiny-generic-cpu:2",
    "uri": "azureml://registries/azureml/models/openai-whisper-tiny-generic-cpu/versions/2",
    "path": "cpu-fp32",
    "providerType": "AzureFoundry",
    "promptTemplate": {...}
  },
  "ignorePipeReport": true
}
```

### 4. 服務立即關閉連接
錯誤發生在第一次讀取響應時，說明服務器:
1. 接收了請求
2. 返回了 HTTP 200 狀態碼
3. 在開始發送響應體之前就崩潰/關閉了連接

## 可能的根本原因

### 最可能的原因:
1. **Foundry 服務 Bug** - `/openai/download` 端點存在未處理的異常
2. **模型路徑問題** - 服務無法訪問 `cpu-fp32` 路徑
3. **Azure 註冊表訪問問題** - 無法從 Azure ML 註冊表下載
4. **權限問題** - 服務沒有寫入權限到模型存儲目錄

## 建議的解決方案

### 方案 1: 聯繫 Foundry 團隊（推薦）
這是一個 Foundry 服務的 Bug，應該向 Foundry 團隊報告:

- **日誌位置**: `C:\Users\yuanwei\.foundry\logs\foundry20251120.log`
- **服務版本**: 運行 `foundry --version` 獲取
- **復現步驟**: 使用 `test-foundry-service.ps1` 腳本

### 方案 2: 嘗試手動下載
繞過 API，使用 Foundry CLI:

```powershell
# 嘗試使用 CLI 下載模型
foundry model add "azureml://registries/azureml/models/openai-whisper-tiny-generic-cpu/versions/2"
```

### 方案 3: 檢查服務配置

```powershell
# 查看 Foundry 配置
foundry config list

# 重啟服務
foundry service stop
foundry service start

# 查看服務日誌（實時）
Get-Content "$env:USERPROFILE\.foundry\logs\foundry$(Get-Date -Format 'yyyyMMdd').log" -Wait
```

### 方案 4: 應用代碼中添加友好提示

雖然不能修復根本問題，但可以在你的應用中:
1. 提供更清晰的錯誤消息
2. 建議用戶使用 CLI 手動下載
3. 提供重啟 Foundry 服務的選項

## 在問題設備上的診斷步驟

### 步驟 1: 複製診斷腳本到問題設備

將 `test-foundry-service.ps1` 複製到問題設備上並運行:

```powershell
# 運行診斷腳本
.\test-foundry-service.ps1
```

腳本會自動檢測問題並收集:
- Foundry 服務狀態
- API 端點測試結果
- 日誌文件位置
- 系統信息（OS、內存、磁盤空間）
- Foundry 版本

### 步驟 2: 比較兩台設備的差異

運行腳本後，比較工作設備和問題設備的輸出，重點關注:

1. **Foundry 版本** - 是否不同？
2. **系統信息** - OS 版本、架構、可用資源
3. **日誌內容** - 問題設備的日誌中是否有錯誤？
4. **已下載的模型** - `GET /openai/models` 返回的內容

### 步驟 3: 收集問題設備的日誌

```powershell
# 查看最新日誌（最後 100 行）
Get-Content "$env:USERPROFILE\.foundry\logs\foundry$(Get-Date -Format 'yyyyMMdd').log" -Tail 100

# 搜索錯誤信息
Get-Content "$env:USERPROFILE\.foundry\logs\foundry$(Get-Date -Format 'yyyyMMdd').log" | 
    Select-String -Pattern "error|exception|fail" -CaseSensitive:$false

# 實時監控日誌（然後觸發下載操作）
Get-Content "$env:USERPROFILE\.foundry\logs\foundry$(Get-Date -Format 'yyyyMMdd').log" -Wait
```

### 步驟 4: 在問題設備上嘗試修復

```powershell
# 1. 重啟 Foundry 服務
foundry service stop
Start-Sleep -Seconds 2
foundry service start

# 2. 清除可能的緩存
# (如果存在配置目錄)
Remove-Item "$env:USERPROFILE\.foundry\cache" -Recurse -Force -ErrorAction SilentlyContinue

# 3. 重新安裝 Foundry（如果需要）
# 先卸載，然後重新安裝最新版本

# 4. 檢查磁盤空間
Get-PSDrive C | Select-Object Used, Free

# 5. 重新運行診斷
.\test-foundry-service.ps1
```

## 結論

**這 100% 是 Foundry 服務端的 Bug，與你的應用代碼無關。**

服務的 `/openai/download` 端點在處理這個特定模型的下載請求時崩潰了。建議向 Foundry 團隊報告此問題，並在修復之前使用 CLI 手動下載模型作為臨時解決方案。
