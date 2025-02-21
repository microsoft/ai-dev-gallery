// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;

namespace AIDevGallery.Controls;

[TemplatePart(Name = nameof(PART_FooterPresenter), Type = typeof(ContentPresenter))]
[TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]

internal partial class TitleBar : Control
{
#pragma warning disable SA1306 // Field names should begin with lower-case letter
    private WndProcHelper? WndProcHelper;
    private MenuFlyout? MenuFlyout;
    private ContentPresenter? PART_ContentPresenter;
    private ContentPresenter? PART_FooterPresenter;

    private void SetWASDKTitleBar()
    {
        if (this.Window == null)
        {
            return;

            // TO DO: Throw exception that window has not been set?
        }

        if (AutoConfigureCustomTitleBar)
        {
            Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (this.ContextFlyout is not null and MenuFlyout menuFlyout)
            {
                this.MenuFlyout = menuFlyout;
                WndProcHelper = new WndProcHelper(this.Window);
                WndProcHelper.RegisterWndProc(WindowWndProc);
                WndProcHelper.RegisterInputNonClientPointerSourceWndProc(InputNonClientPointerSourceWndProc);
            }

            this.Window.SizeChanged -= Window_SizeChanged;
            this.Window.SizeChanged += Window_SizeChanged;
            this.Window.Activated -= Window_Activated;
            this.Window.Activated += Window_Activated;

            if (Window.Content is FrameworkElement rootElement)
            {
                UpdateCaptionButtons(rootElement);
                rootElement.ActualThemeChanged += (s, e) =>
                {
                    UpdateCaptionButtons(rootElement);
                };
            }

            PART_ContentPresenter = GetTemplateChild(nameof(PART_ContentPresenter)) as ContentPresenter;
            PART_FooterPresenter = GetTemplateChild(nameof(PART_FooterPresenter)) as ContentPresenter;

            // Get caption button occlusion information.
            int captionButtonOcclusionWidthRight = Window.AppWindow.TitleBar.RightInset;
            int captionButtonOcclusionWidthLeft = Window.AppWindow.TitleBar.LeftInset;
            PART_LeftPaddingColumn!.Width = new GridLength(captionButtonOcclusionWidthLeft);
            PART_RightPaddingColumn!.Width = new GridLength(captionButtonOcclusionWidthRight);

            if (DisplayMode == DisplayMode.Tall)
            {
                // Choose a tall title bar to provide more room for interactive elements
                // like search box or person picture controls.
                Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }
            else
            {
                Window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }

            // Recalculate the drag region for the custom title bar
            // if you explicitly defined new draggable areas.
            SetDragRegionForCustomTitleBar();

            _isAutoConfigCompleted = true;
        }
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        UpdateVisualStateAndDragRegion(args.Size);
    }

    private void UpdateCaptionButtons(FrameworkElement rootElement)
    {
        Window.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        Window.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        if (rootElement.ActualTheme == ElementTheme.Dark)
        {
            Window.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            Window.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
        }
        else
        {
            Window.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
            Window.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
        }
    }

    private void ResetWASDKTitleBar()
    {
        if (this.Window == null)
        {
            return;

            // TO DO: Throw exception that window has not been set?
        }

        // Only reset if we were the ones who configured
        if (_isAutoConfigCompleted)
        {
            Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
            this.Window.SizeChanged -= Window_SizeChanged;
            this.Window.Activated -= Window_Activated;
            SizeChanged -= this.TitleBar_SizeChanged;
            Window.AppWindow.TitleBar.ResetToDefault();
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            VisualStateManager.GoToState(this, WindowDeactivatedState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, WindowActivatedState, true);
        }
    }

    private void SetDragRegionForCustomTitleBar()
    {
        if (AutoConfigureCustomTitleBar && Window is not null)
        {
            ClearDragRegions(NonClientRegionKind.Passthrough);
            var items = new FrameworkElement?[] { PART_ContentPresenter, PART_FooterPresenter, PART_ButtonHolder };
            var validItems = items.Where(x => x is not null).Select(x => x!).ToArray(); // Prune null items

            SetDragRegion(NonClientRegionKind.Passthrough, validItems);
        }
    }

