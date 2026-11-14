using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using AnswerSheetStudio.Render;

namespace AnswerSheetStudio.UI;

public sealed class ColorBox : Grid
{
    private static readonly string[] Palette =
    {
        "#000000", "#333333", "#666666", "#999999", "#FFFFFF",
        "#1E4E9C", "#2F7DE1", "#39A0ED", "#00A0A0", "#1E9E62",
        "#C0392B", "#E74C3C", "#E67E22", "#F1C40F", "#8E44AD", "#6C3FB5"
    };

    private readonly TextBox _tb;
    private readonly Rectangle _swatch;
    private readonly Button _picker;

    public event Action<string> ValueChanged;

    public string Value
    {
        get => _tb.Text;
        set
        {
            _tb.Text = value ?? "";
            UpdateSwatch();
        }
    }

    public ColorBox(string value)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        _tb = new TextBox { Text = value ?? "", VerticalContentAlignment = VerticalAlignment.Center };
        _tb.LostFocus += (_, _) => Apply(_tb.Text);
        _tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Apply(_tb.Text); };
        SetColumn(_tb, 0);
        Children.Add(_tb);

        _swatch = new Rectangle
        {
            Margin = new Thickness(3, 2, 2, 2),
            Stroke = Brushes.Gray, StrokeThickness = 0.6
        };
        SetColumn(_swatch, 1);
        Children.Add(_swatch);

        _picker = new Button { Content = "…", Margin = new Thickness(2, 2, 0, 2), Padding = new Thickness(0) };
        _picker.Click += (_, _) => ShowPalette();
        SetColumn(_picker, 2);
        Children.Add(_picker);

        UpdateSwatch();
    }

    private void UpdateSwatch()
    {
        try { _swatch.Fill = PageRenderer.Brush(_tb.Text) ?? Brushes.White; }
        catch { _swatch.Fill = Brushes.White; }
    }

    private void Apply(string v)
    {
        UpdateSwatch();
        ValueChanged?.Invoke(_tb.Text);
    }

    private void ShowPalette()
    {
        var popup = new Popup
        {
            PlacementTarget = _picker,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true
        };
        var panel = new WrapPanel { Width = 182, Margin = new Thickness(4) };
        foreach (var color in Palette)
        {
            var b = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(2),
                Background = PageRenderer.Brush(color),
                ToolTip = color, Tag = color
            };
            b.Click += (_, _) =>
            {
                _tb.Text = (string)b.Tag;
                Apply(_tb.Text);
                popup.IsOpen = false;
            };
            panel.Children.Add(b);
        }
        popup.Child = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = panel
        };
        popup.IsOpen = true;
    }
}
