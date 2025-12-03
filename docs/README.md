# FlaUI UI 自動化測試

本文件夾包含使用 FlaUI 5.0.0 編寫的 UI 自動化測試。

## 概述

FlaUI 是一個基於 Microsoft UI Automation 的 .NET 庫，用於自動化測試 Windows 桌面應用程序。本項目使用 FlaUI.UIA3 來測試 AIDevGallery 應用程序。

## 文件結構

```
UITests/
├── FlaUITestBase.cs           # 基礎測試類
├── MainWindowTests.cs         # 主窗口測試
├── BasicInteractionTests.cs   # 基本交互示例測試
└── README.md                  # 本文件
```

## 快速開始

### 🚀 使用 Visual Studio 2022？

請查看 **[VS2022 快速參考](VS2022_QUICK_REFERENCE.md)** - 包含截圖、快捷鍵和故障排除！

**三步完成：**
1. 按 F5（自動部署 MSIX）
2. 按 Ctrl+E, T（打開 Test Explorer）
3. 運行測試 ✅

---

### 前提條件

1. 已部署 AIDevGallery MSIX 包（通過 VS F5 或部署命令）
2. .NET 9.0 SDK
3. Windows 10/11（支持 UIA3）

### 運行測試

```powershell
# 從解決方案根目錄運行所有 UI 測試
dotnet test --filter "TestCategory=UI"

# 運行特定測試類
dotnet test --filter "FullyQualifiedName~MainWindowTests"

# 運行示例測試
dotnet test --filter "TestCategory=Sample"

# 使用 Visual Studio Test Explorer
# 打開 Test Explorer，按類別篩選 "UI"
```

### 查看測試輸出

測試運行時會生成：
- **控制台日誌**: 詳細的測試步驟和元素信息
- **截圖**: 保存在 `bin\x64\[Debug|Release]\Screenshots\` 目錄

## 測試類說明

### FlaUITestBase

基礎測試類，提供以下功能：

- **應用啟動**: 自動找到並啟動 AIDevGallery.exe
- **窗口管理**: 獲取主窗口並等待就緒
- **清理**: 測試後自動關閉應用程序
- **輔助方法**:
  - `WaitForElement()` - 等待元素出現
  - `TakeScreenshot()` - 保存截圖

**使用方式:**
```csharp
[TestClass]
public class YourTests : FlaUITestBase
{
    [TestMethod]
    public void YourTest()
    {
        // MainWindow 和 Automation 已初始化
        var button = MainWindow.FindFirstDescendant(
            cf => cf.ByAutomationId("MyButton"));
        button.Click();
    }
}
```

### MainWindowTests

測試主窗口的基本功能：

- ✅ 應用程序啟動
- ✅ 窗口可見性
- ✅ 窗口大小調整
- ✅ UI 元素存在性
- ✅ 按鈕和文本元素查找
- ✅ 窗口關閉

### BasicInteractionTests

演示 FlaUI 的各種用法（示例測試）：

- 🔍 查找可點擊元素
- 🔍 按名稱搜索元素
- 📊 檢查窗口屬性
- 📝 查找文本輸入元素
- 🌲 導航 UI 樹結構
- ⌨️ 鍵盤輸入
- 📊 統計元素類型

## 編寫新測試

### 基本模板

```csharp
using FlaUI.Core.AutomationElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.UnitTests.UITests;

[TestClass]
public class MyFeatureTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [Description("測試說明")]
    public void Test_MyFeature()
    {
        // Arrange - 準備測試數據
        Assert.IsNotNull(MainWindow);

        // Act - 執行操作
        var button = MainWindow.FindFirstDescendant(
            cf => cf.ByAutomationId("MyButton"));
        button.Click();

        // Assert - 驗證結果
        var result = WaitForElement("ResultText", TimeSpan.FromSeconds(5));
        Assert.IsNotNull(result);
        
        // 可選：截圖
        TakeScreenshot("MyFeature_Result");
    }
}
```

### 查找元素的常用方法

```csharp
// 按 AutomationId 查找（推薦）
var element = MainWindow.FindFirstDescendant(
    cf => cf.ByAutomationId("MyElementId"));

// 按名稱查找
var element = MainWindow.FindFirstDescendant(
    cf => cf.ByName("Button Name"));

// 按控件類型查找
var buttons = MainWindow.FindAllDescendants(
    cf => cf.ByControlType(ControlType.Button));

// 組合條件
var element = MainWindow.FindFirstDescendant(cf => 
    cf.ByControlType(ControlType.Button)
      .And(cf.ByName("Submit")));

// 查找子元素
var children = element.FindAllChildren();

// XPath 查找（高級）
var element = MainWindow.FindFirstByXPath("//Button[@Name='Submit']");
```

### 常見操作

```csharp
// 點擊
button.Click();

// 輸入文本
textBox.AsTextBox().Text = "Hello";

