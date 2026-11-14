using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

public static class Units
{
    public const double D = 96.0 / 25.4;          // 1 mm -> DIP
    public static double P(double mm) => mm * D;
    public static double Mm(double dip) => dip / D;
}

public readonly struct RectMm
{
    public readonly double X, Y, W, H;
    public RectMm(double x, double y, double w, double h) { X = x; Y = y; W = w; H = h; }
    public double Right => X + W;
    public double Bottom => Y + H;
    public RectMm Inset(double l, double t, double r, double b) =>
        new(X + l, Y + t, Math.Max(0, W - l - r), Math.Max(0, H - t - b));
    public bool IsEmpty => W <= 0.01 || H <= 0.01;
}

public readonly struct PointMm
{
    public readonly double X, Y;
    public PointMm(double x, double y) { X = x; Y = y; }
    public System.Windows.Point ToDip() => new(X * Units.D, Y * Units.D);
}

public sealed class DrawCmd
{
    public DrawKind Kind;
    public double X, Y, W, H, X2, Y2;
    public string Content = "";
    public string FontFamily = "Microsoft YaHei";
    public string Color = "#111111";
    public string Fill = "";
    public string Stroke = "#333333";
    public double FontSizeMm = 3.6;
    public double StrokeMm = 0.3;
    public double CornerRadiusMm;
    public bool Bold, Wrap, Dashed;
    public TextAlign HAlign = TextAlign.Left;
    public VAlign VAlign = VAlign.Middle;
    public List<PointMm> Points = new();
    public string ImagePath = "";

    public static DrawCmd Text(double x, double y, double w, double h, string text,
        double sizeMm, string family, string color, TextAlign ha = TextAlign.Left,
        VAlign va = VAlign.Middle, bool bold = false, bool wrap = false) => new()
    {
        Kind = DrawKind.Text, X = x, Y = y, W = w, H = h, Content = text ?? "",
        FontSizeMm = sizeMm, FontFamily = family, Color = color,
        HAlign = ha, VAlign = va, Bold = bold, Wrap = wrap
    };

    public static DrawCmd Rect(double x, double y, double w, double h,
        string stroke, string fill = "", double strokeMm = 0.3, double radius = 0, bool dashed = false) => new()
    {
        Kind = DrawKind.Rect, X = x, Y = y, W = w, H = h,
        Stroke = stroke, Fill = fill, StrokeMm = strokeMm,
        CornerRadiusMm = radius, Dashed = dashed
    };

    public static DrawCmd Line(double x1, double y1, double x2, double y2,
        string color, double strokeMm = 0.25, bool dashed = false) => new()
    {
        Kind = DrawKind.Line, X = x1, Y = y1, X2 = x2, Y2 = y2,
        Color = color, StrokeMm = strokeMm, Dashed = dashed
    };

    public static DrawCmd Polygon(List<PointMm> points, string fill, string stroke = "", double strokeMm = 0) => new()
    {
        Kind = DrawKind.Polygon, Points = points, Fill = fill, Stroke = stroke, StrokeMm = strokeMm
    };

    public static DrawCmd Image(double x, double y, double w, double h, string path) => new()
    {
        Kind = DrawKind.Image, X = x, Y = y, W = w, H = h, ImagePath = path
    };
}

/// <summary>用 WPF 排版引擎实测文字真实尺寸（毫米）。</summary>
public static class TextMeasurer
{
    private static readonly Dictionary<string, double> WidthCache = new();

    private static FormattedText Ft(string text, double sizeMm, string family, bool bold)
    {
        var tf = new Typeface(new FontFamily(family), FontStyles.Normal,
            bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(text ?? "", CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight, tf, Math.Max(0.5, sizeMm * Units.D),
            Brushes.Black, 1.0);
    }

    public static double Width(string text, double sizeMm, string family, bool bold = false)
    {
        var key = $"{family}|{sizeMm:0.###}|{bold}|{text}";
        if (WidthCache.TryGetValue(key, out var v)) return v;
        var w = Units.Mm(Ft(text, sizeMm, family, bold).Width);
        WidthCache[key] = w;
        return w;
    }

    public static double Height(string text, double sizeMm, string family, bool bold = false) =>
        Units.Mm(Ft("Ajg中", sizeMm, family, bold).Height);

    /// <summary>给定可用宽度时，按实测文本宽度估算换行后的行数（中文按字符折行）。</summary>
    public static int WrappedLines(string text, double sizeMm, string family,
        double maxWidthMm, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        double maxW = Math.Max(1, maxWidthMm);
        int lines = 0;
        foreach (var para in text.Replace("\r", "").Split('\n'))
        {
            if (para.Length == 0) { lines++; continue; }
            double total = Width(para, sizeMm, family, bold);
            lines += Math.Max(1, (int)Math.Ceiling(total / maxW));
        }
        return Math.Max(1, lines);
    }

    public static void ClearCache() => WidthCache.Clear();
}
