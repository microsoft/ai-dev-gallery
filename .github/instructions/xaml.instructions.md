---
applyTo: "**/*.xaml"
---

# XAML UI Instructions

When reviewing or modifying XAML files in AI Dev Gallery:

## WinUI 3 Standards

This project uses WinUI 3 with Windows App SDK. Use WinUI controls, not UWP controls.

### Correct Namespaces
```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
xmlns:controls="using:AIDevGallery.Controls"
```

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

### Focus Management
Set initial focus in `Loaded` event:
```csharp
private void Page_Loaded()
{
    InputTextBox.Focus(FocusState.Programmatic);
}
```

## Common Patterns

### Text Input with Button
```xml
<Grid ColumnDefinitions="*, Auto">
    <TextBox x:Name="InputTextBox" 
             PlaceholderText="Enter text..."
             MaxLength="1000"/>
    <Button Grid.Column="1" 
            x:Name="SubmitButton"
            Content="Submit"
            Click="SubmitButton_Click"/>
</Grid>
```

### Loading States
```xml
<ProgressBar x:Name="LoadingProgress"
             IsIndeterminate="True"
             Visibility="Collapsed"/>
```

### Image Display
```xml
<Image x:Name="DisplayImage"
       Stretch="Uniform"
       AutomationProperties.Name="Result image"/>
```

## Code Review Checks

- All controls have appropriate `x:Name` if referenced in code
- Interactive elements are keyboard accessible
- `AutomationProperties.Name` set for screen readers
- Proper use of Grid/StackPanel for layout
- Responsive design considerations
- High contrast theme compatibility
- No hardcoded dimensions where possible

## Theme Compatibility

Use theme resources instead of hardcoded colors:
```xml
<!-- Good -->
<TextBlock Foreground="{ThemeResource TextFillColorPrimary}"/>

<!-- Avoid -->
<TextBlock Foreground="#000000"/>
```

## Sample Page Structure

Standard sample XAML structure:
```xml
<Page x:Class="AIDevGallery.Samples.MySample"
      xmlns="..."
      xmlns:x="...">
    
    <Grid Padding="16" RowDefinitions="Auto,*,Auto">
        <!-- Input area -->
        <StackPanel Grid.Row="0">
            <!-- Input controls -->
        </StackPanel>
        
        <!-- Output/display area -->
        <ScrollViewer Grid.Row="1">
            <!-- Results display -->
        </ScrollViewer>
        
        <!-- Action buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal">
            <!-- Buttons -->
        </StackPanel>
    </Grid>
</Page>
```
