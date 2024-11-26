// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;

namespace AIDevGallery.Pages
{
    internal sealed partial class ScenarioSelectionPage : Page
    {
        private static LastInternalNavigation? lastInternalNavigation;
        private Scenario? selectedScenario;

        public ScenarioSelectionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SetUpScenarios();

            NavigatedToPageEvent.Log(nameof(ScenarioSelectionPage));

            this.NavView.Loaded += (sender, args) =>
            {
                Scenario? scenario = null;
                object? parameter = e.Parameter;

                if (e.Parameter == null && lastInternalNavigation?.Parameter != null)
                {
                    parameter = lastInternalNavigation.Parameter;
                }

                if (parameter is Scenario sc)
                {
                    scenario = sc;
                }
                else if (parameter is MostRecentlyUsedItem mru)
                {
                    scenario = App.FindScenarioById(mru.ItemId);
                }
                else if (parameter is Sample sample)
                {
                    scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sample.Scenario);
                }
                else if (parameter is SampleNavigationArgs sampleArgs)
                {
                    scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
                    if (scenario != null)
                    {
                        NavigateToScenario(scenario, sampleArgs);
                    }
                }

                if (scenario != null)
                {
                    foreach (NavigationViewItem item in NavView.MenuItems)
                    {
                        SetSelectedScenarioInMenu(item, scenario);
                    }
                }
                else
                {
                    if (NavView.MenuItems[0] is NavigationViewItem item)
                    {
                        if (item.MenuItems != null && item.MenuItems.Count > 0)
                        {
                            item.IsExpanded = true;
                            NavView.SelectedItem = item.MenuItems[0];
                        }
                        else
                        {
                            NavView.SelectedItem = item;
                        }
                    }
                }
            };
            base.OnNavigatedTo(e);
        }

        private void SetUpScenarios()
        {
            foreach (var scenarioCategory in ScenarioCategoryHelpers.AllScenarioCategories)
            {
                var categoryMenu = new NavigationViewItem() { Content = scenarioCategory.Name, Icon = new FontIcon() { Glyph = scenarioCategory.Icon }, Tag = scenarioCategory };
                ToolTip categoryToolTip = new() { Content = scenarioCategory.Name };
                ToolTipService.SetToolTip(categoryMenu, categoryToolTip);

                foreach (var scenario in scenarioCategory.Scenarios)
                {
                    NavigationViewItem currNavItem = new() { Content = scenario.Name, Tag = scenario };
                    ToolTip secnarioToolTip = new() { Content = scenario.Name };
                    ToolTipService.SetToolTip(currNavItem, secnarioToolTip);
                    categoryMenu.MenuItems.Add(currNavItem);
                }

                categoryMenu.SelectsOnInvoked = false;
                NavView.MenuItems.Add(categoryMenu);
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is Scenario scenario && scenario != selectedScenario)
            {
                NavigateToScenario(scenario);
            }
        }

        private void NavigateToScenario(Scenario scenario, SampleNavigationArgs? sampleArgs = null)
        {
            selectedScenario = scenario;
            lastInternalNavigation = new LastInternalNavigation(typeof(ScenarioPage), scenario);
            if (sampleArgs != null)
            {
                NavFrame.Navigate(typeof(ScenarioPage), sampleArgs);
            }
            else
            {
                NavFrame.Navigate(typeof(ScenarioPage), scenario);
            }
        }

        private void SetSelectedScenarioInMenu(NavigationViewItem item, Scenario scenario)
        {
            foreach (var menuItem in item.MenuItems)
            {
                if (menuItem is NavigationViewItem navItem)
                {
                    if (navItem.Tag is Scenario modelSample && modelSample.Id.Equals(scenario.Id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        item.IsExpanded = true;
                        NavView.SelectedItem = navItem;
                        return;
                    }
                    else if (navItem.MenuItems.Count > 0)
                    {
                        SetSelectedScenarioInMenu(navItem, scenario);
                    }
                }
            }
        }
    }
}