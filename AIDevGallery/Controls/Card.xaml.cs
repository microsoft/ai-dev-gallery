// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Controls;

internal sealed partial class Card : UserControl
{
    public static readonly DependencyProperty TitleContentProperty = DependencyProperty.Register(nameof(TitleContent), typeof(object), typeof(Card), new PropertyMetadata(defaultValue: null, OnVisualPropertyChanged));

    public object TitleContent
    {
        get => (object)GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(defaultValue: null, OnVisualPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static new readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Card), new PropertyMetadata(defaultValue: null));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1061:Do not hide base class methods", Justification = "We need to hide the base class method")]
    public new object Content
    {
        get => (object)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly DependencyProperty TitlePaddingProperty = DependencyProperty.Register(nameof(TitlePadding), typeof(Thickness), typeof(Card), new PropertyMetadata(defaultValue: new Thickness(12, 12, 16, 12)));

    public Thickness TitlePadding
    {
        get => (Thickness)GetValue(TitlePaddingProperty);
        set => SetValue(TitlePaddingProperty, value);
    }

    public static readonly DependencyProperty DividerVisibilityProperty = DependencyProperty.Register(nameof(DividerVisibility), typeof(Visibility), typeof(Card), new PropertyMetadata(defaultValue: null));

    public Visibility DividerVisibility
    {
        get => (Visibility)GetValue(DividerVisibilityProperty);
        set => SetValue(DividerVisibilityProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(Card), new PropertyMetadata(defaultValue: null, OnVisualPropertyChanged));

    public IconElement Icon
    {
        get => (IconElement)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IsMinimizedProperty = DependencyProperty.Register(nameof(IsMinimized), typeof(bool), typeof(Card), new PropertyMetadata(defaultValue: false, OnVisualPropertyChanged));

    public bool IsMinimized
    {
        get => (bool)GetValue(IsMinimizedProperty);
        set => SetValue(IsMinimizedProperty, value);
    }

    public Card()
    {
        this.InitializeComponent();
        SetVisualStates();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Card card)
        {
            card.SetVisualStates();
        }
    }

    private void SetVisualStates()
    {
        VisualStateManager.GoToState(this, Icon != null ? "IconVisible" : "IconCollapsed", true);

        if (string.IsNullOrEmpty(Title) && Icon == null && TitleContent == null)
        {
            VisualStateManager.GoToState(this, "TitleGridCollapsed", true);
            DividerVisibility = Visibility.Collapsed;
        }
        else
        {
            VisualStateManager.GoToState(this, "TitleGridVisible", true);
            DividerVisibility = Visibility.Visible;
        }

        if (IsMinimized)
        {
            ExpandBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ExpandBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b)
        {
            this.MaxHeight = double.PositiveInfinity;
            b.Visibility = Visibility.Collapsed;
        }
    }
}