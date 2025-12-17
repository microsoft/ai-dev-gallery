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
            string automationId;
            try
            {
                automationId = string.IsNullOrEmpty(button.AutomationId) ? "(no id)" : button.AutomationId;
            }
            catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
            {
                automationId = "(not supported)";
            }
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