using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

/// <summary>
/// 考生信息栏：上排「字段 + 条形码区」，下排「准考证号填涂区」（标题行 + 手写行 + 10×N 网格），
/// 末尾是注意事项。自动页首和独立模块共用。
/// 仅涂考号区且网格右置时，注意事项改放到左侧字段区正下方，与右侧网格共享高度以节约版面。
/// </summary>
public static class StudentInfoRenderer
{
    private const double Pad = 2.5;
    private const int GridRows = 10;

    public static string[] Fields(StudentInfoModule m) =>
        (m.FieldLabels ?? "").Split(new[] { ',', '，', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

    private static bool HasBarcode(StudentInfoModule m) =>
        m.Mode is InfoBarMode.BarcodeOnly or InfoBarMode.Both;

    private static bool HasGrid(StudentInfoModule m) =>
        m.Mode is InfoBarMode.CandidateNoOnly or InfoBarMode.Both;

    /// <summary>只涂考号时（无条形码），考号网格移到字段区右侧并排。</summary>
    private static bool SideBySide(StudentInfoModule m) =>
        m.Mode == InfoBarMode.CandidateNoOnly && m.CandidateSideBySide && HasGrid(m);

    private static bool HasNotice(StudentInfoModule m) =>
        m.ShowNotice && !string.IsNullOrWhiteSpace(m.NoticeText);

    /// <summary>并排时把注意事项并入左侧列，与右侧考号网格共享高度，不再在整幅底部独占一行。</summary>
    private static bool TuckNotice(StudentInfoModule m, bool side) =>
        side && m.NoticeUnderFieldsWhenSide && HasNotice(m);

    /// <summary>并排时左侧列宽度；与 Build 里的 leftW 保持一致。</summary>
    private static double SideLeftWidth(double innerW, GridMetrics gm) =>
        Math.Max(20, innerW - (gm == null ? 0 : gm.Width) - 3);

    private static double UpperHeight(StudentInfoModule m, int fieldRows, bool barcode) =>
        Math.Max(fieldRows * m.FieldHeightMm, barcode ? m.BarcodeHeightMm + 2.0 : 0) + 1.0;

    /// <summary>上下排列时高度相加；左右并排时取两边较大者。Measure 与 Build 共用，保证一致。</summary>
    private static double BodyHeight(double upperH, GridMetrics gm, bool side)
    {
        double gridH = gm == null ? 0 : gm.Height + 2.0;
        return side ? Math.Max(upperH, gridH) : upperH + gridH;
    }

    /// <summary>考号区所有实际尺寸（毫米）。「整体大小 / 指定宽 / 指定总高」都在这里统一生效。</summary>
    private sealed class GridMetrics
    {
        public double Width, Height, MinWidth, MaxWidth;
        public double TitleH, HandH, RowH;
        public double TitleFs, Stroke;
        public double UnitW, UnitH, OffsetY;
        public FillStyleSpec Spec;
        public int Digits;
        public bool Lines;
    }

    private static GridMetrics ComputeGrid(StudentInfoModule m, double innerW, LayoutContext ctx)
    {
        double scale = Math.Clamp(m.CandidateScalePercent, 30, 250) / 100.0;
        int digits = Math.Clamp(m.CandidateDigits, 1, 20);
        double pct = Math.Clamp(m.CandidateAreaWidthPercent, 20, 100) / 100.0;

        // 宽度：优先用「指定宽度(mm)」，否则按百分比；整体大小也参与，但不超出所在栏。
        // 仅涂考号且并排时，宽度按「右方占比」算，并留出左侧字段区的空间。
        bool side = SideBySide(m);
        double width, minWidth, maxWidth;
        if (side)
        {
            double sidePct = Math.Clamp(m.CandidateSidePercent, 25, 75) / 100.0;
            width = innerW * sidePct * scale;
            if (m.CandidateAreaWidthMm > 0) width = m.CandidateAreaWidthMm;
            minWidth = Math.Min(10, innerW * 0.3); maxWidth = innerW * 0.78;
            width = Math.Clamp(width, minWidth, maxWidth);
        }
        else
        {
            width = innerW * pct * scale;
            if (m.CandidateAreaWidthMm > 0) width = m.CandidateAreaWidthMm;
            minWidth = Math.Min(10, innerW); maxWidth = innerW;
            width = Math.Clamp(width, minWidth, maxWidth);
        }

        // 高度：按各行高自动，或用「指定总高(mm)」整体重排
        double titleH = Math.Max(2, m.CandidateTitleHeightMm) * scale;
        double handH = m.ShowHandwriteRow ? Math.Max(1.5, m.HandwriteRowHeightMm) * scale : 0;
        double rowH = Math.Max(1.5, m.CandidateRowHeightMm) * scale;
        double autoH = titleH + handH + GridRows * rowH;
        double height = m.CandidateAreaHeightMm > 0
            ? Math.Max(10, m.CandidateAreaHeightMm) : autoH;
        double vScale = height / autoH;
        titleH *= vScale;
        handH *= vScale;
        rowH *= vScale;

        // 内部元素随「整体大小」和「指定总高」一起等比缩放，避免只改高度导致框与字不匹配
        double elem = scale * vScale;
        double colW = width / digits;

        // 填涂单元：默认与选择题共用「全卷填涂样式」模板；样式类型（方框 / [0]）本区独立选择。
        // 先按模板尺寸计算真实单元宽高，再等比收敛到单元格内，绝不越出网格。
        var spec = FillStyleSpec.ForCandidate(ctx.S, m).Scaled(elem);
        double maxW = Math.Max(2, colW - 0.8);
        double maxH = Math.Max(1, rowH - 0.5);
        double w0 = spec.UnitWidth("0", ctx.Family);
        double h0 = Math.Max(0.1, spec.UnitHeight);
        double k = Math.Min(1, Math.Min(maxW / Math.Max(0.1, w0), maxH / h0));
        if (k < 1) spec = spec.Scaled(k);
        double unitW = Math.Min(spec.UnitWidth("0", ctx.Family), maxW);
        double unitH = Math.Min(spec.UnitHeight, maxH);

        double titleFs = Math.Max(1, m.CandidateTitleSizeMm * elem);
        double stroke = Math.Max(0.05, m.CandidateLineStrokeMm * elem);

        if (spec.NeedsAutoFit(unitH))
            ctx.Warn($"「准考证号填涂区」数字框高 {unitH:0.0}mm 小于数字字号所需高度，" +
                     "已自动缩小 0~9 的字号以保证完整显示；若想保持原字号，可在「全卷填涂样式」关闭「字符自动适应框高」。");

        return new GridMetrics
        {
            Width = width, Height = height, MinWidth = minWidth, MaxWidth = maxWidth,
            TitleH = titleH, HandH = handH, RowH = rowH,
            TitleFs = titleFs, Stroke = stroke,
            UnitW = unitW, UnitH = unitH, OffsetY = spec.OffsetYMm,
            Spec = spec, Digits = digits,
            Lines = m.CandidateGridLines
        };
    }

    private static int NoticeLines(StudentInfoModule m) =>
        Math.Max(1, (m.NoticeText ?? "").Split('\n').Length);

    /// <summary>整幅排布时注意事项占的高度（原有行为）。</summary>
    private static double NoticeFullHeight(StudentInfoModule m) =>
        NoticeLines(m) * m.FontSizeMm * 1.6 + 3.0;

    /// <summary>窄栏排布时按真实折行数估高：上方呼吸 1.5 + 分隔线后 1.5 + 文本行高 + 底部余量 1.5。</summary>
    private static double NoticeNarrowHeight(StudentInfoModule m, double width, string family)
    {
        int lines = TextMeasurer.WrappedLines(m.NoticeText, m.FontSizeMm, family,
            Math.Max(10, width));
        return lines * m.FontSizeMm * 1.5 + 4.5;
    }

    public static double Measure(StudentInfoModule m, double width, LayoutContext ctx)
    {
        double titleH = m.ShowInfoTitle && !string.IsNullOrWhiteSpace(m.InfoTitle)
            ? m.FontSizeMm * 1.8 + 1.5 : 0;
        int rows = (Fields(m).Length + 1) / 2;
        double upperH = UpperHeight(m, rows, HasBarcode(m));
        double innerW = Math.Max(10, width - Pad * 2);
        var gm = HasGrid(m) ? ComputeGrid(m, innerW, ctx) : null;
        bool side = SideBySide(m);
        double bodyH = BodyHeight(upperH, gm, side);
        double noticeH = HasNotice(m) ? NoticeFullHeight(m) : 0;
        if (TuckNotice(m, side))
        {
            // 注意事项放进左侧字段区下方，整块高度 = max(左列, 右列)，省掉底部一整行
            bodyH = Math.Max(upperH + NoticeNarrowHeight(m, SideLeftWidth(innerW, gm), ctx.Family),
                             gm == null ? 0 : gm.Height + 2.0);
            noticeH = 0;
        }
        return Pad * 2 + titleH + bodyH + noticeH + 1.0;
    }

    public static void Build(StudentInfoModule m, RectMm area, LayoutContext ctx,
        List<DrawCmd> cmds, bool collectRegions = true)
    {
        var fam = ctx.Family;
        var txt = ctx.S.TextColor;
        var box = m.EffectiveBoxColor(ctx.S);
        double stroke = ctx.S.BoxStrokeMm;
        double fs = m.FontSizeMm;

        // 信息栏外框可关；关掉后内部标题横线、考号网格线、注意事项分隔线仍保留
        if (m.ShowInfoFrame)
            cmds.Add(DrawCmd.Rect(area.X, area.Y, area.W, area.H,
                m.EffectiveGroupFrameColor(ctx.S), strokeMm: stroke,
                dashed: m.GroupFramePattern == LinePattern.Dashed));

        double y = area.Y + Pad;
        double innerW = Math.Max(10, area.W - Pad * 2);
        double x0 = area.X + Pad;

        if (m.ShowInfoTitle && !string.IsNullOrWhiteSpace(m.InfoTitle))
        {
            cmds.Add(DrawCmd.Text(x0, y, innerW, fs * 1.8, m.InfoTitle,
                fs * 1.15, fam, txt, TextAlign.Left, VAlign.Middle, true));
            cmds.Add(DrawCmd.Line(x0, y + fs * 1.8, x0 + innerW, y + fs * 1.8, box, stroke));
            y += fs * 1.8 + 1.5;
        }

        double bodyTop = y;
        bool barcode = HasBarcode(m);
        bool side = SideBySide(m);
        var fields = Fields(m);
        int rows = (fields.Length + 1) / 2;
        double upperH = UpperHeight(m, rows, barcode);
        var gm = HasGrid(m) ? ComputeGrid(m, innerW, ctx) : null;
        double bodyH = BodyHeight(upperH, gm, side);
        bool tuck = TuckNotice(m, side);
        double tuckH = tuck ? NoticeNarrowHeight(m, SideLeftWidth(innerW, gm), fam) : 0;
        if (tuck)
            bodyH = Math.Max(upperH + tuckH, gm == null ? 0 : gm.Height + 2.0);

        double rightW = barcode
            ? Math.Min(innerW * 0.45, Math.Max(20, m.BarcodeWidthMm + 4)) : 0;
        double gridW = side ? gm.Width : 0;
        double gap = (rightW > 0 ? 3 : 0) + (gridW > 0 ? 3 : 0);
        double leftW = Math.Max(20, innerW - rightW - gridW - gap);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < 2; c++)
            {
                int idx = r * 2 + c;
                if (idx >= fields.Length) break;
                double x = x0 + c * (leftW / 2);
                double cw = leftW / 2 - 1.5;
                string label = fields[idx];
                double lw = TextMeasurer.Width(label, fs, fam) + 1.0;
                cmds.Add(DrawCmd.Text(x, bodyTop + r * m.FieldHeightMm, lw, m.FieldHeightMm,
                    label, fs, fam, txt, TextAlign.Left, VAlign.Middle));
                cmds.Add(DrawCmd.Line(x + lw, bodyTop + r * m.FieldHeightMm + m.FieldHeightMm * 0.78,
                    x + cw, bodyTop + r * m.FieldHeightMm + m.FieldHeightMm * 0.78, box, stroke));
            }
        }

        if (barcode)
        {
            double rx = x0 + leftW + 3;
            double bw = Math.Min(Math.Max(10, rightW - 1), m.BarcodeWidthMm);
            cmds.Add(DrawCmd.Rect(rx, bodyTop, bw, m.BarcodeHeightMm, box, strokeMm: stroke, dashed: true));
            cmds.Add(DrawCmd.Text(rx, bodyTop + m.BarcodeHeightMm * 0.70, bw, m.BarcodeHeightMm * 0.26,
                m.BarcodeText, fs, fam, txt, TextAlign.Center, VAlign.Middle));
            var rnd = new Random(20261110);
            double bx = rx + 2;
            while (bx < rx + bw - 2)
            {
                double w = 0.25 + rnd.NextDouble() * 0.7;
                cmds.Add(DrawCmd.Rect(bx, bodyTop + 2, w, Math.Max(3, m.BarcodeHeightMm * 0.55), "", txt, 0, 0));
                bx += w + 0.35 + rnd.NextDouble() * 0.8;
            }
        }

        if (gm != null)
        {
            // 仅涂考号区：与字段区左右并排；其它组合：字段区下方整幅居中
            double gx = side ? x0 + leftW + 3 : x0 + (innerW - gm.Width) / 2;
            double gy = side ? bodyTop : bodyTop + upperH;
            if (collectRegions && ctx.CollectRegions)
                ctx.Regions.Add(new ModuleRegion
                {
                    Kind = ModuleRegionKind.CandidateGrid,
                    ModuleId = m.Id, Module = m, Label = "准考证号填涂区",
                    PageIndex = ctx.PageIndex, Column = ctx.Column,
                    Bounds = new RectMm(gx, gy, gm.Width, gm.Height),
                    BaseWidthMm = innerW, ContentX = area.X + Pad,
                    MinWidthMm = gm.MinWidth, MaxWidthMm = gm.MaxWidth,
                    MinHeightMm = 10, MaxHeightMm = 300,
                    IsFirstSegment = true, CanResizeHeight = true
                });
            DrawGrid(m, new RectMm(gx, gy, gm.Width, gm.Height), gm, ctx, cmds);
        }

        y = bodyTop + bodyH;

        if (HasNotice(m))
        {
            if (tuck)
            {
                // 左列：分隔线只跨字段区宽度，绝不穿过右侧考号网格
                double ny = bodyTop + upperH + 1.5;
                cmds.Add(DrawCmd.Line(x0, ny, x0 + leftW, ny, box, stroke * 0.8, true));
                ny += 1.5;
                double nh = Math.Max(1, tuckH - 4.5);
                cmds.Add(DrawCmd.Text(x0, ny, leftW, nh, m.NoticeText,
                    fs, fam, txt, TextAlign.Left, VAlign.Top, wrap: true));
            }
            else
            {
                cmds.Add(DrawCmd.Line(x0, y, x0 + innerW, y, box, stroke * 0.8, true));
                y += 1.5;
                double nh = NoticeLines(m) * fs * 1.6;
                cmds.Add(DrawCmd.Text(x0, y, innerW, nh, m.NoticeText,
                    fs, fam, txt, TextAlign.Left, VAlign.Top, wrap: true));
            }
        }
    }