// 獲取文本
var text = textElement.Name;

// 檢查狀態
var isEnabled = element.IsEnabled;
var isVisible = !element.IsOffscreen;

// 等待條件
Retry.WhileTrue(() => element.IsOffscreen, 
    timeout: TimeSpan.FromSeconds(5));
```

## 調試技巧

### 1. 使用 Inspect.exe

Windows SDK 自帶的工具，用於查看 UI 元素：

```powershell
# 查找 Inspect.exe
"%ProgramFiles(x86)%\Windows Kits\10\bin\*\x64\inspect.exe"
```

使用步驟：
1. 運行 Inspect.exe
2. 將鼠標移動到應用程序的元素上
3. 查看 AutomationId、Name、ControlType 等屬性

### 2. 添加日誌

```csharp
Console.WriteLine($"Element found: {element.Name}");
Console.WriteLine($"AutomationId: {element.AutomationId}");
```

### 3. 截圖調試

```csharp
TakeScreenshot("Debug_Step1");
// ... 執行操作
TakeScreenshot("Debug_Step2");
```

### 4. 列出所有元素

```csharp
var allElements = MainWindow.FindAllDescendants();
foreach (var elem in allElements.Take(20))
{
    Console.WriteLine($"{elem.ControlType}: {elem.Name} [{elem.AutomationId}]");
}
```

## 最佳實踐

### ✅ 推薦做法

1. **使用 AutomationId** 而不是 Name（更穩定）
2. **添加等待** 讓 UI 有時間更新
3. **清理資源** 使用 `TestCleanup` 關閉應用
4. **截圖記錄** 失敗時保存截圖
5. **避免硬編碼延遲** 使用 `WaitForElement` 或 `Retry`

### ❌ 避免做法

1. ❌ 不要依賴固定的 Sleep() 延遲
2. ❌ 不要假設元素立即可用
3. ❌ 不要在一個測試中測試太多內容
4. ❌ 不要忘記處理異常情況
5. ❌ 不要共享狀態（每個測試應獨立）

## 常見問題

### Q: 測試找不到 AIDevGallery.exe？

**A:** 確保已構建應用程序：
```powershell
# 必須構建主應用程序
dotnet build AIDevGallery\AIDevGallery.csproj -c Debug /p:Platform=x64
```

### Q: 出現 "Class not registered (0x80040154)" 錯誤？

**A:** 這是 WinUI3 應用程序在未打包模式下運行的已知問題。解決方案：

1. **已修復**：Program.cs 已更新以在測試模式下繞過 AppInstance 檢查
2. **確保重新構建**：修改 Program.cs 後需要重新構建主應用程序
3. **測試模式**：測試框架會自動設置 `AIDEVGALLERY_TEST_MODE=1` 環境變量

```powershell
# 重新構建主應用程序
dotnet build AIDevGallery\AIDevGallery.csproj -c Debug /p:Platform=x64

# 然後運行測試
dotnet test --filter "TestCategory=Smoke"
```

### Q: 測試超時或窗口未出現？

**A:** 可能原因：
1. 應用啟動慢 - 增加 `TestInitialize` 中的超時時間
2. 另一個實例在運行 - 基礎類會自動清理
3. 應用崩潰 - 檢查應用日誌

### Q: 找不到元素？

**A:** 調試步驟：
1. 使用 Inspect.exe 確認元素的 AutomationId
2. 檢查元素是否在視圖中（不是 IsOffscreen）
3. 添加等待時間讓 UI 加載完成
4. 嘗試按其他屬性查找（Name、ControlType）

### Q: 測試在 CI/CD 中失敗？

**A:** 確保：
1. CI 代理有圖形界面（不是 headless）
2. 應用程序已正確構建
3. 有足夠的權限運行應用程序
4. 環境變量正確設置

### Q: 如何測試需要模型的功能？

**A:** 選項：
1. 使用模擬數據（推薦用於快速測試）
2. 下載小型測試模型
3. 跳過實際推理，只測試 UI 流程

## 性能考慮

- **並行測試**: 小心並行運行 UI 測試，可能導致窗口衝突
- **資源清理**: 每個測試後確保關閉應用程序
- **測試隔離**: 不要依賴測試執行順序
- **超時設置**: 合理設置超時，避免測試掛起

## 相關資源

- **FlaUI 文檔**: https://github.com/FlaUI/FlaUI/wiki
- **UI Automation 概述**: https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32
- **技術對比文檔**: [UI-Testing-Technology-Comparison.md](../../docs/UI-Testing-Technology-Comparison.md)
- **測試設計文檔**: [Automated-Testing-Design.md](../../docs/Automated-Testing-Design.md)

## 維護

如有問題或建議，請提交 Issue 或 Pull Request。

---

**最後更新**: 2025-11-25  
**FlaUI 版本**: 5.0.0  
**維護者**: 開發團隊
