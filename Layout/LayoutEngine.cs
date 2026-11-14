using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

/// <summary>一个「栏」：只负责内容排版，不再持有定位点 / 标识 / 页眉页脚。</summary>
public sealed class FacePlan
{
    public int Index;
    public int Column;
    public RectMm Bounds;
    public RectMm Content;
    public bool HasContent;
    public List<DrawCmd> Commands { get; } = new();
}

/// <summary>一个印刷「面」= 一张纸的正面或反面（A3 是整幅 420×297，含全部栏）。</summary>
public sealed class PagePlan
{
    public int Index;
    public int SheetIndex;
    public bool IsFront;
    public double WidthMm, HeightMm;
    public bool HasContent;
    public List<FacePlan> Faces { get; } = new();
    public List<DrawCmd> SpanCommands { get; } = new();
    /// <summary>整面唯一的装饰：四角定位点 + 每面唯一标识 + 整面页眉页脚。</summary>
    public List<DrawCmd> Decorations { get; } = new();
}

public sealed class LayoutResult
{
    public List<PagePlan> Pages { get; } = new();
    /// <summary>页面上可直接拖拽调整大小的区域（模块区域 + 考号填涂区），毫米坐标。</summary>
    public List<ModuleRegion> Regions { get; } = new();
    /// <summary>印刷面总数（一张纸 = 2 面）。</summary>
    public int TotalFaces, ContentFaces, SheetCount, Columns, TotalPages;
    public double PageWidthMm, PageHeightMm, FaceWidthMm;
    public double ColumnWidthMm => FaceWidthMm;
    public List<string> Warnings { get; } = new();
    public string Summary => $"{SheetCount} 张纸 · {TotalFaces} 个印刷面（{ContentFaces} 面有内容）· 每面 {Columns} 栏";
}

public static class LayoutEngine
{
    private const int MaxFaces = 400;

    public static LayoutResult Build(SheetProject project)
    {
        var f = project.Settings.FaceId;
        int n = Math.Clamp(f.BlockCount, 1, 5);
        int slots = f.Style == FaceIdStyle.CombinedBoxes ? n : n * 2;
        LayoutResult result = null;
        for (int pass = 0; pass < 3; pass++)
        {
            result = BuildOnce(project, slots);
            int next = SuggestSlots(project, result, slots);
            if (next == slots) break;
            slots = next;
        }
        var distinct = result.Warnings.Distinct().ToList();
        result.Warnings.Clear();
        result.Warnings.AddRange(distinct);
        return result;
    }

    private static int SuggestSlots(SheetProject p, LayoutResult r, int current)
    {
        var f = p.Settings.FaceId;
        int n = Math.Clamp(f.BlockCount, 1, 5);
        if (f.Style == FaceIdStyle.CombinedBoxes) return n;
        // 标识画在整张纸的一面，可用长度按整幅纸算，不再按栏宽算
        double length = f.Orientation == IdOrientation.Horizontal
            ? r.PageWidthMm - 2 * f.OffsetXMm
            : r.PageHeightMm - 2 * f.OffsetYMm;
        // 与定位点同一行时，两端定位点占用的宽度不能再用来放标识方块
        if (f.SameRowAsMarkers)
            length -= 2 * (p.Settings.MarkerInsetMm + p.Settings.MarkerSizeMm + 2.0);
        length = Math.Max(10, length);
        double unit = f.Orientation == IdOrientation.Horizontal ? f.BlockWidthMm : f.BlockHeightMm;
        double step = Math.Max(0.5, unit + f.GapMm);
        int bySpace = Math.Max(n, (int)Math.Floor((length + f.GapMm) / step));
        int s = n;
        while (s < Math.Min(bySpace, 40) && Comb(s, n) < r.TotalFaces) s++;
        return Math.Clamp(s, n, Math.Max(n, bySpace));
    }

    private sealed class Insets
    {
        public double L, T, R, B;
        public RectMm Strip;
        public double HeaderY, FooterY, ContentTop, ContentBottom;
    }

