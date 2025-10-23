// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AIDevGallery.Pages;

internal sealed partial class AgentSelectionPage : Page
{
    public AgentSelectionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        SetUpAgents();

        this.NavView.Loaded += (sender, args) =>
        {
            HandleNavigation(e.Parameter);
        };
        base.OnNavigatedTo(e);
    }

    public void HandleNavigation(object? obj)
    {
        // Navigate to overview by default
        if (NavView.MenuItems.Count > 0 && NavView.MenuItems[0] is NavigationViewItem item)
        {
            NavView.SelectedItem = item;
        }
    }

    public void ShowHideNavPane()
    {
        NavView.OpenPaneLength = NavView.OpenPaneLength == 0 ? 276 : 0;
    }

    private void SetUpAgents()
    {
        NavView.MenuItems.Clear();

        // Overview item
        NavView.MenuItems.Add(new NavigationViewItem() 
        { 
            Content = "Overview", 
            Icon = new FontIcon() { Glyph = "\uF0E2" }, 
            Tag = "Overview" 
        });

        NavView.MenuItems.Add(new NavigationViewItemSeparator());

        // Agent category
        var agentCategory = new NavigationViewItem() 
        { 
            Content = "Agents", 
            Icon = new FontIcon() { Glyph = "\uF0B9" }, 
            Tag = "AgentCategory",
            SelectsOnInvoked = false
        };
        ToolTip agentToolTip = new() { Content = "Agents" };
        ToolTipService.SetToolTip(agentCategory, agentToolTip);
        
        // Add placeholder agent items
        // TODO: Add actual agent items when available
        
        NavView.MenuItems.Add(agentCategory);
        
        // MCP category
        var mcpCategory = new NavigationViewItem() 
        { 
            Content = "Model Context Protocol", 
            Icon = new FontIcon() { Glyph = "\uE8F1" }, 
            Tag = "MCPCategory",
            SelectsOnInvoked = false
        };
        ToolTip mcpToolTip = new() { Content = "Model Context Protocol" };
        ToolTipService.SetToolTip(mcpCategory, mcpToolTip);
        
        // Add MCP sub-items
        mcpCategory.MenuItems.Add(new NavigationViewItem() 
        { 
            Content = "File Operations MCP", 
            Tag = "FileOperationsMCP" 
        });
        mcpCategory.MenuItems.Add(new NavigationViewItem() 
        { 
            Content = "System Info MCP", 
            Tag = "SystemInfoMCP" 
        });
        mcpCategory.MenuItems.Add(new NavigationViewItem() 
        { 
            Content = "Settings MCP", 
            Tag = "SettingsMCP" 
        });
        
        NavView.MenuItems.Add(mcpCategory);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            switch (tag)
            {
                case "Overview":
                    NavFrame.Navigate(typeof(AgentOverviewPage));
                    break;
                case "FileOperationsMCP":
                    NavFrame.Navigate(typeof(FileOperationsMCPPage));
                    break;
                case "SystemInfoMCP":
                    NavFrame.Navigate(typeof(SystemInfoMCPPage));
                    break;
                case "SettingsMCP":
                    NavFrame.Navigate(typeof(SettingsMCPPage));
                    break;
            }
        }
    }
}
