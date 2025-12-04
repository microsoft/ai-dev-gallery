// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Tests.TestInfra;
using Interop.UIAutomationClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AIDevGallery.Tests.UITests;

/// <summary>
/// Smoke tests using native Windows UIA3 API without FlaUI dependency.
/// These tests verify basic application functionality using COM-based UIAutomation.
/// </summary>
[TestClass]
public class NativeUIA3SmokeTests : NativeUIA3TestBase
{
    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [TestCategory("NativeUIA3")]
    [Description("Verifies that the application can be found and launched using native UIA3")]
    public void NativeUIA3_ApplicationLaunches()
    {
        // The TestInitialize already launched the app and got the main window
        Assert.IsNotNull(AppProcess, "Application process should be launched");
        Assert.IsNotNull(Automation, "UIA Automation should be initialized");
        Assert.IsNotNull(MainWindow, "Main window should be available");

        Console.WriteLine("✓ Application launched successfully");
        Console.WriteLine($"✓ Process ID: {AppProcess.Id}");

        try
        {
            var windowName = MainWindow.CurrentName;
            Console.WriteLine($"✓ Main window title: {windowName}");
        }
        catch (COMException ex)
        {
            Console.WriteLine($"Could not get window name: {ex.Message}");
        }

        TakeScreenshot("NativeUIA3_ApplicationLaunched");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [TestCategory("NativeUIA3")]
    [Description("Verifies that the main window has basic properties using native UIA3")]
    public void NativeUIA3_MainWindowHasBasicProperties()
    {
        Assert.IsNotNull(MainWindow, "Main window should exist");

        // Check basic window properties
        try
        {
            var windowName = MainWindow.CurrentName;
            Assert.IsFalse(string.IsNullOrEmpty(windowName), "Window should have a name");
            Console.WriteLine($"✓ Window name: {windowName}");
        }
        catch (COMException ex)
        {
            Console.WriteLine($"Could not get window name: {ex.Message}");
        }

        try
        {
            var bounds = MainWindow.CurrentBoundingRectangle;
            var width = bounds.right - bounds.left;
            var height = bounds.bottom - bounds.top;

            Assert.IsTrue(width > 0, "Window should have width");
            Assert.IsTrue(height > 0, "Window should have height");

            Console.WriteLine("✓ Main window has valid properties");
            Console.WriteLine($"  Size: {width}x{height}");
            Console.WriteLine($"  Position: ({bounds.left}, {bounds.top})");
        }
        catch (COMException ex)
        {
            Assert.Fail($"Could not get window bounds: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Smoke")]
    [TestCategory("NativeUIA3")]
    [Description("Verifies that UI elements can be queried using native UIA3")]
    public void NativeUIA3_CanQueryUIElements()
    {
        Assert.IsNotNull(MainWindow, "Main window should exist");

        // Try to get all descendants
        var allElements = GetAllDescendants();

        Assert.IsNotNull(allElements, "Should be able to query elements");
        Assert.IsTrue(allElements.Length > 0, "Should find at least some UI elements");

        Console.WriteLine($"✓ Found {allElements.Length} UI elements");

        // Count element types
        var uniqueTypes = new HashSet<int>();
        foreach (var element in allElements)
        {
            try
            {
                var controlType = element.CurrentControlType;
                uniqueTypes.Add(controlType);
            }
            catch (COMException)
            {
                // Skip elements that can't be queried
            }
        }

        Console.WriteLine($"✓ Found {uniqueTypes.Count} different control types");

        // Release COM objects
        foreach (var element in allElements)
        {
            try
            {
                Marshal.ReleaseComObject(element);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [TestCategory("NativeUIA3")]
    [Description("Logs details for elements that expose AutomationId values using native UIA3")]
    public void NativeUIA3_LogAutomationIds()
    {
        Assert.IsNotNull(MainWindow, "Main window should be initialized");
        Assert.IsNotNull(Automation, "Automation should be initialized");

        Console.WriteLine("=== Elements Exposing AutomationId (Native UIA3) ===");
        var allElements = GetAllDescendants();
        var elementsWithId = new List<(IUIAutomationElement Element, string AutomationId)>();
        var elementsWithoutId = new List<IUIAutomationElement>();

        foreach (var element in allElements)
        {
            try
            {
                var automationId = element.CurrentAutomationId;
                if (string.IsNullOrWhiteSpace(automationId))
                {
                    elementsWithoutId.Add(element);
                    continue;
                }

                elementsWithId.Add((element, automationId));
            }
            catch (COMException)
            {
                elementsWithoutId.Add(element);
            }
        }

        Console.WriteLine($"Total elements scanned: {allElements.Length}");
        Console.WriteLine($"Elements with AutomationId: {elementsWithId.Count}");
        Console.WriteLine($"Elements without AutomationId: {elementsWithoutId.Count}");

        static string GetControlTypeName(int controlType)
        {
            return controlType switch
            {
                UIA_ControlTypeIds.UIA_ButtonControlTypeId => "Button",
                UIA_ControlTypeIds.UIA_TextControlTypeId => "Text",
                UIA_ControlTypeIds.UIA_EditControlTypeId => "Edit",
                UIA_ControlTypeIds.UIA_WindowControlTypeId => "Window",
                UIA_ControlTypeIds.UIA_PaneControlTypeId => "Pane",
                UIA_ControlTypeIds.UIA_ListControlTypeId => "List",
                UIA_ControlTypeIds.UIA_ListItemControlTypeId => "ListItem",
                UIA_ControlTypeIds.UIA_ImageControlTypeId => "Image",
                UIA_ControlTypeIds.UIA_GroupControlTypeId => "Group",
                UIA_ControlTypeIds.UIA_CheckBoxControlTypeId => "CheckBox",
                UIA_ControlTypeIds.UIA_ComboBoxControlTypeId => "ComboBox",
                UIA_ControlTypeIds.UIA_ScrollBarControlTypeId => "ScrollBar",
                UIA_ControlTypeIds.UIA_HyperlinkControlTypeId => "Hyperlink",
                UIA_ControlTypeIds.UIA_MenuControlTypeId => "Menu",
                UIA_ControlTypeIds.UIA_MenuItemControlTypeId => "MenuItem",
                UIA_ControlTypeIds.UIA_ToolBarControlTypeId => "ToolBar",
                UIA_ControlTypeIds.UIA_TabControlTypeId => "Tab",
                UIA_ControlTypeIds.UIA_TabItemControlTypeId => "TabItem",
                _ => $"Unknown({controlType})"
            };
        }

        static string DescribeName(IUIAutomationElement element)
        {
            try
            {
                var name = element.CurrentName;
                return string.IsNullOrEmpty(name) ? "(no name)" : name;
            }
            catch (COMException)
            {
                return "(name not accessible)";
            }
        }

        static string DescribeClass(IUIAutomationElement element)
        {
            try
            {
                var className = element.CurrentClassName;
                return string.IsNullOrEmpty(className) ? "(no class)" : className;
            }
            catch (COMException)
            {
                return "(class not accessible)";
            }
        }

        static string DescribeFramework(IUIAutomationElement element)
        {
            try
            {
                var framework = element.CurrentFrameworkId;
                return string.IsNullOrEmpty(framework) ? "(no framework)" : framework;
            }
            catch (COMException)
            {
                return "(framework not accessible)";
            }
        }

        static string DescribeIsEnabled(IUIAutomationElement element)
        {
            try
            {
                return element.CurrentIsEnabled != 0 ? "true" : "false";
            }
            catch (COMException)
            {
                return "(IsEnabled not accessible)";
            }
        }

        static string DescribeBounds(IUIAutomationElement element)
        {
            try
            {
                var bounds = element.CurrentBoundingRectangle;
                var width = bounds.right - bounds.left;
                var height = bounds.bottom - bounds.top;
                return $"[{bounds.left}, {bounds.top}, {width}x{height}]";
            }
            catch (COMException)
            {
                return "(bounds not accessible)";
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- Elements WITH AutomationId ---");
        foreach (var elementInfo in elementsWithId.Take(50))
        {
            try
            {
                var controlType = GetControlTypeName(elementInfo.Element.CurrentControlType);
                var name = DescribeName(elementInfo.Element);
                var className = DescribeClass(elementInfo.Element);
                var framework = DescribeFramework(elementInfo.Element);
                var isEnabled = DescribeIsEnabled(elementInfo.Element);
                var bounds = DescribeBounds(elementInfo.Element);

                Console.WriteLine($"  - ControlType='{controlType}', AutomationId='{elementInfo.AutomationId}', Name='{name}', Class='{className}', Framework='{framework}', IsEnabled={isEnabled}, Bounds={bounds}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - Error describing element: {ex.Message}");
            }
        }

        if (elementsWithId.Count > 50)
        {
            Console.WriteLine($"  ... and {elementsWithId.Count - 50} more with AutomationId");
        }

        Console.WriteLine();
        Console.WriteLine("--- Elements WITHOUT AutomationId ---");
        foreach (var element in elementsWithoutId.Take(50))
        {
            try
            {
                var controlType = GetControlTypeName(element.CurrentControlType);
                var name = DescribeName(element);
                var className = DescribeClass(element);
                var framework = DescribeFramework(element);
                var isEnabled = DescribeIsEnabled(element);
                var bounds = DescribeBounds(element);

                Console.WriteLine($"  - ControlType='{controlType}', Name='{name}', Class='{className}', Framework='{framework}', IsEnabled={isEnabled}, Bounds={bounds}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - Error describing element: {ex.Message}");
            }
        }

        if (elementsWithoutId.Count > 50)
        {
            Console.WriteLine($"  ... and {elementsWithoutId.Count - 50} more without AutomationId");
        }

        // Clean up COM objects
        foreach (var elementInfo in elementsWithId)
        {
            try
            {
                Marshal.ReleaseComObject(elementInfo.Element);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        foreach (var element in elementsWithoutId)
        {
            try
            {
                Marshal.ReleaseComObject(element);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        Assert.IsTrue(true, "This sample is intended only for logging AutomationId information");
    }

    [TestMethod]
    [TestCategory("UI")]
    [TestCategory("Sample")]
    [TestCategory("NativeUIA3")]
    [Description("Tests finding specific elements by AutomationId using native UIA3")]
    public void NativeUIA3_FindElementByAutomationId()
    {
        Assert.IsNotNull(MainWindow, "Main window should exist");
        Assert.IsNotNull(Automation, "Automation should be initialized");

        // Try to find some common elements
        var testAutomationIds = new[] { "NavigationView", "SettingsButton", "SearchBox" };

        Console.WriteLine("=== Testing Element Lookup by AutomationId ===");

        foreach (var automationId in testAutomationIds)
        {
            Console.WriteLine($"Looking for element with AutomationId: {automationId}");

            try
            {
                var condition = Automation.CreatePropertyCondition(
                    UIA_PropertyIds.UIA_AutomationIdPropertyId,
                    automationId);

                var element = MainWindow.FindFirst(TreeScope.TreeScope_Descendants, condition);

                if (element != null)
                {
                    Console.WriteLine($"  ✓ Found element: {automationId}");

                    try
                    {
                        var name = element.CurrentName;
                        var controlType = element.CurrentControlType;
                        Console.WriteLine($"    Name: {name}");
                        Console.WriteLine($"    ControlType: {controlType}");
                    }
                    catch (COMException ex)
                    {
                        Console.WriteLine($"    Could not get element details: {ex.Message}");
                    }

                    Marshal.ReleaseComObject(element);
                }
                else
                {
                    Console.WriteLine($"  ✗ Element not found: {automationId}");
                }
            }
            catch (COMException ex)
            {
                Console.WriteLine($"  ✗ Error searching for {automationId}: {ex.Message}");
            }
        }

        // This test is informational, so always pass
        Assert.IsTrue(true, "Element search test completed");
    }
}

