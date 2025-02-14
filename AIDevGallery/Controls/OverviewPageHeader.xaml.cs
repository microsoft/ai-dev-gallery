using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Controls;

internal sealed partial class OverviewPageHeader : UserControl
{
    public static readonly DependencyProperty ImageContentProperty = DependencyProperty.Register(nameof(ImageContent), typeof(object), typeof(OverviewPageHeader), new PropertyMetadata(defaultValue: null));

    public object ImageContent
    {
        get => (object)GetValue(ImageContentProperty);
        set => SetValue(ImageContentProperty, value);
    }

    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(OverviewPageHeader), new PropertyMetadata(defaultValue: null));

    public object ActionContent
    {
        get => (object)GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(OverviewPageHeader), new PropertyMetadata(defaultValue: null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(OverviewPageHeader), new PropertyMetadata(defaultValue: null));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public OverviewPageHeader()
    {
        this.InitializeComponent();
    }

    private void Control_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate if the text + image collide
        if ((TextPanel.ActualWidth + ImageContentHolder.ActualWidth) >= e.NewSize.Width)
        {
            VisualStateManager.GoToState(this, "NarrowLayout", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "WideLayout", true);
        }
    }
}