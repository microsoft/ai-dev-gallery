# Axe.Windows 无障碍扫描结果报告

**扫描日期**: 2025-12-17  
**扫描工具**: Axe.Windows v2.4.2  
**应用**: AI Dev Gallery Dev  
**框架**: Win32  

---

## 📊 扫描总结

- **总问题数**: 5,508
  - 🔴 **错误 (Status=1)**: 4,970 个
  - 🟡 **警告 (Status=2)**: 538 个

- **扫描覆盖**: 完整的应用窗口及其所有子元素

---

## 🔍 主要问题分类

### 1. **名称属性 (Name Property) 问题** - 最常见
这是最严重的问题类别，影响大量 UI 元素。

常见问题描述:
- `The Name property must not be longer than 512 characters`
- `The Name property must not contain only whitespace`
- `The Name property of a focusable element must not be null`
- `The Name property of a focusable element must not be an empty string`
- `The Name property must not include the element's control type`
- `The Name must not include the same text as the LocalizedControlType`
- `The Name property must not contain any characters in the private Unicode range` (U+E000 to U+F8FF)

**影响**: 屏幕阅读器用户无法准确获得元素的标签信息

**标准**: Section 508 502.3.1

---

### 2. **本地化控制类型 (LocalizedControlType) 问题** - 非常常见
几乎与 Name 属性问题同样严重。

常见问题描述:
- `The LocalizedControlType property must not be null`
- `The LocalizedControlType property must not be an empty string`
- `The LocalizedControlType property must not contain only white space`
- `The LocalizedControlType should be reasonable based on the element's ControlTypeId`
- `The LocalizedControlType property must not contain any characters in the private Unicode range`

**影响**: 辅助技术无法准确理解控件的类型

**标准**: Section 508 502.3.1

---

### 3. **边界矩形 (BoundingRectangle) 问题** - 常见
关于元素的可视位置和大小信息不正确。

常见问题描述:
- `An on-screen element must not have a null BoundingRectangle property`
- `The BoundingRectangle property must not be defined as [0,0,0,0]`
- `The BoundingRectangle property must represent an area of at least 25 pixels`
- `An element's BoundingRectangle must be contained within its parent element`
- `An element's BoundingRectangle must not obscure its container element`
- `The BoundingRectangle property is not valid, but the element is off-screen`

**影响**: 屏幕阅读器用户无法确定元素的准确位置，可能导致交互困难

**标准**: Section 508 502.3.1

---

### 4. **IsControlElement 属性问题** - 常见
控制元素属性未正确设置。

常见问题描述:
- `The given ControlType must have a non-null IsControlElement property`
- `The given ControlType must have the IsControlElement property set to true`

**影响**: 元素在控制视图中的包含/排除状态不明确

**标准**: Section 508 502.3.1

---

### 5. **按钮模式支持问题** - 常见
按钮没有实现所需的 UI 自动化模式。

常见问题描述:
- `A button must support one of these patterns: Invoke, Toggle, or ExpandCollapse`
- `A button must not support both the Invoke and Toggle patterns`

**影响**: 屏幕阅读器用户无法正确激活按钮

**标准**: WCAG 4.1.2

---

### 6. **框架兼容性问题** - 关键
- `The framework used to build this application does not support UI Automation`

**影响**: 这是一个框架级别的问题，表明 Win32 框架对 UI 自动化的支持有限

**标准**: Section 508 502.3.1

---

### 7. **元素关系问题** - 常见
- `Focusable sibling elements must not have the same Name and LocalizedControlType`
- `An element must not have the same Name and LocalizedControlType as its parent`

**影响**: 用户界面中的元素无法被唯一识别

**标准**: Section 508 502.3.1 / WCAG 4.1.2

---

### 8. **模式支持问题** - 中等
- `An element of the given type should not support the Window pattern`
- 某些元素不支持或错误地支持了特定的 UI 自动化模式

**标准**: Section 508 502.3.10

---

## 📋 问题统计按标准分类

| 标准 | 问题数 |
|------|--------|
| Section 508 502.3.1 | ~4,500+ |
| Section 508 502.3.10 | ~100+ |
| WCAG 4.1.2 | ~368 |
| 其他 | ~540 |

---

## 🎯 受影响的元素

扫描发现的主要元素类型及其问题:

1. **Window (窗口)** - 主应用窗口
2. **Pane (面板)** - 多个容器面板
3. **Button (按钮)** - 标题栏按钮 (Minimize, Maximize, Close 等)
4. **TitleBar (标题栏)** - 窗口标题栏元素
5. **其他 UI 元素** - 菜单、文本框、组合框等

---

## ⚠️ 关键发现

1. **大规模命名问题**: 4,970 个错误中，大多数与元素命名和属性设置有关

2. **Win32 框架限制**: 应用使用 Win32 框架，这在 UI 自动化支持方面存在先天不足

3. **私有 Unicode 字符**: 一些元素的名称/控制类型包含私有 Unicode 范围的字符 (U+E000-U+F8FF)，这些是不允许的

4. **尺寸问题**: 许多元素的边界矩形无效或过小 (<25 像素)

5. **模式实现问题**: 按钮等控件未正确实现所需的 UI 自动化模式

---

## 📌 优先级建议

### 🔴 高优先级 (必须修复)
1. 移除所有名称/标签中的私有 Unicode 字符
2. 为所有可聚焦元素提供有效的、有意义的 Name 属性
3. 为所有元素提供有效的 LocalizedControlType 属性
4. 修复边界矩形问题 (确保有效且包含在父元素内)
5. 为按钮实现正确的 UI 自动化模式 (Invoke/Toggle/ExpandCollapse)

### 🟡 中优先级
1. 修复重复的 Name 和 LocalizedControlType 问题
2. 设置正确的 IsControlElement 属性值
3. 移除不需要的 Window 模式支持

### 🔵 低优先级 (长期改进)
1. 考虑迁移到支持更好 UI 自动化的框架 (如 WPF, UWP, 或 WinUI)
2. 实现更完整的 UI 自动化模式支持

---

## 📚 参考资源

- [Section 508 标准](https://www.access-board.gov/ict/#502-interoperability-assistive-technology)
- [WCAG 2.1 标准](https://www.w3.org/TR/WCAG21/)
- [UI 自动化文档](https://docs.microsoft.com/en-us/windows/win32/winauto/about-uia)
- [Axe.Windows 工具](https://accessibilityinsights.io/)

---

## 📝 扫描详情

- **扫描模式**: Complete (完整)
- **发现的元素总数**: 多层级完整 UI 树
- **主要入口点**: AI Dev Gallery Dev 窗口 (WinUIDesktopWin32WindowClass)
- **进程 ID**: 7460 (应用), 9768 (扫描进程)
- **屏幕截图**: 已捕获 (scshot.png)

---

**生成时间**: 2025-12-17  
**报告版本**: 1.0