    private static LayoutResult BuildOnce(SheetProject project, int slots)
    {
        var s = project.Settings;
        var ctx = new LayoutContext { Project = project };

        double pageW, pageH;
        int cols;
        if (s.Paper == PaperSize.A4) { pageW = 210; pageH = 297; cols = 1; }
        else { pageW = 420; pageH = 297; cols = Math.Clamp(s.Columns, 2, 3); }
        double colW = pageW / cols;
        int colsPerSide = cols;
        int colsPerSheet = cols * 2;

        var result = new LayoutResult
        {
            Columns = cols, PageWidthMm = pageW, PageHeightMm = pageH, FaceWidthMm = colW
        };

        var flows = new Queue<ModuleFlow>(project.Modules.Select(ModuleFlowFactory.Create));
        var faces = new List<FacePlan>();
        var spanCommands = new Dictionary<int, List<DrawCmd>>();
        var spanHeights = new Dictionary<int, double>();

        double safe = s.MarkerInsetMm + s.MarkerSizeMm + 1.0;
        var whole = new RectMm(0, 0, pageW, pageH);
        var ins = ComputeInsets(s, whole, slots, safe);
        double gutter = Math.Max(2.0, Math.Min(s.BaseMarginMm, 8.0));
        double hfH = s.HeaderFooterSizeMm * 1.7;

        int f = 0;
        while ((flows.Count > 0 || f < colsPerSheet || f % colsPerSheet != 0) && f < MaxFaces)
        {
            int col = f % colsPerSide;
            int pageIndex = f / colsPerSide;
            bool isFront = pageIndex % 2 == 0;
            var bounds = new RectMm(col * colW, 0, colW, pageH);
            var face = new FacePlan { Index = f, Column = col, Bounds = bounds };
            ctx.PageIndex = pageIndex;
            ctx.Column = col;

            // 可选：A3 正面整幅通栏页首（默认关闭；默认是只在正面第一栏）
            bool span = s.Paper == PaperSize.A3 && s.HeaderScope == HeaderScope.SpanFrontSide &&
                        isFront && s.AutoHeader;
            double contentTop = ins.ContentTop;
            if (span)
            {
                if (!spanHeights.TryGetValue(pageIndex, out double spanH))
                {
                    var list = new List<DrawCmd>();
                    spanCommands[pageIndex] = list;
                    var rect = new RectMm(ins.L, contentTop, pageW - ins.L - ins.R, pageH);
                    spanH = BuildAutoHeader(project, rect, ctx, list, out _);
                    spanHeights[pageIndex] = spanH;
                }
                contentTop += spanHeights[pageIndex] + 2.0;
            }

            double l = col == 0 ? ins.L : gutter;
            double r = col == cols - 1 ? ins.R : gutter;
            var content = bounds.Inset(l, contentTop, r, pageH - ins.ContentBottom);
            face.Content = content;

            bool needHeader = s.AutoHeader && isFront &&
                (s.HeaderScope == HeaderScope.AllFrontColumns || col == 0) &&
                s.HeaderScope != HeaderScope.SpanFrontSide;

            double y = content.Y;
            double bottom = content.Bottom;

            bool hasAuto = false;
            if (needHeader)
            {
                var rect = new RectMm(content.X, y, content.W, Math.Max(0, bottom - y));
                double used = BuildAutoHeader(project, rect, ctx, face.Commands, out _);
                y += used;
                hasAuto = true;
            }

            int placed = 0;
            while (flows.Count > 0)
            {
                var flow = flows.Peek();
                var area = new RectMm(content.X, y, content.W, Math.Max(0, bottom - y));
                int before = face.Commands.Count;
                flow.FillWrapped(area, bottom, false, ctx, face.Commands, out double ny);
                bool progressed = ny > y + 0.001 || face.Commands.Count > before;
                if (flow.Done)
                {
                    flows.Dequeue();
                    y = ny;
                    placed += Math.Max(1, face.Commands.Count - before);
                    continue;
                }
                if (!progressed)
                {
                    if (placed == 0)
                    {
                        // 强制放置会重排同一区域，先移除上一次收集到的区域矩形
                        ctx.Regions.RemoveAll(r => r.ModuleId == flow.Module.Id &&
                            r.PageIndex == ctx.PageIndex && r.Column == ctx.Column &&
                            Math.Abs(r.Bounds.Y - y) < 0.01);
                        flow.FillWrapped(area, bottom, true, ctx, face.Commands, out ny);
                        if (ny > y + 0.001 || face.Commands.Count > before)
                        {
                            y = ny;
                            placed++;
                            ctx.Warn($"「{flow.Module.Name}」单个区块高于可用区域，已压缩后强制放置，请检查效果。");
                            if (flow.Done) flows.Dequeue();
                        }
                        else
                        {
                            flows.Dequeue();
                            ctx.Warn($"「{flow.Module.Name}」无法排入任何版面，已跳过。");
                        }
                    }
                    break;
                }
                y = ny;
                placed += Math.Max(1, face.Commands.Count - before);
                break;
            }

            // 整栏答题区大外框：把本栏的全部内容（标题 / 考生信息栏 + 选择题 + 非选择题）整体框起来
            if (s.ShowAnswerAreaFrame && (hasAuto || placed > 0))
            {
                double pad = Math.Max(0, s.AnswerAreaFramePadMm);
                double fx = Math.Max(0, content.X - pad);
                double fr = Math.Min(pageW, content.X + content.W + pad);
                double fw = Math.Max(4, fr - fx);
                double ftop = Math.Max(0, content.Y - pad);
                double fbot = Math.Min(pageH, Math.Max(ftop + 6, y + pad));
                string fc = string.IsNullOrWhiteSpace(s.AnswerAreaFrameColor)
                    ? s.BoxColor : s.AnswerAreaFrameColor;
                face.Commands.Add(DrawCmd.Rect(fx, ftop, fw, fbot - ftop, fc,
                    strokeMm: Math.Max(0.05, s.AnswerAreaFrameStrokeMm),
                    dashed: s.AnswerAreaFramePattern == LinePattern.Dashed));
            }

            face.HasContent = hasAuto || span || placed > 0;
            faces.Add(face);
            f++;
        }
        if (f >= MaxFaces) ctx.Warn("内容超过 400 个逻辑栏，已停止分页，请减少题量或增大字号密度。");

        result.TotalPages = (faces.Count + cols - 1) / cols;
        result.TotalFaces = result.TotalPages;
        result.SheetCount = (result.TotalPages + 1) / 2;

        for (int p = 0; p < result.TotalPages; p++)
        {
            var page = new PagePlan
            {
                Index = p, SheetIndex = p / 2, IsFront = p % 2 == 0,
                WidthMm = pageW, HeightMm = pageH
            };
            if (spanCommands.TryGetValue(p, out var list)) page.SpanCommands.AddRange(list);
            for (int c = 0; c < cols; c++)
            {
                int fi = p * cols + c;
                if (fi < faces.Count) page.Faces.Add(faces[fi]);
                else page.Faces.Add(new FacePlan { Index = fi, Column = c,
                    Bounds = new RectMm(c * colW, 0, colW, pageH), HasContent = false });
            }
            page.HasContent = page.SpanCommands.Count > 0 || page.Faces.Any(x => x.HasContent);
            result.Pages.Add(page);
        }
        result.ContentFaces = result.Pages.Count(x => x.HasContent);

        // 后处理：整面一套装饰 + 占位符终值
        foreach (var page in result.Pages)
        {
            if (page.HasContent) DecoratePage(page, s, slots, result, ctx, ins);
            foreach (var face in page.Faces)
                foreach (var c in face.Commands)
                    if (c.Kind == DrawKind.Text) c.Content = Finalize(c.Content, result);
            foreach (var c in page.SpanCommands)
                if (c.Kind == DrawKind.Text) c.Content = Finalize(c.Content, result);
            foreach (var c in page.Decorations)
                if (c.Kind == DrawKind.Text) c.Content = Finalize(c.Content, result);
        }

        result.Regions.AddRange(ctx.Regions.GroupBy(r => r.Key).Select(g => g.First()));
        result.Warnings.AddRange(ctx.Warnings);
        return result;
    }

