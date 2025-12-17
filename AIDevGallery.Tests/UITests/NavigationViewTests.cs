// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// UI tests for NavigationView interactions using FlaUI.
/// </summary>
[TestClass]
public class NavigationViewTests : FlaUITestBase
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Navigation")]
    [Description("Test clicking all NavigationView items")]
    public void NavigationView_ClickAllLeftMenuHostItems()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Console.WriteLine("Starting test: Click all navigation items");

        Thread.Sleep(1000);

        // Act - Find the MenuItemsHost to get only top-level navigation items
        var menuItemsHost = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));

        Assert.IsNotNull(menuItemsHost, "MenuItemsHost should be found");

        // Find only DIRECT children ListItems, not all descendants
        // This prevents getting nested navigation items from inner NavigationViews
        var navigationItems = menuItemsHost.FindAllChildren(cf =>
            cf.ByControlType(ControlType.ListItem))
            .Where(item => item.IsEnabled && item.IsOffscreen == false)
            .ToArray();

        Console.WriteLine($"Found {navigationItems.Length} enabled navigation items");

        Assert.IsTrue(navigationItems.Length > 0, "Should have at least one navigation item");

        // Click each item
        foreach (var item in navigationItems)
        {
            Console.WriteLine($"\nClicking navigation item: {item.Name}");

            try
            {
                item.Click();
                Thread.Sleep(1000);

                var screenshotName = $"NavigationView_Item_{item.Name?.Replace(" ", "_") ?? "Unknown"}";
                TakeScreenshot(screenshotName);

                Console.WriteLine($"Successfully clicked: {item.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to click item {item.Name}: {ex.Message}");
            }
        }

        // Assert
        Console.WriteLine("\nNavigation item clicking test completed");
    }
}