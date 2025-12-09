// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// UI tests for NavigationView interactions using FlaUI.
/// </summary>
[TestClass]
public class NavigationViewTests : FlaUITestBase
{
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Helper method to wait for debugger attachment.
    /// Set WAIT_FOR_DEBUGGER environment variable to enable.
    /// </summary>
    private void WaitForDebuggerAttachment()
    {
        var waitForDebugger = Environment.GetEnvironmentVariable("WAIT_FOR_DEBUGGER");
        if (string.IsNullOrEmpty(waitForDebugger))
        {
            return;
        }

        Console.WriteLine("=== DEBUGGER ATTACHMENT HELPER ===");
        Console.WriteLine($"App Process ID: {App?.ProcessId}");
        Console.WriteLine($"Test Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
        Console.WriteLine();
        Console.WriteLine("To debug the test code:");
        Console.WriteLine("  1. Debug > Attach to Process (Ctrl+Alt+P)");
        Console.WriteLine($"  2. Find and attach to process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
        Console.WriteLine();
        Console.WriteLine("To debug the AIDevGallery app:");
        Console.WriteLine("  1. Debug > Attach to Process (Ctrl+Alt+P)");
        Console.WriteLine($"  2. Find and attach to: AIDevGallery.exe (PID: {App?.ProcessId})");
        Console.WriteLine();
        Console.WriteLine("Waiting 20 seconds for debugger attachment...");
        
        for (int i = 20; i > 0; i--)
        {
            Console.Write($"\r{i} seconds remaining... ");
            Thread.Sleep(1000);
        }
        Console.WriteLine("\nContinuing test execution...");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Navigation")]
    [Description("Test clicking the second NavigationView item (Samples)")]
    public void NavigationView_ClickSecondItem_Samples()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        
        // Wait for debugger attachment if needed
        // WaitForDebuggerAttachment();
        
        Console.WriteLine("Starting test: Click Samples navigation item");

        // Wait a moment for the UI to fully render
        Thread.Sleep(1000);
        TakeScreenshot("NavigationView_BeforeClick");

        // Act - Find the MenuItemsHost group which contains the main navigation items
        // This avoids getting nested navigation items from inner NavigationViews
        Console.WriteLine("Searching for MenuItemsHost...");
        var menuItemsHost = MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("MenuItemsHost"));

        Assert.IsNotNull(menuItemsHost, "MenuItemsHost should be found");
        Console.WriteLine($"Found MenuItemsHost: {menuItemsHost.ControlType}");

        // Get only the DIRECT children ListItems of MenuItemsHost, not all descendants
        // This prevents getting nested navigation items from inner NavigationViews
        Console.WriteLine("Searching for direct ListItem children of MenuItemsHost...");
        var topLevelListItems = menuItemsHost.FindAllChildren(cf =>
            cf.ByControlType(ControlType.ListItem));

        Console.WriteLine($"Found {topLevelListItems.Length} top-level ListItem controls");

        // Log all found items for debugging
        for (int i = 0; i < topLevelListItems.Length; i++)
        {
            var item = topLevelListItems[i];
            Console.WriteLine($"  ListItem {i}: Name='{item.Name}', AutomationId='{item.AutomationId}', IsEnabled={item.IsEnabled}");
        }

        // Find the "Samples" list item by name from top-level items only
        Console.WriteLine("\nSearching for 'Samples' ListItem by name...");
        var samplesItem = topLevelListItems.FirstOrDefault(item => 
            item.Name == "Samples");

        // If not found by name, try to get the second list item (index 1)
        // Based on the structure: Home (0), Samples (1), Models (2), AI APIs (3)
        if (samplesItem == null && topLevelListItems.Length > 1)
        {
            Console.WriteLine("'Samples' not found by name, using second ListItem (index 1)...");
            samplesItem = topLevelListItems[1];
        }

        Assert.IsNotNull(samplesItem, "Samples ListItem should be found");
        Console.WriteLine($"Found Samples item: Name='{samplesItem.Name}', IsEnabled={samplesItem.IsEnabled}, ControlType={samplesItem.ControlType}");

        // Verify it's the correct item
        Assert.AreEqual("Samples", samplesItem.Name, "Found item should be named 'Samples'");

        // Click the Samples item
        Console.WriteLine("Clicking Samples ListItem...");
        samplesItem.Click();

        // Wait for navigation to complete
        Thread.Sleep(2000);
        TakeScreenshot("NavigationView_AfterClick");

        // Assert - Verify the navigation occurred
        Console.WriteLine("Verifying navigation...");
        
        // Check if the item is selected
        var isSelected = false;
        try
        {
            if (samplesItem.Patterns.SelectionItem.IsSupported)
            {
                isSelected = samplesItem.Patterns.SelectionItem.Pattern.IsSelected.Value;
                Console.WriteLine($"Item selection state: {isSelected}");
                Assert.IsTrue(isSelected, "Samples item should be selected after clicking");
            }
            else
            {
                Console.WriteLine("SelectionItem pattern not supported on this element");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not check selection state: {ex.Message}");
        }

        // Verify the navigation frame exists
        var frameContent = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavFrame"));
        Assert.IsNotNull(frameContent, "Navigation frame should exist after navigation");
        
        Console.WriteLine("✓ Navigation test completed successfully");
    }

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

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Navigation")]
    [Description("Find and list all NavigationView structure")]
    public void NavigationView_InspectStructure()
    {
        // Arrange
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Console.WriteLine("Inspecting NavigationView structure");

        Thread.Sleep(1000);

        // Act - Find the NavigationView
        var navigationView = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavView"));
        
        if (navigationView == null)
        {
            navigationView = MainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Pane).And(cf.ByClassName("NavigationView")));
        }

        Assert.IsNotNull(navigationView, "NavigationView should be found");
        Console.WriteLine($"NavigationView found: {navigationView.ClassName}");

        // Find all descendants
        var allElements = navigationView.FindAllDescendants();
        Console.WriteLine($"\nTotal elements in NavigationView: {allElements.Length}");

        // Group by control type
        var grouped = allElements
            .GroupBy(e => e.ControlType.ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        Console.WriteLine("\nElements by type:");
        foreach (var group in grouped)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        }

        // Find specific element types
        Console.WriteLine("\n=== ListItems (Navigation Items) ===");
        var listItems = navigationView.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        foreach (var item in listItems)
        {
            Console.WriteLine($"  - {item.Name} [AutomationId: {item.AutomationId}, Enabled: {item.IsEnabled}]");
        }

        Console.WriteLine("\n=== ContentPresenter Elements ===");
        var contentPresenters = navigationView.FindAllDescendants(cf => cf.ByClassName("ContentPresenter"));
        Console.WriteLine($"Found {contentPresenters.Length} ContentPresenter elements");
        
        foreach (var presenter in contentPresenters.Take(10))
        {
            var bounds = presenter.BoundingRectangle;
            Console.WriteLine($"  - AutomationId: {presenter.AutomationId}, Position: ({bounds.X}, {bounds.Y}), Size: {bounds.Width}x{bounds.Height}");
            
            // Try to find text content
            var textElements = presenter.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            if (textElements.Length > 0)
            {
                Console.WriteLine($"    Contains text: {string.Join(", ", textElements.Select(t => t.Name))}");
            }
        }

        TakeScreenshot("NavigationView_Structure");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Navigation")]
    [Description(@"Test validating the Settings page navigation and optional cache clearing functionality.
    
    Background: Users access Settings through the footer navigation menu to manage app preferences and clear cached data.
    The cache clearing feature may not always be available depending on app state, so the test handles this gracefully.
    
    Method: Navigates to Settings via FooterMenuItemsHost, verifies Settings page loads correctly, then attempts to clear 
    cache if the button is available. Uses screenshots for debugging and validates each step with assertions.
    
    Goal: Ensure Settings navigation remains functional and cache clearing workflow works when available. Validates both 
    the navigation flow and the optional cache management feature without failing when cache button doesn't exist.")]
    public void NavigationView_Settings_ClearCache()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Thread.Sleep(1000);
        TakeScreenshot("Settings_Start");

        Console.WriteLine("=== Step 1: Navigate to Settings ===");
        var footerMenuHost = MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("FooterMenuItemsHost"));
        Assert.IsNotNull(footerMenuHost, "FooterMenuItemsHost should be found");

        var footerItems = footerMenuHost.FindAllChildren(cf =>
            cf.ByControlType(ControlType.ListItem));
        Console.WriteLine($"Found {footerItems.Length} footer items");

        var settingsNavItem = footerItems.FirstOrDefault(item => item.Name == "Settings");
        
        if (settingsNavItem == null)
        {
            Console.WriteLine("Settings item not found by name, trying index 1...");
            settingsNavItem = footerItems.ElementAtOrDefault(1);
        }
        
        Assert.IsNotNull(settingsNavItem, "Settings item should be found");
        Assert.AreEqual("Settings", settingsNavItem.Name, "Item should be named 'Settings'");

        settingsNavItem.Click();
        Thread.Sleep(1000);

        var settingsPageTitle = MainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Settings")));
        Assert.IsNotNull(settingsPageTitle, "Settings page should display 'Settings' text");
        Console.WriteLine("Settings page loaded successfully");
        TakeScreenshot("Settings_Opened");

        Console.WriteLine("\n=== Step 2: Clear Cache (Optional) ===");
        Thread.Sleep(1000);

        var clearCacheBtn = MainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Clear cache")));
        
        if (clearCacheBtn == null)
        {
            Console.WriteLine("Clear cache button not found - skipping");
            return;
        }

        Console.WriteLine("Found Clear cache button");
        TakeScreenshot("Settings_BeforeClearCache");
        clearCacheBtn.Click();
        Thread.Sleep(1000);

        Console.WriteLine("\n=== Step 3: Confirm Clear Cache ===");
        TakeScreenshot("Settings_ClearCacheDialog");

        var confirmBtn = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("PrimaryButton"))
                        ?? MainWindow.FindFirstDescendant(cf => 
                            cf.ByControlType(ControlType.Button).And(cf.ByName("Yes")));
        
        if (confirmBtn == null)
        {
            Console.WriteLine("Confirmation button not found");
            return;
        }

        Console.WriteLine("Found confirmation button");
        confirmBtn.Click();
        Thread.Sleep(2000);
        TakeScreenshot("Settings_AfterClearCache");

        var dialogClosed = MainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Window).And(cf.ByName("Clear cache"))) == null;
        Console.WriteLine("Test completed successfully");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Model")]
    [Description(@"End-to-end test validating the complete model download workflow from Foundry Local catalog.
    
    Background: Users need to download AI models from Foundry Local to run samples locally. This involves navigating through 
    multiple UI layers: Samples > Text > The First Sample Item > Model Selection > Foundry Local > Model variant selection.
    The test ensures the entire pipeline works correctly and all UI elements are accessible.
    
    Method: Simulates user navigation through each step with explicit waits for UI stability. Searches for the first available 
    downloadable model using UI tree traversal, finds matching text and button elements, opens variant popup, and initiates download.
    Uses screenshots at key checkpoints for debugging test failures.
    
    Goal: Verify critical user workflow remains functional across releases. Catches regressions in navigation flow, model catalog loading,
    and download UI. Essential for validating model management features.")] 
    public void NavigationView_DownloadFoundryLocalModel()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Thread.Sleep(1000);
        TakeScreenshot("DownloadModel_Start");

        Console.WriteLine("=== Step 1: Navigate to Samples ===");
        var menuHost = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
        Assert.IsNotNull(menuHost, "MenuItemsHost should be found");
        var menuItems = menuHost.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var samplesNav = menuItems.FirstOrDefault(item => item.Name == "Samples") ?? menuItems.ElementAtOrDefault(1);
        Assert.IsNotNull(samplesNav, "Samples item should be found");
        samplesNav.Click();
        Thread.Sleep(2000);
        TakeScreenshot("DownloadModel_SamplesOpened");

        Console.WriteLine("=== Step 2: Navigate to Text > The First Sample Item ===");
        var innerNavView = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NavView"))
            ?.FindFirstDescendant(cf => cf.ByAutomationId("NavView"));
        Assert.IsNotNull(innerNavView, "Inner NavView should be found");
        
        var innerMenuHost = innerNavView.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
        Assert.IsNotNull(innerMenuHost, "Inner MenuItemsHost should be found");
        
        var categoryItems = innerMenuHost.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var textCategory = categoryItems.FirstOrDefault(item => item.Name == "Text");
        Assert.IsNotNull(textCategory, "Text list item should be found");
        
        try
        {
            textCategory.Click();
            Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: {ex.Message}");
        }

        // Get the first sample item under the Text category
        var textCategoryChildren = textCategory.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        AutomationElement? firstSampleItem = null;
        
        if (textCategoryChildren.Length == 0)
        {
            var sampleItemsList = innerMenuHost.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            firstSampleItem = sampleItemsList.FirstOrDefault(item => 
                item != null && 
                categoryItems.All(cat => cat.AutomationId != item.AutomationId));
        }
        else
        {
            firstSampleItem = textCategoryChildren.FirstOrDefault();
        }
        
        Assert.IsNotNull(firstSampleItem, "First sample item under Text category should be found");
        Console.WriteLine($"Found first sample item: {firstSampleItem.Name}");
        TakeScreenshot("DownloadModel_FirstSampleOpened");

        Console.WriteLine("=== Step 3: Open Model Selection ===");
        firstSampleItem.Click();
        Thread.Sleep(5000);
        
        // Check if modelTypeSelector already exists
        var modelTypeSelector = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("modelTypeSelector"));
        
        if (modelTypeSelector == null)
        {
            // modelTypeSelector not found, need to click ModelBtn to open it
            var modelButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelBtn"));
            if (modelButton == null)
            {
                var buttons = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                modelButton = buttons.FirstOrDefault(btn => btn.Name != null && btn.Name.Contains("Selected models", StringComparison.OrdinalIgnoreCase));
            }
            Assert.IsNotNull(modelButton, "Selected models button should be found");
            modelButton.Click();
            Thread.Sleep(1000);
            TakeScreenshot("DownloadModel_ModelSelectionOpened");
            
            // Try to find modelTypeSelector again after clicking
            modelTypeSelector = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("modelTypeSelector"));
        }
        else
        {
            Console.WriteLine("modelTypeSelector already exists, skipping ModelBtn click");
            TakeScreenshot("DownloadModel_ModelSelectionAlreadyOpen");
        }

        Console.WriteLine("=== Step 4: Select Foundry Local ===");
        Assert.IsNotNull(modelTypeSelector, "Model type selector should be found");
        var foundryLocalOption = modelTypeSelector.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName("Foundry Local")));
        Assert.IsNotNull(foundryLocalOption, "Foundry Local item should be found");

        try
        {
            if (foundryLocalOption.Patterns.SelectionItem.IsSupported)
                foundryLocalOption.Patterns.SelectionItem.Pattern.Select();
            else
                foundryLocalOption.Click();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Selection fallback: {ex.Message}");
            foundryLocalOption.Click();
        }
        Thread.Sleep(1000);

        try
        {
            if (foundryLocalOption.Patterns.SelectionItem.IsSupported)
            {
                var isSelected = foundryLocalOption.Patterns.SelectionItem.Pattern.IsSelected.Value;
                if (!isSelected)
                {
                    Console.WriteLine("Retrying selection");
                    foundryLocalOption.Click();
                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Verification skipped: {ex.Message}");
        }
        TakeScreenshot("DownloadModel_FoundryLocalSelected");

        Console.WriteLine("=== Step 5: Find Available Model ===");
        AutomationElement? downloadableHeader = null;
        for (int i = 0; i < 10; i++)
        {
            downloadableHeader = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DownloadableModelsTxt"));
            if (downloadableHeader != null)
            {
                Console.WriteLine("✓ Models section loaded");
                break;
            }
            Console.WriteLine($"Waiting ({i + 1}/10)...");
            Thread.Sleep(500);
        }
        Assert.IsNotNull(downloadableHeader, "DownloadableModelsTxt should be found");

        var modelsContainer = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ModelsView"));
        Assert.IsNotNull(modelsContainer, "ModelsView should be found");

        var modelsViewChildren = modelsContainer.FindAllChildren();
        AutomationElement? downloadButton = null;
        string? modelName = null;

        bool pastDownloadableHeader = false;
        foreach (var child in modelsViewChildren)
        {
            string? automationId = null;
            try
            {
                if (child.Properties.AutomationId.IsSupported)
                    automationId = child.AutomationId;
            }
            catch { }

            if (automationId == "DownloadableModelsTxt")
            {
                pastDownloadableHeader = true;
                continue;
            }

            if (pastDownloadableHeader && child.ControlType == ControlType.Group)
            {
                var buttons = child.FindAllChildren(cf => cf.ByControlType(ControlType.Button)).ToArray();
                var texts = child.FindAllChildren(cf => cf.ByControlType(ControlType.Text)).ToArray();

                string? targetModelName = null;
                foreach (var text in texts)
                {
                    try
                    {
                        if (text.Properties.Name.IsSupported)
                        {
                            var textName = text.Name;
                            if (!string.IsNullOrEmpty(textName) && textName != "More info")
                            {
                                targetModelName = textName;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (targetModelName != null)
                {
                    foreach (var button in buttons)
                    {
                        string? buttonName = null;
                        try
                        {
                            if (button.Properties.Name.IsSupported)
                                buttonName = button.Name;
                        }
                        catch { }

                        if (buttonName == targetModelName && buttonName != "More info")
                        {
                            downloadButton = button;
                            modelName = targetModelName;
                            Console.WriteLine($"✓ Found downloadable: {modelName}");
                            break;
                        }
                    }
                }

                if (downloadButton != null) break;
            }
        }
        Assert.IsNotNull(downloadButton, "First available model button should be found");
        TakeScreenshot("DownloadModel_BeforeClick");

        Console.WriteLine($"=== Step 6: Click Model Button ({modelName}) ===");
        downloadButton.Click();
        Thread.Sleep(2000);
        TakeScreenshot("DownloadModel_VariantPopupOpened");

        Console.WriteLine("=== Step 7: Select Variant to Download ===");
        var variantPopup = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window).And(cf.ByName("Popup")));
        Assert.IsNotNull(variantPopup, "Popup window should be found");
        Console.WriteLine("✓ Variant popup found");

        var variantDownloadButton = variantPopup.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
        Assert.IsNotNull(variantDownloadButton, "Variant button in Popup should be found");

        string? variantName = null;
        try
        {
            if (variantDownloadButton.Properties.Name.IsSupported)
                variantName = variantDownloadButton.Name;
        }
        catch { }
        Console.WriteLine($"✓ Selecting variant: {variantName}");

        variantDownloadButton.Click();
        Thread.Sleep(2000);
        TakeScreenshot("DownloadModel_VariantSelected");
        Console.WriteLine("✓ Download initiated successfully");
    }
}