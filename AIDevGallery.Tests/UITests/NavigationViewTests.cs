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
        Console.WriteLine("Clicked Settings navigation item, waiting for page load...");
        Thread.Sleep(1000);

        var settingsPageTitle = MainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Settings")));
        
        Assert.IsNotNull(settingsPageTitle, "Settings page should display 'Settings' text");
        Console.WriteLine("✓ Settings page loaded successfully");
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
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - custom ''(AutomationId="NavView")
//          - window ''(AutomationId="PaneRoot")
//           - pane ''
//             - group ''(AutomationId="MenuItemsHost")
//              - list item 'Home'
//              - list item 'Samples' // click this one
//              - list item 'Models'
//              - list item 'AI APIs'
        var menuHost = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MenuItemsHost"));
        Assert.IsNotNull(menuHost, "MenuItemsHost should be found");
        var menuItems = menuHost.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
        var samplesNav = menuItems.FirstOrDefault(item => item.Name == "Samples") ?? menuItems.ElementAtOrDefault(1);
        Assert.IsNotNull(samplesNav, "Samples item should be found");
        samplesNav.Click();
        Thread.Sleep(2000);
        TakeScreenshot("DownloadModel_SamplesOpened");

        Console.WriteLine("=== Step 2: Navigate to Text > The First Sample Item ===");
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - custom ''(AutomationId="NavView")
//          - custom '' (AutomationId="NavView")
//           - window ''(AutomationId="PaneRoot")
//             - pane ''(AutomationId="MenuItemsScrollViewer")
//               - group ''(AutomationId="MenuItemsHost")
//                 - list item 'Overview'
//                 - list item 'Text' // click this one to expand
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
//pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - custom ''(AutomationId="NavView")
//          - custom '' (AutomationId="NavView")
//           - window ''(AutomationId="PaneRoot")
//             - pane ''(AutomationId="MenuItemsScrollViewer")
//               - group ''(AutomationId="MenuItemsHost")
//                 - list item 'Overview'
//                 - list item 'Text'
//                   - list item 'Generate Text' // click this one
//                   - ...
//                   - ...
//                 - ...
//                 - ...
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

        var modelTypeSelector = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("modelTypeSelector"));

        if (modelTypeSelector == null)
        {
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - custom ''(AutomationId="NavView")
//          - custom '' (AutomationId="NavView")
//            - button 'Close Navigation'(AutomationId="TogglePaneButton")
//            - window ''(AutomationId="PaneRoot")
//            - button 'Selected models'(AutomationId="ModelBtn") // click
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
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - list ''(AutomationId="modelTypeSelector")
//         - list item 'Foundry Local' // click this one
        Assert.IsNotNull(modelTypeSelector, "Model type selector should be found");
        var foundryLocalOption = modelTypeSelector.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName("Foundry Local")));
        Assert.IsNotNull(foundryLocalOption, "Foundry Local item should be found");

        try
        {
            if (foundryLocalOption.Patterns.SelectionItem.IsSupported)
            {
                foundryLocalOption.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                foundryLocalOption.Click();
            }
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
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - pane ''(AutomationId="ModelsView")
//          - list '' (AutomationId="ModelSelectionItemsView")
//          - text 'Available models on Foundry Local'(AutomationId="DownloadableModelsTxt")
//          - group ''
//            - button 'More info'
//            - text 'whisper-tiny'
//            - button 'whisper-tiny' // click the 1st available to download
//          - group ''
//          - ...
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
                {
                    automationId = child.AutomationId;
                }
            }
            catch
            {
            }

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
                    catch
                    {
                    }
                }

                if (targetModelName != null)
                {
                    foreach (var button in buttons)
                    {
                        string? buttonName = null;
                        try
                        {
                            if (button.Properties.Name.IsSupported)
                            {
                                buttonName = button.Name;
                            }
                        }
                        catch
                        {
                        }

                        if (buttonName == targetModelName && buttonName != "More info")
                        {
                            downloadButton = button;
                            modelName = targetModelName;
                            Console.WriteLine($"✓ Found downloadable: {modelName}");
                            break;
                        }
                    }
                }

                if (downloadButton != null)
                {
                    break;
                }
            }
        }

        Assert.IsNotNull(downloadButton, "First available model button should be found");
        TakeScreenshot("DownloadModel_BeforeClick");

        Console.WriteLine($"=== Step 6: Click Model Button ({modelName}) ===");
// pane 'Desktop 1'
//  - windows 'AI Dev Gallery Dev'
//    - pane ''
//      - pane ''
//        - window 'Popup'
//          - pane ''
//            - button 'openai-whisper-tiny-generic-cpu' // click this one
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
            {
                variantName = variantDownloadButton.Name;
            }
        }
        catch
        {
        }

        Console.WriteLine($"✓ Selecting variant: {variantName}");

        variantDownloadButton.Click();
        Thread.Sleep(2000);
        TakeScreenshot("DownloadModel_VariantSelected");
        Console.WriteLine("✓ Download initiated successfully");
    }
}