    /// <summary>按整张纸的一面计算四边预留量（避开四角定位点与每面唯一标识）。</summary>
    private static Insets ComputeInsets(PageSettings s, RectMm whole, int slots, double safe)
    {
        var f = s.FaceId;
        var strip = ResolveStrip(s, whole, slots, safe);
        double outer = Math.Max(s.BaseMarginMm, safe + 1.0);
        double l = outer, t = outer, r = outer, b = outer;
        bool horiz = f.Orientation == IdOrientation.Horizontal;
        bool top = f.Anchor is CornerAnchor.TopLeft or CornerAnchor.TopRight;
        bool left = f.Anchor is CornerAnchor.TopLeft or CornerAnchor.BottomLeft;
        if (horiz)
        {
            if (top) t = Math.Max(t, strip.Bottom + 1.5);
            else b = Math.Max(b, whole.Bottom - strip.Y + 1.5);
        }
        else
        {
            if (left) l = Math.Max(l, strip.Right + 1.5);
            else r = Math.Max(r, whole.Right - strip.X + 1.5);
        }
        double hfH = s.HeaderFooterSizeMm * 1.7;
        double headerY = t;
        double contentTop = t + (s.ShowHeader ? hfH + s.LineGapMm : 0);
        double footerY = whole.Bottom - b - hfH;
        double contentBottom = s.ShowFooter ? footerY - 1.0 : whole.Bottom - b;
        return new Insets
        {
            L = l, T = t, R = r, B = b, Strip = strip,
            HeaderY = headerY, FooterY = footerY,
            ContentTop = contentTop, ContentBottom = contentBottom
        };
    }

