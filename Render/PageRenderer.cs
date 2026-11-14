using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AnswerSheetStudio.Layout;
using AnswerSheetStudio.Models;
using Line = System.Windows.Shapes.Line;
using Point = System.Windows.Point;
using IoPath = System.IO.Path;

namespace AnswerSheetStudio.Render;

public static class PageRenderer
{
    private static readonly Dictionary<string, Brush> BrushCache = new();

    public static Brush Brush(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        if (BrushCache.TryGetValue(color, out var b)) return b;
        try
        {
            b = (Brush)new BrushConverter().ConvertFromString(color);
            b?.Freeze();
        }
        catch { b = Brushes.Black; }
        BrushCache[color] = b;
        return b;
    }

    public static Canvas BuildCanvas(PagePlan page, SheetProject project, bool forPrint)
    {
        double W = page.WidthMm * Units.D, H = page.HeightMm * Units.D;
        var canvas = new Canvas
        {
            Width = W, Height = H,
            Background = Brushes.White,
            ClipToBounds = true
        };
        // 打印时显式铺白底，保证不依赖打印机的“打印背景”开关
        if (forPrint)
            canvas.Children.Add(new Rectangle { Width = W, Height = H, Fill = Brushes.White });

        // 整面装饰层（定位点 / 唯一标识 / 页眉页脚）先画，栏内容不会覆盖它
        foreach (var cmd in page.Decorations) Add(canvas, cmd, project);
        foreach (var cmd in page.SpanCommands) Add(canvas, cmd, project);
        foreach (var face in page.Faces)
        {
            if (!forPrint && project.Settings.ShowGuides && face.HasContent)
            {
                var guide = new Rectangle
                {
                    Width = face.Bounds.W * Units.D,
                    Height = face.Bounds.H * Units.D,
                    Stroke = Brush("#C9CDD4"),
                    StrokeThickness = 0.6,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                Canvas.SetLeft(guide, face.Bounds.X * Units.D);
                Canvas.SetTop(guide, face.Bounds.Y * Units.D);
                canvas.Children.Add(guide);
            }
            foreach (var cmd in face.Commands) Add(canvas, cmd, project);
        }
        return canvas;
    }

    private static void Add(Canvas canvas, DrawCmd c, SheetProject project)
    {
        double D = Units.D;
        switch (c.Kind)
        {
            case DrawKind.Text:
            {
                double fsDip = Math.Max(3, c.FontSizeMm * D);
                double lineHDip = fsDip * 1.35;
                // 文字框高度若小于一行行高，WPF 会把字形上下裁掉（数字只剩一条横线）。
                // 这里把文本框扩到「至少容纳完整字形」的高度，并按对齐方式反向平移，
                // 使字形仍以原来的矩形为中心；多行文本按实际折行数计算。
                int lines = c.Wrap
                    ? Math.Max(1, TextMeasurer.WrappedLines(c.Content, c.FontSizeMm,
                        c.FontFamily, Math.Max(1, c.W), c.Bold))
                    : 1;
                double boxHDip = c.H * D;
                double needHDip = Math.Max(boxHDip, lineHDip * lines);
                double topDip = c.Y * D;
                if (needHDip > boxHDip + 0.01)
                {
                    double extra = needHDip - boxHDip;
                    topDip -= c.VAlign switch
                    {
                        Models.VAlign.Top => 0,
                        Models.VAlign.Bottom => extra,
                        _ => extra / 2
                    };
                }
                var tb = new TextBlock
                {
                    Text = c.Content,
                    FontFamily = new FontFamily(string.IsNullOrWhiteSpace(c.FontFamily) ? "Microsoft YaHei" : c.FontFamily),
                    FontSize = fsDip,
                    FontWeight = c.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = Brush(c.Color) ?? Brushes.Black,
                    Width = Math.Max(1, c.W * D),
                    Height = Math.Max(1, needHDip),
                    TextAlignment = c.HAlign switch
                    {
                        Models.TextAlign.Center => TextAlignment.Center,
                        Models.TextAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left
                    },
                    TextWrapping = c.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    TextTrimming = c.Wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
                    LineHeight = lineHDip,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    // 扩高后整块文本框已按对齐方式居中，行内不再额外加 padding，避免多行末行被挤出
                    Padding = new Thickness(0, needHDip > boxHDip + 0.01 ? 0 : VPad(c, lineHDip), 0, 0)
                };
                Canvas.SetLeft(tb, c.X * D);
                Canvas.SetTop(tb, topDip);
                canvas.Children.Add(tb);
                break;
            }
            case DrawKind.Rect:
            {
                var rect = new Rectangle
                {
                    Width = Math.Max(0, c.W * D),
                    Height = Math.Max(0, c.H * D),
                    Fill = Brush(c.Fill),
                    Stroke = Brush(c.Stroke),
                    StrokeThickness = c.StrokeMm * D,
                    RadiusX = c.CornerRadiusMm * D,
                    RadiusY = c.CornerRadiusMm * D
                };
                if (c.Dashed && c.StrokeMm > 0)
                    rect.StrokeDashArray = new DoubleCollection { 2.5, 2.0 };
                if (c.StrokeMm <= 0) rect.StrokeThickness = 0;
                Canvas.SetLeft(rect, c.X * D);
                Canvas.SetTop(rect, c.Y * D);
                canvas.Children.Add(rect);
                break;
            }
            case DrawKind.Line:
            {
                var line = new Line
                {
                    X1 = c.X * D, Y1 = c.Y * D, X2 = c.X2 * D, Y2 = c.Y2 * D,
                    Stroke = Brush(c.Color) ?? Brushes.Black,
                    StrokeThickness = Math.Max(0.4, c.StrokeMm * D)
                };
                if (c.Dashed) line.StrokeDashArray = new DoubleCollection { 2.5, 2.0 };
                canvas.Children.Add(line);
                break;
            }
            case DrawKind.Polygon:
            {
                var poly = new Polygon
                {
                    Points = new PointCollection(c.Points.Select(p => p.ToDip())),
                    Fill = Brush(c.Fill),
                    Stroke = Brush(c.Stroke),
                    StrokeThickness = c.StrokeMm * D
                };
                canvas.Children.Add(poly);
                break;
            }
            case DrawKind.Image:
            {
                var path = c.ImagePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) break;
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(IoPath.GetFullPath(path));
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    var img = new System.Windows.Controls.Image
                    {
                        Source = bi, Stretch = Stretch.Uniform,
                        Width = Math.Max(1, c.W * D), Height = Math.Max(1, c.H * D)
                    };
                    Canvas.SetLeft(img, c.X * D);
                    Canvas.SetTop(img, c.Y * D);
                    canvas.Children.Add(img);
                }
                catch { /* 图片损坏时静默跳过 */ }
                break;
            }
        }
    }

    private static double VPad(DrawCmd c, double lineHDip)
    {
        if (c.VAlign == Models.VAlign.Top) return 0;
        double free = Math.Max(0, c.H * Units.D - lineHDip);
        return c.VAlign == Models.VAlign.Bottom ? free : free / 2;
    }

    public static FixedDocument CreateDocument(LayoutResult layout, SheetProject project)
    {
        var doc = new FixedDocument();
        foreach (var page in layout.Pages)
        {
            double w = page.WidthMm * Units.D, h = page.HeightMm * Units.D;
            var fp = new FixedPage { Width = w, Height = h, Background = Brushes.White };
            var canvas = BuildCanvas(page, project, true);
            FixedPage.SetLeft(canvas, 0);
            FixedPage.SetTop(canvas, 0);
            fp.Children.Add(canvas);
            var pc = new PageContent();
            ((IAddChild)pc).AddChild(fp);
            doc.Pages.Add(pc);
        }
        return doc;
    }

    public static void ExportImages(LayoutResult layout, SheetProject project, string directory, int scale = 2)
    {
        Directory.CreateDirectory(directory);
        foreach (var page in layout.Pages)
        {
            var canvas = BuildCanvas(page, project, true);
            double w = page.WidthMm * Units.D, h = page.HeightMm * Units.D;
            canvas.Measure(new Size(w, h));
            canvas.Arrange(new Rect(0, 0, w, h));
            canvas.UpdateLayout();
            int pw = (int)(w * scale), ph = (int)(h * scale);
            var rtb = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(canvas);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var file = IoPath.Combine(directory, $"第{page.Index + 1:00}面_{(page.IsFront ? "正面" : "反面")}.png");
            using var fs = File.Create(file);
            enc.Save(fs);
        }
    }
}