    /// <summary>图示样式：标题行 + 手写数字行 + 0~9 十行 × 考号位数列，每格一个小方框内含数字。</summary>
    private static void DrawGrid(StudentInfoModule m, RectMm a, GridMetrics gm,
        LayoutContext ctx, List<DrawCmd> cmds)
    {
        var fam = ctx.Family;
        var txt = ctx.S.TextColor;
        var box = m.EffectiveItemFrameColor(ctx.S);
        double stroke = gm.Stroke;
        int digits = gm.Digits;
        double titleH = gm.TitleH, handH = gm.HandH, rowH = gm.RowH;
        double rowsH = GridRows * rowH;
        double W = a.W, H = a.H;
        double colW = W / digits;
        double gridY = a.Y + titleH + handH;
        // 小题框线型统一控制整块考号网格（外框 + 内部行列线），避免外框虚线、内部实线不协调
        bool itemDashed = m.ItemFramePattern == LinePattern.Dashed;

        if (gm.Lines)
            cmds.Add(DrawCmd.Rect(a.X, a.Y, W, H, box,
                strokeMm: stroke, dashed: itemDashed));

        cmds.Add(DrawCmd.Text(a.X, a.Y, W, titleH, m.CandidateTitle,
            gm.TitleFs, fam, txt, TextAlign.Center, VAlign.Middle));
        if (gm.Lines)
            cmds.Add(DrawCmd.Line(a.X, a.Y + titleH, a.X + W, a.Y + titleH,
                box, stroke, itemDashed));

        // 手写数字行：只有竖分隔线，格内留白
        if (handH > 0 && gm.Lines)
        {
            for (int c = 1; c < digits; c++)
            {
                double lx = a.X + c * colW;
                cmds.Add(DrawCmd.Line(lx, a.Y + titleH, lx, gridY, box, stroke, itemDashed));
            }
            cmds.Add(DrawCmd.Line(a.X, gridY, a.X + W, gridY, box, stroke, itemDashed));
        }

        // 填涂矩阵网格线
        if (gm.Lines)
        {
            for (int r = 1; r < GridRows; r++)
            {
                double ly = gridY + r * rowH;
                cmds.Add(DrawCmd.Line(a.X, ly, a.X + W, ly, box, stroke, itemDashed));
            }
            for (int c = 1; c < digits; c++)
            {
                double lx = a.X + c * colW;
                cmds.Add(DrawCmd.Line(lx, gridY, lx, gridY + rowsH, box, stroke, itemDashed));
            }
        }

        double bw = gm.UnitW, bh = gm.UnitH;
        double oy = Math.Clamp(gm.OffsetY, -(rowH - bh) / 2, (rowH - bh) / 2);

        for (int r = 0; r < GridRows; r++)
        {
            for (int c = 0; c < digits; c++)
            {
                double bx = a.X + c * colW + (colW - bw) / 2;
                double by = gridY + r * rowH + (rowH - bh) / 2 + oy;
                // 与选择题 [A] 共用绘制器：本区可独立设成方框或字面中括号 [0]
                FillStyleRenderer.Draw(cmds, bx, by, bw, bh, r.ToString(),
                    gm.Spec, fam, txt, box);
            }
        }
    }
}