    private static string Expand(string template, PageSettings s, int pageIndex, int sheetIndex) =>
        (template ?? "")
        .Replace("{标题}", s.Title ?? "")
        .Replace("{副标题}", s.Subtitle ?? "")
        .Replace("{面}", (pageIndex + 1).ToString())
        .Replace("{页}", (sheetIndex + 1).ToString())
        .Replace("{栏}", "1");

    private static string Finalize(string text, LayoutResult r) => text
        .Replace("{总面}", r.TotalFaces.ToString())
        .Replace("{总页}", r.SheetCount.ToString())
        .Replace("{总栏}", r.Columns.ToString());

    /// <summary>标题 + 副标题 + 考生信息栏，返回实际占用高度。</summary>
    public static double BuildAutoHeader(SheetProject project, RectMm rect, LayoutContext ctx,
        List<DrawCmd> cmds, out double widthUsed, bool collectRegions = true)
    {
        var s = project.Settings;
        widthUsed = rect.W;
        double y = rect.Y;
        if (!string.IsNullOrWhiteSpace(s.Title))
        {
            int lines = Math.Max(1, s.Title.Count(c => c == '\n') + 1);
            double h = s.TitleSizeMm * 1.55 * lines;
            cmds.Add(DrawCmd.Text(rect.X, y, rect.W, h, s.Title, s.TitleSizeMm,
                s.FontFamily, s.TextColor, TextAlign.Center, VAlign.Middle, true, true));
            y += h + s.HeaderGapMm;
        }
        if (!string.IsNullOrWhiteSpace(s.Subtitle))
        {
            int lines = Math.Max(1, s.Subtitle.Count(c => c == '\n') + 1);
            double h = s.SubtitleSizeMm * 1.55 * lines;
            cmds.Add(DrawCmd.Text(rect.X, y, rect.W, h, s.Subtitle, s.SubtitleSizeMm,
                s.FontFamily, s.TextColor, TextAlign.Center, VAlign.Middle, false, true));
            y += h + s.HeaderGapMm;
        }
        if (s.AutoHeader && project.HeaderTemplate is StudentInfoModule info)
        {
            double ih = StudentInfoRenderer.Measure(info, rect.W, ctx);
            StudentInfoRenderer.Build(info, new RectMm(rect.X, y, rect.W, ih), ctx, cmds,
                collectRegions);
            y += ih + s.LineGapMm;
        }
        return y - rect.Y;
    }

    public static double AutoHeaderHeight(SheetProject project, double width, LayoutContext ctx)
    {
        var temp = new List<DrawCmd>();
        return BuildAutoHeader(project, new RectMm(0, 0, width, 1000), ctx, temp, out _, false);
    }

