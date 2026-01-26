---
applyTo: "**/*.xaml"
---

# XAML UI Instructions

When reviewing or modifying XAML files in AI Dev Gallery:

## WinUI 3 Standards

This project uses WinUI 3 with Windows App SDK. Use WinUI controls, not UWP controls.

## Accessibility Requirements

### All Interactive Elements
- Must be keyboard navigable (`IsTabStop="True"` where needed)
- Must have accessible names (`AutomationProperties.Name`)
- Must support high contrast themes

### Screen Reader Support
Use `NarratorHelper` in code-behind for dynamic announcements:
```csharp
NarratorHelper.Announce(control, "Message", "UniqueAnnouncementId");
```

## Code Review Checks

- All controls have appropriate `x:Name` if referenced in code
- Interactive elements are keyboard accessible
- `AutomationProperties.Name` set for screen readers
- Proper use of Grid/StackPanel for layout
- Responsive design considerations
- High contrast theme compatibility
