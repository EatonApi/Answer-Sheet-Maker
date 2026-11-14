using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using AnswerSheetStudio.Layout;
using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Render;

/// <summary>
/// 原生 PDF 导出：不依赖打印机，也不需要任何第三方库 / NuGet 包。
/// 每个印刷面按真实毫米尺寸生成一页（MediaBox 用 pt，A4=595.28×841.89、A3=1190.55×841.89），
/// 用与预览 / PNG 完全相同的 WPF 画面离屏渲染，再以 zlib 压缩的无损 RGB 位图嵌入 PDF。
/// 这样中文、字体、定位点、虚线、图片都与编辑器所见一致，打印时 100% 缩放即为原始毫米尺寸。
/// </summary>
public static class PdfExporter
{
    /// <summary>默认导出精度（DPI）；300 DPI 已满足一般阅卷扫描与印刷。</summary>
    public const int DefaultDpi = 300;

    private const double MmToPoint = 72.0 / 25.4;

    private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>把整份版面导出为单个多页 PDF。</summary>
    public static void Export(LayoutResult layout, SheetProject project, string filePath, int dpi = DefaultDpi)
    {
        if (layout == null || layout.Pages.Count == 0)
            throw new InvalidOperationException("当前没有可导出的页面。");
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("未指定 PDF 路径。", nameof(filePath));

        dpi = Math.Clamp(dpi, 72, 1200);
        string full = Path.GetFullPath(filePath);
        string dir = Path.GetDirectoryName(full) ?? "";
        if (dir.Length > 0) Directory.CreateDirectory(dir);

        int count = layout.Pages.Count;
        // 对象编号：1=目录，2=页树，之后每页 3 个对象（Page / Contents / 图像），最后是 Info
        int infoId = 3 + count * 3;
        var offsets = new long[infoId + 1];

        using var fs = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(fs, "%PDF-1.4\n");
        fs.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });   // 二进制标记，避免被当纯文本处理
        Write(fs, "% Answer Sheet Studio PDF export\n");

        Obj(fs, 1, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(i => $"{3 + i * 3} 0 R"));
        Obj(fs, 2, offsets, $"<< /Type /Pages /Kids [{kids}] /Count {count} >>");

        for (int i = 0; i < count; i++)
        {
            var page = layout.Pages[i];
            double wPt = page.WidthMm * MmToPoint, hPt = page.HeightMm * MmToPoint;
            int pageId = 3 + i * 3, contentId = pageId + 1, imageId = pageId + 2;

            Obj(fs, pageId, offsets,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(wPt)} {Num(hPt)}] " +
                $"/Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentId} 0 R >>");

            // cm：把单位正方形映射到整页；图像按 PDF 惯例自下而上绘制，正好铺满 MediaBox
            string content = $"q\n{Num(wPt)} 0 0 {Num(hPt)} 0 0 cm\n/Im0 Do\nQ\n";
            ObjStream(fs, contentId, offsets, "<< /Length {LEN} >>", Encoding.ASCII.GetBytes(content));

            int pw = Math.Max(1, (int)Math.Round(page.WidthMm * dpi / 25.4));
            int ph = Math.Max(1, (int)Math.Round(page.HeightMm * dpi / 25.4));
            byte[] zdata = RenderPageToZlib(page, project, dpi, pw, ph);
            ObjStream(fs, imageId, offsets,
                $"<< /Type /XObject /Subtype /Image /Width {pw} /Height {ph} " +
                "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {LEN} >>",
                zdata);
        }

        string title = PdfHexText(project?.Settings?.Title ?? "答题卡");
        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
        Obj(fs, infoId, offsets,
            $"<< /Title {title} /Producer (Answer Sheet Studio) /Creator (Answer Sheet Studio) " +
            $"/CreationDate (D:{stamp}) >>");

        long xrefPos = fs.Position;
        Write(fs, $"xref\n0 {infoId + 1}\n");
        Write(fs, "0000000000 65535 f \n");
        for (int i = 1; i <= infoId; i++)
            Write(fs, offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write(fs, $"trailer\n<< /Size {infoId + 1} /Root 1 0 R /Info {infoId} 0 R >>\n");
        Write(fs, $"startxref\n{xrefPos}\n%%EOF\n");
    }

    /// <summary>与预览 / PNG 完全同一套画面，输出 zlib 压缩的 RGB 原始像素。</summary>
    private static byte[] RenderPageToZlib(PagePlan page, SheetProject project, int dpi, int pw, int ph)
    {
        double wDip = page.WidthMm * Units.D, hDip = page.HeightMm * Units.D;
        var canvas = PageRenderer.BuildCanvas(page, project, true);
        canvas.Measure(new Size(wDip, hDip));
        canvas.Arrange(new Rect(0, 0, wDip, hDip));
        canvas.UpdateLayout();

        var rtb = new RenderTargetBitmap(pw, ph, dpi, dpi, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(canvas);

        int stride = pw * 4;
        var bgra = new byte[stride * ph];
        rtb.CopyPixels(new System.Windows.Int32Rect(0, 0, pw, ph), bgra, stride, 0);
        var row = new byte[pw * 3];

        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
        {
            for (int y = 0; y < ph; y++)
            {
                int src = y * stride;
                for (int x = 0; x < pw; x++, src += 4)
                {
                    int a = bgra[src + 3];
                    // Pbgra32 为预乘 alpha；画布已铺白底，这里把半透明像素合成到白色上
                    int white = 255 - a;
                    int j = x * 3;
                    row[j] = (byte)Math.Min(255, bgra[src + 2] + white);
                    row[j + 1] = (byte)Math.Min(255, bgra[src + 1] + white);
                    row[j + 2] = (byte)Math.Min(255, bgra[src] + white);
                }
                z.Write(row, 0, row.Length);
            }
        }
        return ms.ToArray();
    }

    private static void Write(FileStream fs, string asciiOrUtf8)
    {
        byte[] b = Encoding.UTF8.GetBytes(asciiOrUtf8);
        fs.Write(b, 0, b.Length);
    }

    private static void Obj(FileStream fs, int id, long[] offsets, string dict)
    {
        offsets[id] = fs.Position;
        Write(fs, $"{id} 0 obj\n{dict}\nendobj\n");
    }

    private static void ObjStream(FileStream fs, int id, long[] offsets, string dictTemplate, byte[] data)
    {
        offsets[id] = fs.Position;
        Write(fs, $"{id} 0 obj\n" +
                  dictTemplate.Replace("{LEN}", data.Length.ToString(CultureInfo.InvariantCulture)) +
                  "\nstream\n");
        fs.Write(data, 0, data.Length);
        Write(fs, "\nendstream\nendobj\n");
    }

    /// <summary>PDF 文本串：UTF-16BE + BOM 的十六进制字符串，中文标题不会乱码。</summary>
    private static string PdfHexText(string s)
    {
        var enc = Encoding.BigEndianUnicode;
        byte[] bom = enc.GetPreamble();
        byte[] body = enc.GetBytes(s ?? "");
        var all = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
        Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
        return "<" + Convert.ToHexString(all) + ">";
    }
}