    /// <summary>每面唯一标识的位置；bounds 现在是整张纸的一面。</summary>
    private static RectMm ResolveStrip(PageSettings s, RectMm bounds, int slots, double safe)
    {
        var f = s.FaceId;
        double bw = Math.Max(0.5, f.BlockWidthMm), bh = Math.Max(0.5, f.BlockHeightMm);
        double gap = Math.Max(0, f.GapMm);
        bool horiz = f.Orientation == IdOrientation.Horizontal;
        double w = horiz ? slots * bw + (slots - 1) * gap : bw;
        double h = horiz ? bh : slots * bh + (slots - 1) * gap;
        bool left = f.Anchor is CornerAnchor.TopLeft or CornerAnchor.BottomLeft;
        bool top = f.Anchor is CornerAnchor.TopLeft or CornerAnchor.TopRight;

        double x, y;
        if (f.SameRowAsMarkers)
        {
            // 与四角定位点共用同一条边缘带：横向标识落在上/下定位点那一行，
            // 纵向标识落在左/右定位点那一列；水平 / 垂直方向都自动避开两端的定位点。
            double mi = Math.Max(0, s.MarkerInsetMm);
            double ms = Math.Clamp(s.MarkerSizeMm, 2, 10);
            double avoid = mi + ms + 2.0;
            if (horiz)
            {
                double bandY = top ? bounds.Y + mi : bounds.Bottom - mi - ms;
                y = bandY + (ms - h) / 2.0;                // 与定位点垂直居中对齐
                double lo = bounds.X + avoid;
                double hi = Math.Max(lo, bounds.Right - avoid - w);
                x = left ? Math.Max(lo, bounds.X + f.OffsetXMm)
                         : Math.Min(hi, bounds.Right - f.OffsetXMm - w);
                x = Math.Clamp(x, lo, hi);
                y = Math.Clamp(y, bounds.Y + 1, Math.Max(bounds.Y + 1, bounds.Bottom - h - 1));
            }
            else
            {
                double bandX = left ? bounds.X + mi : bounds.Right - mi - ms;
                x = bandX + (ms - w) / 2.0;                // 与定位点水平居中对齐
                double lo = bounds.Y + avoid;
                double hi = Math.Max(lo, bounds.Bottom - avoid - h);
                y = top ? Math.Max(lo, bounds.Y + f.OffsetYMm)
                        : Math.Min(hi, bounds.Bottom - f.OffsetYMm - h);
                y = Math.Clamp(y, lo, hi);
                x = Math.Clamp(x, bounds.X + 1, Math.Max(bounds.X + 1, bounds.Right - w - 1));
            }
        }
        else if (horiz)
        {
            x = left ? bounds.X + f.OffsetXMm : bounds.Right - f.OffsetXMm - w;
            x = Math.Clamp(x, bounds.X + 1, Math.Max(bounds.X + 1, bounds.Right - w - 1));
            y = top ? bounds.Y + safe + 1 + f.OffsetYMm
                    : bounds.Bottom - safe - 1 - h - f.OffsetYMm;
        }
        else
        {
            y = top ? bounds.Y + f.OffsetYMm : bounds.Bottom - f.OffsetYMm - h;
            y = Math.Clamp(y, bounds.Y + 1, Math.Max(bounds.Y + 1, bounds.Bottom - h - 1));
            x = left ? bounds.X + safe + 1 + f.OffsetXMm
                    : bounds.Right - safe - 1 - w - f.OffsetXMm;
        }
        return new RectMm(x, y, w, h);
    }

