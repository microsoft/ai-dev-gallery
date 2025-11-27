// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// Sample UI tests demonstrating FlaUI basic operations.
/// These tests show how to interact with the AIDevGallery UI.
/// </summary>
[TestClass]
public class BasicInteractionTests : FlaUITestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to find and log all clickable elements")]
    public void Sample_FindAllClickableElements()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Find all buttons and clickable elements
        var buttons = MainWindow.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));

        var hyperlinks = MainWindow.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Hyperlink));

        // Log findings
        Console.WriteLine($"=== Clickable Elements Found ===");
        Console.WriteLine($"Buttons: {buttons.Length}");
        Console.WriteLine($"Hyperlinks: {hyperlinks.Length}");
        Console.WriteLine();

        // Log button details
        Console.WriteLine("=== Buttons ===");
        foreach (var button in buttons.Take(15))
        {
            var name = string.IsNullOrEmpty(button.Name) ? "(no name)" : button.Name;
            var automationId = string.IsNullOrEmpty(button.AutomationId) ? "(no id)" : button.AutomationId;
            Console.WriteLine($"  - {name} [ID: {automationId}]");
        }

        if (buttons.Length > 15)
        {
            Console.WriteLine($"  ... and {buttons.Length - 15} more");
        }

        // Take screenshot
        TakeScreenshot("Sample_ClickableElements");

        // Assert
        Assert.IsTrue(
            buttons.Length > 0 || hyperlinks.Length > 0,
            "Should find at least some clickable elements");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to search for elements by name")]
    public void Sample_SearchElementsByName()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Search for common UI element names
        string[] searchTerms = { "Settings", "Home", "Search", "Menu", "Back", "Close" };

        Console.WriteLine("=== Searching for Common UI Elements ===");
        foreach (var term in searchTerms)
        {
            var elements = MainWindow.FindAllDescendants(cf => cf.ByName(term));
            if (elements.Length > 0)
            {
                Console.WriteLine($"Found '{term}': {elements.Length} element(s)");
                foreach (var element in elements.Take(3))
                {
                    Console.WriteLine($"  - Type: {element.ControlType}, Enabled: {element.IsEnabled}");
                }
            }
            else
            {
                Console.WriteLine($"'{term}': Not found");
            }
        }

        TakeScreenshot("Sample_SearchByName");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to check window properties")]
    public void Sample_InspectWindowProperties()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Get window properties
        Console.WriteLine("=== Main Window Properties ===");
        Console.WriteLine($"Title: {MainWindow.Title}");
        Console.WriteLine($"AutomationId: {MainWindow.AutomationId}");
        Console.WriteLine($"ClassName: {MainWindow.ClassName}");
        Console.WriteLine($"ControlType: {MainWindow.ControlType}");
        Console.WriteLine($"IsEnabled: {MainWindow.IsEnabled}");
        Console.WriteLine($"IsOffscreen: {MainWindow.IsOffscreen}");
        Console.WriteLine($"ProcessId: {MainWindow.Properties.ProcessId.ValueOrDefault}");
        Console.WriteLine();

        var bounds = MainWindow.BoundingRectangle;
        Console.WriteLine($"Position: ({bounds.X}, {bounds.Y})");
        Console.WriteLine($"Size: {bounds.Width}x{bounds.Height}");
        Console.WriteLine();

        // Check available patterns
        Console.WriteLine("=== Available Patterns ===");
        Console.WriteLine($"Transform: {MainWindow.Patterns.Transform.IsSupported}");
        Console.WriteLine($"Window: {MainWindow.Patterns.Window.IsSupported}");
        Console.WriteLine($"Invoke: {MainWindow.Patterns.Invoke.IsSupported}");

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(MainWindow.Title), "Window should have a title");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to find text input elements")]
    public void Sample_FindTextInputElements()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Find all text input elements
        var textBoxes = MainWindow.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));

        var comboBoxes = MainWindow.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));

        // Log findings
        Console.WriteLine("=== Text Input Elements ===");
        Console.WriteLine($"TextBoxes/Edit controls: {textBoxes.Length}");
        Console.WriteLine($"ComboBoxes: {comboBoxes.Length}");
        Console.WriteLine();

        if (textBoxes.Length > 0)
        {
            Console.WriteLine("TextBoxes:");
            foreach (var textBox in textBoxes.Take(10))
            {
                var name = string.IsNullOrEmpty(textBox.Name) ? "(no name)" : textBox.Name;
                var automationId = string.IsNullOrEmpty(textBox.AutomationId) ? "(no id)" : textBox.AutomationId;
                Console.WriteLine($"  - {name} [ID: {automationId}]");
            }
        }

        TakeScreenshot("Sample_TextInputElements");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to navigate the UI tree structure")]
    public void Sample_NavigateUITree()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Navigate the UI tree
        Console.WriteLine("=== UI Tree Structure (Top Level) ===");
        var children = MainWindow.FindAllChildren();

        Console.WriteLine($"Main window has {children.Length} direct children");
        Console.WriteLine();

        foreach (var child in children.Take(10))
        {
            Console.WriteLine($"Child: {child.ControlType}");
            Console.WriteLine($"  Name: {child.Name}");
            Console.WriteLine($"  AutomationId: {child.AutomationId}");
            Console.WriteLine($"  ClassName: {child.ClassName}");

            // Get grand-children count
            var grandChildren = child.FindAllChildren();
            Console.WriteLine($"  Has {grandChildren.Length} children");
            Console.WriteLine();
        }

        if (children.Length > 10)
        {
            Console.WriteLine($"... and {children.Length - 10} more children");
        }

        TakeScreenshot("Sample_UITree");

        // Assert
        Assert.IsTrue(children.Length > 0, "Main window should have child elements");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to use keyboard input")]
    public void Sample_KeyboardInput()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Make sure the window has focus
        MainWindow.Focus();
        System.Threading.Thread.Sleep(500);

        // Act - Send keyboard input
        Console.WriteLine("=== Keyboard Input Demo ===");
        Console.WriteLine("Sending Tab key to navigate...");

        // Send Tab key a few times
        Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
        System.Threading.Thread.Sleep(300);

        Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
        System.Threading.Thread.Sleep(300);

        Console.WriteLine("Keyboard input sent successfully");
        TakeScreenshot("Sample_KeyboardInput");

        // Assert - Just verify the window is still responsive
        Assert.IsTrue(MainWindow.IsAvailable, "Window should still be available after keyboard input");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [Description("Demonstrates how to count different element types")]
    public void Sample_CountElementTypes()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");

        // Act - Count different element types
        var allElements = MainWindow.FindAllDescendants();

        var elementTypeCounts = allElements
            .GroupBy(e => e.ControlType.ToString())
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Log findings
        Console.WriteLine("=== Element Type Statistics ===");
        Console.WriteLine($"Total elements: {allElements.Length}");
        Console.WriteLine();
        Console.WriteLine("Breakdown by type:");

        foreach (var item in elementTypeCounts)
        {
            Console.WriteLine($"  {item.Type}: {item.Count}");
        }

        TakeScreenshot("Sample_ElementTypes");

        // Assert
        Assert.IsTrue(allElements.Length > 0, "Should find UI elements");
        Assert.IsTrue(elementTypeCounts.Count > 0, "Should have multiple element types");
    }
}