    private double GetRasterizationScaleForElement(UIElement element)
    {
        if (element.XamlRoot != null)
        {
            return element.XamlRoot.RasterizationScale;
        }

        return 0.0;
    }

    private void SetDragRegion(NonClientRegionKind nonClientRegionKind, params FrameworkElement[] frameworkElements)
    {
        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(Window.AppWindow.Id);
        List<RectInt32> rects = [];
        var scale = GetRasterizationScaleForElement(this);

        foreach (var frameworkElement in frameworkElements)
        {
            if (frameworkElement == null)
            {
                continue;
            }

            GeneralTransform transformElement = frameworkElement.TransformToVisual(null);
            Windows.Foundation.Rect bounds = transformElement.TransformBounds(new Windows.Foundation.Rect(0, 0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
            var transparentRect = new RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale));
            rects.Add(transparentRect);
        }

        if (rects.Count > 0)
        {
            nonClientInputSrc.SetRegionRects(nonClientRegionKind, [.. rects]);
        }
    }

    /// <summary>
    /// Clears the drag regions for the specified non-client region kind.
    /// </summary>
    /// <param name="nonClientRegionKind">The kind of non-client region to clear.</param>
    public void ClearDragRegions(NonClientRegionKind nonClientRegionKind)
    {
        var noninputsrc = InputNonClientPointerSource.GetForWindowId(Window.AppWindow.Id);
        noninputsrc.ClearRegionRects(nonClientRegionKind);
    }

    private LRESULT InputNonClientPointerSourceWndProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        switch (Msg)
        {
            case WM_NCLBUTTONDOWN:
                {
                    if (MenuFlyout?.IsOpen ?? false)
                    {
                        MenuFlyout.Hide();
                    }

                    break;
                }

            case WM_NCRBUTTONDOWN:
                {
                    PointInt32 pt = new((int)lParam & 0xFFFF, (int)lParam >> 16);
                    FlyoutShowOptions options = new()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Position = InfoHelper.SystemVersion.Build >= 22000 ?
                            new Windows.Foundation.Point((pt.X - this.Window.AppWindow.Position.X - 8) / XamlRoot.RasterizationScale, (pt.Y - this.Window.AppWindow.Position.Y) / XamlRoot.RasterizationScale) :
                            new Windows.Foundation.Point(pt.X - this.Window.AppWindow.Position.X - 8, pt.Y - this.Window.AppWindow.Position.Y)
                    };

                    MenuFlyout?.ShowAt(this, options);
                    return (LRESULT)0;
                }
        }

        if (WndProcHelper is null)
        {
            throw new InvalidOperationException($"Internal error: {nameof(WndProcHelper)} is missing.");
        }

        return WndProcHelper.CallInputNonClientPointerSourceWindowProc(hWnd, Msg, wParam, lParam);
    }

    private LRESULT WindowWndProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        const uint WM_SYSMENU = 0x0313;
        switch (Msg)
        {
            case WM_SYSMENU:
                {
                    return (LRESULT)0;
                }

            case WM_SYSCOMMAND:
                {
                    var sysCommand = (uint)wParam & 0xFFF0;

                    if (sysCommand == SC_MOUSEMENU)
                    {
                        FlyoutShowOptions options = new()
                        {
                            Position = new Windows.Foundation.Point(0, 15),
                            ShowMode = FlyoutShowMode.Standard
                        };
                        MenuFlyout?.ShowAt(null, options);
                        return (LRESULT)0;
                    }
                    else if (sysCommand == SC_KEYMENU)
                    {
                        FlyoutShowOptions options = new()
                        {
                            Position = new Windows.Foundation.Point(0, 45),
                            ShowMode = FlyoutShowMode.Standard
                        };
                        MenuFlyout?.ShowAt(null, options);
                        return (LRESULT)0;
                    }

                    break;
                }
        }

        if (WndProcHelper is null)
        {
            throw new InvalidOperationException($"Internal error: {nameof(WndProcHelper)} is missing.");
        }

        return WndProcHelper.CallWindowProc(hWnd, Msg, wParam, lParam);
    }
#pragma warning restore SA1306 // Field names should begin with lower-case letter
}