    /// <summary>整张纸的一面：一套定位点、一套唯一标识、一条页眉、一条页脚。</summary>
    private static void DecoratePage(PagePlan page, PageSettings s, int slots,
        LayoutResult r, LayoutContext ctx, Insets ins)
    {
        var f = s.FaceId;
        var strip = ins.Strip;
        string black = s.MarkerColor, white = "#FFFFFF", outline = s.BoxColor;
        double bw = Math.Max(0.5, f.BlockWidthMm), bh = Math.Max(0.5, f.BlockHeightMm);
        double gap = Math.Max(0, f.GapMm);
        bool horiz = f.Orientation == IdOrientation.Horizontal;
        int n = Math.Clamp(f.BlockCount, 1, 5);
        var whole = new RectMm(0, 0, page.WidthMm, page.HeightMm);

        var blackFlags = new bool[slots];
        if (f.Style == FaceIdStyle.CombinedBoxes)
        {
            long code = page.Index + 1;
            long max = 1L << n;
            if (code >= max)
            {
                ctx.Warn($"印刷面超过 {max} 面，黑白组合标识（{n} 位）已循环复用，建议增加方块数量。");
                code %= max;
            }
            for (int i = 0; i < Math.Min(n, slots); i++) blackFlags[i] = ((code >> i) & 1L) != 0;
        }
        else
        {
            long combos = Math.Max(1, Comb(slots, n));
            if (combos <= page.Index)
                ctx.Warn("印刷面数超过当前方块位置组合的容量，部分面标识重复，建议增大方块数量或减小方块间距。");
            long rank = page.Index % combos;
            foreach (int i in Unrank(slots, n, rank)) blackFlags[i] = true;
        }

        for (int i = 0; i < slots; i++)
        {
            double x = horiz ? strip.X + i * (bw + gap) : strip.X;
            double y = horiz ? strip.Y : strip.Y + i * (bh + gap);
            x = Math.Clamp(x, whole.X, whole.Right - bw);
            y = Math.Clamp(y, whole.Y, whole.Bottom - bh);
            if (blackFlags[i])
                page.Decorations.Add(DrawCmd.Rect(x, y, bw, bh, black, black, 0));
            else if (f.Style == FaceIdStyle.CombinedBoxes && f.OutlineWhiteBlocks)
                page.Decorations.Add(DrawCmd.Rect(x, y, bw, bh, outline, white, s.BoxStrokeMm));
        }

        // 四角定位点：整幅纸四个角，每个印刷面只有一套
        double inset = s.MarkerInsetMm, size = Math.Clamp(s.MarkerSizeMm, 2, 10);
        var corners = new (double x, double y, int kind)[]
        {
            (whole.X + inset, whole.Y + inset, 0),
            (whole.Right - inset - size, whole.Y + inset, 1),
            (whole.X + inset, whole.Bottom - inset - size, 2),
            (whole.Right - inset - size, whole.Bottom - inset - size, 3)
        };
        foreach (var (x, y, kind) in corners)
        {
            if (s.ForceMarkerBackground)
                page.Decorations.Add(DrawCmd.Rect(x - 0.6, y - 0.6, size + 1.2, size + 1.2,
                    "", white, 0));
            if (s.MarkerStyle == MarkerStyle.Square)
                page.Decorations.Add(DrawCmd.Rect(x, y, size, size, black, black, 0));
            else
            {
                var pts = kind switch
                {
                    0 => new List<PointMm> { new(x, y), new(x + size, y), new(x, y + size) },
                    1 => new List<PointMm> { new(x + size, y), new(x, y), new(x + size, y + size) },
                    2 => new List<PointMm> { new(x, y + size), new(x, y), new(x + size, y + size) },
                    _ => new List<PointMm> { new(x + size, y + size), new(x + size, y), new(x, y + size) }
                };
                var tri = DrawCmd.Polygon(pts, black);
                tri.X = x; tri.Y = y; tri.W = size; tri.H = size;
                page.Decorations.Add(tri);
            }
        }

        // 整面一条页眉 / 一条页脚（栏内不再重复）
        double hfH = s.HeaderFooterSizeMm * 1.7;
        double hfW = Math.Max(1, page.WidthMm - ins.L - ins.R);
        if (s.ShowHeader)
            page.Decorations.Add(DrawCmd.Text(ins.L, ins.HeaderY, hfW, hfH,
                Expand(s.HeaderText, s, page.Index, page.SheetIndex), s.HeaderFooterSizeMm,
                s.FontFamily, s.TextColor, TextAlign.Center, VAlign.Middle));
        if (s.ShowFooter)
            page.Decorations.Add(DrawCmd.Text(ins.L, ins.FooterY, hfW, hfH,
                Expand(s.FooterText, s, page.Index, page.SheetIndex), s.HeaderFooterSizeMm,
                s.FontFamily, s.TextColor, TextAlign.Center, VAlign.Middle));
    }

    private static long Comb(int n, int k)
    {
        if (k < 0 || k > n || n > 60) return 0;
        k = Math.Min(k, n - k);
        long v = 1;
        for (int i = 1; i <= k; i++)
        {
            v = v * (n - k + i) / i;
            if (v > 100_000_000L) return 100_000_000L;
        }
        return v;
    }

    /// <summary>组合数系统反排名：返回 size k 的第 rank 个组合（互不相同）。</summary>
    private static int[] Unrank(int n, int k, long rank)
    {
        var result = new int[k];
        int j = k;
        for (int i = n - 1; i >= 0 && j > 0; i--)
        {
            long c = Comb(i, j);
            if (rank >= c) { rank -= c; result[--j] = i; }
            else if (c == 0 && rank == 0) { result[--j] = i; }
        }
        while (j > 0) result[--j] = 0;
        return result;
    }
}
