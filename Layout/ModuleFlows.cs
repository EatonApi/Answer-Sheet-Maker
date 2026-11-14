using System.Windows.Media.Imaging;
using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

public sealed class LayoutContext
{
    public SheetProject Project { get; init; }
    public PageSettings S => Project.Settings;
    public string Family => S.FontFamily;
    public List<string> Warnings { get; } = new();
    /// <summary>本次排版中页面上可直接拖拽调整的区域（仅真实构建时收集）。</summary>
    public List<ModuleRegion> Regions { get; } = new();
    public int PageIndex, Column;
    public bool CollectRegions = true;

    public void Warn(string message)
    {
        if (!Warnings.Contains(message)) Warnings.Add(message);
    }
}

public abstract class ModuleFlow
{
    public ModuleBase Module { get; }
    public bool Done { get; protected set; }
    protected bool TitleDrawn;
    protected int Cursor;

    protected ModuleFlow(ModuleBase module) { Module = module; }

    public abstract void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY);

    /// <summary>
    /// 统一的区域包装：① 按「区域宽度 / 水平对齐」收窄排版区；② 把内容向内缩进后交给各 Flow 排版；
    /// ③ 按「指定区域高度 / 最小高度」调整整块尺寸（调大留白、调小等比压缩）；
    /// ④ 最外层补整组外框（跨栏续排时每栏各自成框）。所有模块都可用，非选择题模块默认开启外框。
    /// </summary>
    private const double GroupFramePad = 2.8;

    public void FillWrapped(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        // 1) 区域宽度与栏内水平对齐
        double pct = Math.Clamp(Module.RegionWidthPercent, 20, 100) / 100.0;
        double w = Math.Max(10, area.W * pct);
        double x = Module.RegionAlign switch
        {
            RegionAlign.Center => area.X + (area.W - w) / 2,
            RegionAlign.Right => area.X + area.W - w,
            _ => area.X
        };
        // 水平偏移由页面上直接拖拽写入：以左边界为锚点，避免改宽度时被对齐方式挪走
        if (Module.RegionOffsetMm > 0) x = area.X + Module.RegionOffsetMm;
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.W - w));
        var region = new RectMm(x, area.Y, w, Math.Max(0, bottom - area.Y));

        bool framed = Module.ShowGroupFrame;
        var work = framed ? region.Inset(GroupFramePad, GroupFramePad, GroupFramePad, 0) : region;
        double effBottom = Math.Max(work.Y, bottom - (framed ? GroupFramePad : 0));

        // 指定高度只作用于模块的第一段；跨栏 / 跨面续排部分保持自然高度
        bool firstSegment = Cursor == 0 && !TitleDrawn;
        int before = cmds.Count;
        int regionBefore = ctx.Regions.Count;
        Fill(work, effBottom, force, ctx, cmds, out double innerY);
        bool hasContent = cmds.Count > before || innerY > work.Y + 0.001;
        // 没有真正放进内容时必须原样返回，否则会被分页循环误判为「有进展」
        if (!hasContent) { newY = area.Y; return; }

        // 2) 内容自然高度 -> 指定 / 最小高度
        double gap = Math.Max(0, ctx.S.LineGapMm);
        double span = Math.Max(1, innerY - area.Y);                  // 含尾部行间距
        double outer = Math.Max(1, span - gap + (framed ? GroupFramePad : 0)); // 外框自然高
        double available = Math.Max(1, bottom - area.Y);
        double maxFrame = Math.Max(1, available - gap);
        double desired = outer;
        if (Module.RegionMinHeightMm > 0) desired = Math.Max(desired, Module.RegionMinHeightMm);
        if (firstSegment && Module.RegionHeightMm > 0) desired = Module.RegionHeightMm;
        if (desired > maxFrame)
        {
            ctx.Warn($"「{Module.Name}」区域指定高度 {desired:0.0}mm 超过本面可用 {maxFrame:0.0}mm，已收敛到可用高度。");
            desired = maxFrame;
        }

        // 3) 调小：把本次内容整体等比压缩；调大：内容在上、下方留白作答
        double k = 1;
        if (desired < outer - 0.01)
        {
            k = Math.Max(0.45, desired / outer);
            if (k > desired / outer + 0.001)
                ctx.Warn($"「{Module.Name}」区域高度过小，内容最多压缩到自然高度的 45%，请调大高度或减少题量。");
            ScaleVertical(cmds, before, area.Y, k);
        }
        // 内部子区域（解答题每题框、考号网格）也必须跟着压缩，否则预览页手柄与真实框错位
        if (k < 1 && ctx.CollectRegions)
            ScaleRegions(ctx.Regions, regionBefore, area.Y, k);
        double frameH = Math.Min(maxFrame, Math.Max(outer * k, desired));
        if (framed && frameH > 2)
            cmds.Add(DrawCmd.Rect(region.X, region.Y, region.W, frameH,
                Module.EffectiveGroupFrameColor(ctx.S), strokeMm: ctx.S.BoxStrokeMm,
                dashed: Module.GroupFramePattern == LinePattern.Dashed));

        // 记录真实区域矩形，供预览页直接拖拽调整大小
        if (ctx.CollectRegions)
            ctx.Regions.Add(new ModuleRegion
            {
                Kind = ModuleRegionKind.Region,
                ModuleId = Module.Id, Module = Module, Label = Module.Name,
                PageIndex = ctx.PageIndex, Column = ctx.Column,
                Bounds = new RectMm(region.X, region.Y, region.W, frameH),
                BaseWidthMm = area.W, ContentX = area.X,
                MinWidthMm = Math.Max(2, area.W * 0.20), MaxWidthMm = area.W,
                // 拖到过矮会挤坏版式：拖动下限取自然高度的一半，更极端的值仍可在属性面板里填
                MinHeightMm = Math.Max(8, outer * 0.5), MaxHeightMm = Math.Max(20, available - gap),
                IsFirstSegment = firstSegment, CanResizeHeight = firstSegment
            });

        newY = Math.Min(area.Y + frameH + gap, bottom);
    }

    /// <summary>把本次 Fill 内部收集的子区域按同一比例垂直压缩（RectMm 是只读结构，整体替换）。</summary>
    private static void ScaleRegions(List<ModuleRegion> regions, int start, double originY, double k)
    {
        for (int i = start; i < regions.Count; i++)
        {
            var r = regions[i];
            var b = r.Bounds;
            r.Bounds = new RectMm(b.X, originY + (b.Y - originY) * k,
                b.W, Math.Max(1, b.H * k));
        }
    }

    /// <summary>把本次新增的绘制指令以 originY 为轴做垂直等比缩放（字号与线宽不变）。</summary>
    private static void ScaleVertical(List<DrawCmd> cmds, int start, double originY, double k)
    {
        for (int i = start; i < cmds.Count; i++)
        {
            var c = cmds[i];
            c.Y = originY + (c.Y - originY) * k;
            if (c.Kind == DrawKind.Line)
                c.Y2 = originY + (c.Y2 - originY) * k;
            // 文字块不压缩高度：字号本身不会缩小，压 H 会把标题 / 题号裁掉
            if (c.Kind != DrawKind.Text) c.H = Math.Max(0, c.H * k);
            if (c.Kind == DrawKind.Polygon)
                for (int j = 0; j < c.Points.Count; j++)
                {
                    var p = c.Points[j];
                    c.Points[j] = new PointMm(p.X, originY + (p.Y - originY) * k);
                }
        }
    }

    protected double TitleHeight(LayoutContext ctx) =>
        Module.ShowTitle && !string.IsNullOrWhiteSpace(Module.Title) ? Module.TitleSizeMm * 1.5 : 0;

    protected void EmitTitle(RectMm area, ref double y, LayoutContext ctx, List<DrawCmd> cmds)
    {
        double h = TitleHeight(ctx);
        if (h <= 0) { TitleDrawn = true; return; }
        string title = Module.Title + (Cursor > 0 ? "（续）" : "");
        cmds.Add(DrawCmd.Text(area.X, y, area.W, h, title,
            Module.TitleSizeMm, ctx.Family, ctx.S.TextColor,
            TextAlign.Left, VAlign.Middle, true));
        y += h + ctx.S.LineGapMm;
        TitleDrawn = true;
    }

    protected static bool Fits(double y, double h, double bottom, bool force) =>
        force || y + h <= bottom + 0.001;

    protected void Finish(bool force, double y, out double newY)
    {
        Done = Done || Cursor >= Total();
        if (!Done) TitleDrawn = false;
        newY = y;
    }

    protected abstract int Total();
}

public static class ModuleFlowFactory
{
    public static ModuleFlow Create(ModuleBase m) => m switch
    {
        ChoiceModule => new ChoiceFlow(m),
        BlankModule => new BlankFlow(m),
        SolutionModule => new SolutionFlow(m),
        EssayModule => new EssayFlow(m),
        NonAnswerModule => new NonAnswerFlow(m),
        StudentInfoModule => new InfoFlow(m),
        ImageModule => new ImageFlow(m),
        CustomModule => new CustomFlow(m),
        _ => new CustomFlow(m)
    };
}

public sealed class ChoiceFlow : ModuleFlow
{
    private sealed class Parts
    {
        public double NumW, OptW, LineW, Pad, Total;
        public double MaxBoxW;
        public bool Stack;
        /// <summary>选项竖排时的填涂框高度与上下步进（Fill 中按可用高度再收敛）。</summary>
        public double UnitH, Step;
        public double[] BoxW = Array.Empty<double>();
    }

    public ChoiceFlow(ModuleBase m) : base(m) { }
    protected override int Total() => ((ChoiceModule)Module).QuestionCount;

    // 单题（按该题自己的选项标签）的尺寸测量；numW 为全局题号最大宽度（已含留白）
    private static Parts MeasureOne(ChoiceModule m, string[] labels, LayoutContext ctx, FillStyleSpec baseSpec, double numW)
    {
        var boxW = new double[labels.Length];
        double optW = 0, maxBox = 0;
        if (m.Display != ChoiceDisplay.Underline)
        {
            // 单元宽度完全由生效的填涂规格决定（全卷模板或模块单独设定）
            for (int i = 0; i < labels.Length; i++)
            {
                boxW[i] = baseSpec.UnitWidth(labels[i], ctx.Family);
                optW += boxW[i] + m.OptionGapMm;
                maxBox = Math.Max(maxBox, boxW[i]);
            }
        }
        double lineW = m.Display switch
        {
            ChoiceDisplay.Underline => m.UnderlineLengthMm,
            ChoiceDisplay.Both => m.UnderlineLengthMm + 2.0,
            _ => 0
        };
        double pad = 2.0;
        // 选项竖排时每题只占一个填涂框的宽度，但要用掉「选项个数」的高度
        bool stack = m.OptionLayout == OptionLayout.Vertical && m.Display != ChoiceDisplay.Underline;
        double optSpan = stack ? maxBox : optW;
        return new Parts { NumW = numW, OptW = optW, LineW = lineW, Pad = pad, BoxW = boxW,
                           MaxBoxW = maxBox, Stack = stack,
                           Total = numW + optSpan + lineW + pad };
    }

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (ChoiceModule)Module;
        var baseSpec = FillStyleSpec.ForChoice(ctx.S, m);

        // 全局题号最大宽度（所有题共用同一题号列宽，避免题号长短导致错位）
        double numW = 0;
        if (m.ShowNumber)
        {
            for (int i = 0; i < m.QuestionCount; i++)
                numW = Math.Max(numW, TextMeasurer.Width(m.NumberFor(i), m.FontSizeMm, ctx.Family));
            numW += 1.2;
        }

        // 逐题测量，找「最宽单题」与「最高行」，用统一的单元格尺寸，保证不重叠、不溢出栏宽
        double maxTotal = 0;
        for (int i = 0; i < m.QuestionCount; i++)
        {
            var labels = m.LabelsFor(i);
            if (labels.Length == 0) labels = new[] { "A" };
            var p0 = MeasureOne(m, labels, ctx, baseSpec, numW);
            maxTotal = Math.Max(maxTotal, p0.Total);
        }

        double scale = maxTotal > area.W ? Math.Max(0.30, area.W / maxTotal) : 1.0;
        if (scale < 0.999)
            ctx.Warn($"「{m.Name}」单题实测宽度 {maxTotal:0.0}mm 超过栏宽，已自动缩放 {scale:0.00} 倍以保证整题不溢出。");
        var spec = baseSpec.Scaled(scale);
        if (m.Display != ChoiceDisplay.Underline && spec.NeedsAutoFit(spec.UnitHeight))
            ctx.Warn($"「{m.Name}」填涂框高 {spec.UnitHeight:0.0}mm 小于字符字号所需高度，" +
                     "已自动缩小 [A] 字符以保证完整显示；可在「全卷填涂样式」关闭「字符自动适应框高」。");

        double itemW = maxTotal * scale;
        double baseUnitH = Math.Max(1, spec.UnitHeight);
        double stepBase = baseUnitH + Math.Max(0, m.OptionStackGapMm * scale);
        bool stack = m.OptionLayout == OptionLayout.Vertical && m.Display != ChoiceDisplay.Underline;
        // 最大行高：竖排取决于选项最多的那题；横排所有题一致
        double maxRowH = 0;
        if (stack)
        {
            for (int i = 0; i < m.QuestionCount; i++)
            {
                var labels = m.LabelsFor(i);
                if (labels.Length == 0) labels = new[] { "A" };
                maxRowH = Math.Max(maxRowH, Math.Max(m.RowHeightMm, stepBase * labels.Length));
            }
        }
        else
        {
            maxRowH = Math.Max(m.RowHeightMm, baseUnitH + 2.0);
        }
        double rowH = maxRowH;

        double colGap = m.ColumnGapMm;
        int maxByWidth = Math.Max(1, (int)Math.Floor((area.W + colGap) / (itemW + colGap)));
        int columns = m.QuestionLayout == ChoiceLayout.Vertical
            ? Math.Min(Math.Max(1, m.VerticalColumns), maxByWidth)
            : maxByWidth;

        double y = area.Y;
        if (!TitleDrawn)
        {
            double th = TitleHeight(ctx);
            if (!Fits(y, th + rowH, bottom, force)) { newY = y; return; }
            if (force && th + rowH > bottom - y) rowH = Math.Max(4, bottom - y - th);
            EmitTitle(area, ref y, ctx, cmds);
        }

        // 竖排：按列优先（column-major）——先向下填满第 1 列（1、2…），再换到右侧第 2 列（3、4…）
        if (m.QuestionLayout == ChoiceLayout.Vertical)
        {
            double gap = ctx.S.LineGapMm;
            int remaining = m.QuestionCount - Cursor;
            int perColIdeal = (int)Math.Ceiling((double)remaining / columns);
            int rowsMax = Math.Max(1, (int)Math.Floor((bottom - y + gap) / (rowH + gap)));
            int rowsPerCol = Math.Min(perColIdeal, rowsMax);
            int slots = columns * rowsPerCol;
            int n = Math.Min(slots, remaining);
            for (int k = 0; k < n; k++)
            {
                int col = k / rowsPerCol;
                int r = k % rowsPerCol;
                double x = area.X + col * (itemW + colGap);
                double yy = y + r * (rowH + gap);
                var labels = m.LabelsFor(Cursor + k);
                if (labels.Length == 0) labels = new[] { "A" };
                EmitQuestion(cmds, m, labels, baseSpec, numW, scale, spec, x, yy, itemW, rowH, Cursor + k, ctx);
            }
            Cursor += n;
            Done = Cursor >= m.QuestionCount;
            if (!Done) TitleDrawn = false;
            newY = Math.Min(bottom, y + rowsPerCol * (rowH + gap));
            return;
        }

        // 横排：行优先（row-major），每行从左到右排 columns 道题
        int rows = 0;
        while (Cursor < m.QuestionCount)
        {
            double h = rowH;
            if (h > bottom - y)
            {
                if (!force) break;
                h = Math.Max(4, bottom - y);
            }
            int count = Math.Min(columns, m.QuestionCount - Cursor);
            double x = area.X;
            for (int c = 0; c < count; c++, Cursor++)
            {
                var labels = m.LabelsFor(Cursor);
                if (labels.Length == 0) labels = new[] { "A" };
                EmitQuestion(cmds, m, labels, baseSpec, numW, scale, spec, x, y, itemW, h, Cursor, ctx);
                x += itemW + colGap;
            }
            y += h + ctx.S.LineGapMm;
            rows++;
            if (force) break;
        }
        Done = Cursor >= m.QuestionCount;
        if (!Done) TitleDrawn = false;
        newY = y > bottom ? bottom : y;
    }

    private static void EmitQuestion(List<DrawCmd> cmds, ChoiceModule m, string[] labels,
        FillStyleSpec baseSpec, double numW, double scale, FillStyleSpec spec,
        double x, double y, double itemW, double h, int index, LayoutContext ctx)
    {
        string fam = ctx.Family, txt = ctx.S.TextColor, box = m.EffectiveBoxColor(ctx.S);
        double fs = m.FontSizeMm * scale, stroke = ctx.S.BoxStrokeMm;
        double x0 = x;
        // 可选的小题框：整道题（题号 + 选项 / 手写线）单独成框，颜色与线型取通用组的「小题框」设置
        if (m.ShowQuestionFrame)
            cmds.Add(DrawCmd.Rect(x, y, itemW, h, m.EffectiveItemFrameColor(ctx.S), "",
                stroke, dashed: m.ItemFramePattern == LinePattern.Dashed));
        // 按本题自己的标签测内部尺寸（baseSpec 未缩放；绘制时统一乘 scale）
        var p = MeasureOne(m, labels, ctx, baseSpec, numW);
        double uh = Math.Max(1, spec.UnitHeight);
        p.UnitH = uh;
        if (p.Stack)
        {
            p.Step = uh + Math.Max(0, m.OptionStackGapMm * scale);
            // 行高不够时先去间距，仍不够等比缩小填涂框（字符自动缩小，不裁切）
            if (p.Step * labels.Length > h)
            {
                p.Step = Math.Max(1.0, h / Math.Max(1, labels.Length));
                p.UnitH = Math.Min(p.UnitH, p.Step);
            }
        }
        else p.Step = 0;

        if (m.ShowNumber)
        {
            double nw = numW * scale;
            // 选项竖排时题号与第一个选项对齐，否则与整块题居中对齐
            double nh = p.Stack ? p.UnitH : h;
            cmds.Add(DrawCmd.Text(x0, y, nw, nh, m.NumberFor(index),
                fs, fam, txt, TextAlign.Left, VAlign.Middle));
            x0 += nw;
        }
        if (m.Display != ChoiceDisplay.Underline)
        {
            // 与考号填涂区共用同一个绘制器：样式各自可选，尺寸由生效规格决定
            if (p.Stack)
            {
                double step = p.Step;
                for (int i = 0; i < labels.Length; i++)
                {
                    double uw = p.BoxW[i] * scale;
                    FillStyleRenderer.Draw(cmds, x0, y + i * step + spec.OffsetYMm, uw, p.UnitH,
                        labels[i], spec, fam, txt, box);
                }
                // 竖排后手写横线画在整列选项的右侧
                x0 += p.MaxBoxW * scale;
            }
            else
            {
                double uy = y + (h - uh) / 2 + spec.OffsetYMm;
                for (int i = 0; i < labels.Length; i++)
                {
                    double uw = p.BoxW[i] * scale;
                    FillStyleRenderer.Draw(cmds, x0, uy, uw, uh, labels[i], spec, fam, txt, box);
                    x0 += uw + m.OptionGapMm * scale;
                }
            }
        }
        if (m.Display != ChoiceDisplay.Bracket)
        {
            double len = m.UnderlineLengthMm * scale;
            double ly = p.Stack ? y + h * 0.5 : y + h * 0.78;
            cmds.Add(DrawCmd.Line(x0, ly, Math.Min(x + itemW, x0 + len), ly, box, stroke));
        }
    }
}

public sealed class BlankFlow : ModuleFlow
{
    public BlankFlow(ModuleBase m) : base(m) { }
    protected override int Total() => ((BlankModule)Module).BlankCount;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (BlankModule)Module;
        double y = area.Y;
        double rowH = m.LineHeightMm * m.LinesPerBlank;
        string fam = ctx.Family, txt = ctx.S.TextColor, box = m.EffectiveBoxColor(ctx.S);
        double stroke = ctx.S.BoxStrokeMm;

        if (!TitleDrawn)
        {
            if (!Fits(y, TitleHeight(ctx) + rowH, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        // 一行多空：把栏宽按「每行空数」均分，每格是一个完整的空（空号 + 横线 + 可选小框）。
        // cols = 1 时 cellW = area.W、gap = 0，绘制结果与旧版逐行实现完全一致。
        int cols = Math.Clamp(m.BlanksPerRow, 1, 12);
        double gap = cols > 1 ? Math.Max(0, m.BlankGapMm) : 0;
        double CellWidth(int n) => Math.Max(1, (area.W - (n - 1) * gap) / n);
        // 栏太窄 / 每行空数太多时自动收敛列数，保证每格至少能放下空号与一小段横线
        while (cols > 1 && CellWidth(cols) < 14)
        {
            cols--;
            if (cols == 1) gap = 0;
        }
        double cellW = CellWidth(cols);
        if (cols == 1) gap = 0;
        int lines = Math.Max(1, m.LinesPerBlank);

        for (int guard = 0; Cursor < m.BlankCount; guard++)
        {
            double h = rowH;
            if (h > bottom - y)
            {
                if (!force) break;
                h = Math.Max(4, bottom - y);
            }
            int n = Math.Min(cols, m.BlankCount - Cursor);
            for (int c = 0; c < n; c++)
            {
                double cellX = area.X + c * (cellW + gap);
                string num = $"{m.NumberFor(Cursor + c)}.";
                double nw = m.ShowNumber ? TextMeasurer.Width(num, m.FontSizeMm, fam) + 1.5 : 0;
                if (cols > 1) nw = Math.Min(nw, cellW * 0.6);   // 空号不能吃掉整格
                if (m.ShowNumber)
                    cmds.Add(DrawCmd.Text(cellX, y, nw, h, num, m.FontSizeMm, fam, txt,
                        TextAlign.Left, VAlign.Middle));
                double avail = Math.Max(1, cellW - nw - 1);
                double lineW = m.BlankWidthMm > 0 ? Math.Min(m.BlankWidthMm, avail) : avail;
                for (int l = 0; l < lines; l++)
                {
                    double ly = y + (l + 0.82) * h / lines;
                    cmds.Add(DrawCmd.Line(cellX + nw, ly, cellX + nw + lineW, ly, box, stroke));
                }
                if (m.ShowFrame)
                    cmds.Add(DrawCmd.Rect(cellX, y, cellW, h, m.EffectiveItemFrameColor(ctx.S), "",
                        stroke * 0.8, dashed: m.ItemFramePattern == LinePattern.Dashed));
            }
            Cursor += n;
            y += h + ctx.S.LineGapMm;
            if (force) break;
        }
        Done = Cursor >= m.BlankCount;
        if (!Done) TitleDrawn = false;
        newY = Math.Min(y, bottom);
    }
}

public sealed class SolutionFlow : ModuleFlow
{
    /// <summary>跨页拆分时，续排部分至少要有的高度；低于它就把整题挪到下一栏 / 下一页。</summary>
    private const double SplitMinPartMm = 14.0;

    /// <summary>正在跨页续排的题目索引（-1 = 没有）。</summary>
    private int _splitQ = -1;
    /// <summary>该题已在前一栏 / 前一页画出的高度。</summary>
    private double _splitDrawn;

    public SolutionFlow(ModuleBase m) : base(m) { }
    protected override int Total() => ((SolutionModule)Module).QuestionCount;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (SolutionModule)Module;
        double y = area.Y;
        string fam = ctx.Family, txt = ctx.S.TextColor, box = m.EffectiveBoxColor(ctx.S);
        double stroke = ctx.S.BoxStrokeMm;
        string[] texts = (m.QuestionTexts ?? "").Replace("\r", "").Split('\n');

        // 续排段：不再重复模块标题，只补这一题剩下的部分
        if (_splitQ >= 0)
        {
            double full = m.HeightFor(_splitQ);
            double remain = Math.Max(0, full - _splitDrawn);
            double avail = bottom - y;
            double h = Math.Min(remain, avail);
            if (h < 8)
            {
                if (!force) { newY = y; return; }
                // 实在放不下：放弃这道题的剩余部分，避免分页死循环
                _splitQ = -1; _splitDrawn = 0; Cursor = Math.Min(Cursor + 1, m.QuestionCount);
                Done = Cursor >= m.QuestionCount;
                newY = y;
                return;
            }
            DrawUnit(area, ctx, cmds, m, _splitQ, y, h, cont: true, texts, fam, txt, box, stroke);
            y += h;
            if (remain - h <= 0.5) { _splitQ = -1; _splitDrawn = 0; Cursor++; }
            else _splitDrawn += h;
            Done = Cursor >= m.QuestionCount && _splitQ < 0;
            TitleDrawn = true;
            newY = Math.Min(y, bottom);
            return;
        }

        if (!TitleDrawn)
        {
            // 允许单题跨页时，标题后面只要能放下续排的最小高度就可以开始，不必等整题放得下
            double need = TitleHeight(ctx) +
                (m.AllowQuestionSplit ? SplitMinPartMm : m.HeightFor(Cursor));
            if (!Fits(y, need, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        for (; Cursor < m.QuestionCount; Cursor++)
        {
            double h = m.HeightFor(Cursor);
            double avail = bottom - y;
            if (h > avail + 0.001)
            {
                if (force)
                {
                    h = Math.Max(8, avail);
                }
                else if (m.AllowQuestionSplit && avail >= SplitMinPartMm)
                {
                    // 单题跨页：本栏先画能放下的部分，余下部分到下一栏 / 下一页以「N题续」继续
                    DrawUnit(area, ctx, cmds, m, Cursor, y, avail, cont: false, texts, fam, txt, box, stroke);
                    _splitQ = Cursor;
                    _splitDrawn = avail;
                    Done = false;
                    TitleDrawn = false;      // 下一段重新画模块标题（带（续））
                    newY = Math.Min(bottom, bottom);
                    return;
                }
                else break;
            }
            DrawUnit(area, ctx, cmds, m, Cursor, y, h, cont: false, texts, fam, txt, box, stroke);
            y += h + ctx.S.LineGapMm;
            if (force) { Cursor++; break; }
        }
        Done = Cursor >= m.QuestionCount;
        if (!Done) TitleDrawn = false;
        newY = Math.Min(y, bottom);
    }

    /// <summary>画一道题（或它的一段）：外框 + 题号/分数/题干 + 引导线 + 可拖拽区域。</summary>
    private void DrawUnit(RectMm area, LayoutContext ctx, List<DrawCmd> cmds, SolutionModule m,
        int idx, double y, double h, bool cont, string[] texts,
        string fam, string txt, string box, double stroke)
    {
        if (m.ShowFrame)
            cmds.Add(DrawCmd.Rect(area.X, y, area.W, h, m.EffectiveItemFrameColor(ctx.S), "",
                stroke, dashed: m.ItemFramePattern == LinePattern.Dashed));

        double pad = 2.5;
        double headH = Math.Max(m.FontSizeMm * 1.9, m.FontSizeMm * 1.35);
        // 续排段用「17题续」标注，正常段用「17.」
        string head = cont ? $"{m.NumberFor(idx)}题续" : $"{m.NumberFor(idx)}.";
        string body = !cont && idx < texts.Length ? texts[idx] : "";
        string score = cont ? "" : m.ScoreTextFor(idx);   // 只有第一段显示分数
        double numW = TextMeasurer.Width(head, m.FontSizeMm, fam) + 1.5;
        double scoreW = string.IsNullOrEmpty(score) ? 0
            : TextMeasurer.Width(score, m.EffectiveScoreFontSizeMm, fam) + 1.2;
        cmds.Add(DrawCmd.Text(area.X + pad, y, numW, headH, head,
            m.FontSizeMm, fam, txt, TextAlign.Left, VAlign.Middle, true));
        if (scoreW > 0)
            cmds.Add(DrawCmd.Text(area.X + pad + numW, y, scoreW, headH, score,
                m.EffectiveScoreFontSizeMm, fam,
                string.IsNullOrWhiteSpace(m.ScoreColor) ? txt : m.ScoreColor,
                TextAlign.Left, VAlign.Middle));
        if (!string.IsNullOrWhiteSpace(body))
        {
            double bx = area.X + pad + numW + scoreW;
            double bw = Math.Max(1, area.W - numW - scoreW - pad * 2);
            cmds.Add(DrawCmd.Text(bx, y, bw, headH, body,
                m.FontSizeMm, fam, txt, TextAlign.Left, VAlign.Middle));
        }

        // 首行横线从「题号 + 分数」之后开始：题号与分数都占行首，横线不压在分数下面。
        // 未开启分数时 scoreW = 0，起点仍是题号右侧；续排段一律通栏。
        double hw = numW + scoreW;
        if (m.ShowGuideLines)
        {
            string gl = string.IsNullOrWhiteSpace(m.GuideLineColor) ? box : m.GuideLineColor;
            double gs = m.GuideLineStrokeMm > 0 ? m.GuideLineStrokeMm : stroke * 0.6;
            bool gd = m.GuideLinePattern == LinePattern.Dashed;

            double lineTop = y + headH;
            double gap = Math.Max(2.0, m.GuideGapMm);
            double x0 = area.X + pad, x1 = area.X + area.W - pad;
            double bottomLimit = y + h - Math.Max(2.0, gs);
            bool withNumber = m.GuideLineWithNumber && !cont;   // 续排段没有题号可对齐，全部通栏
            int count;
            double firstY, step;
            if (withNumber)
            {
                firstY = lineTop + 0.6;
                double avail = Math.Max(0, bottomLimit - firstY);
                if (m.GuideLineCount > 0)
                    count = Math.Min(m.GuideLineCount, Math.Max(2, (int)Math.Floor(avail / 3.0) + 1));
                else
                    count = Math.Max(2, (int)Math.Round(avail / gap) + 1);
                step = count > 1 ? avail / (count - 1) : 0;
                if (avail < 3.0) count = 0;
            }
            else
            {
                double span = Math.Max(0, h - headH);
                if (m.GuideLineCount > 0)
                {
                    count = Math.Min(m.GuideLineCount, Math.Max(1, (int)Math.Floor(span / 3.0)));
                    step = span / (count + 1);
                    firstY = lineTop + step;
                }
                else
                {
                    double minMargin = Math.Min(gap * 0.45, span * 0.25);
                    double usable = Math.Max(0, span - 2 * minMargin);
                    count = Math.Max(1, (int)Math.Floor(usable / gap) + 1);
                    step = gap;
                    firstY = lineTop + (span - (count - 1) * step) / 2;
                }
                if (span < 3.0) count = 0;
            }
            for (int i = 0; i < count; i++)
            {
                double gy = firstY + i * step;
                double indent = Math.Min(hw, (x1 - x0) * 0.6);
                double sx = (withNumber && i == 0) ? Math.Min(x1 - 2, x0 + indent) : x0;
                cmds.Add(DrawCmd.Line(sx, gy, x1, gy, gl, gs, gd));
            }
        }

        // 每道题的答题区都是可直接在预览页点选、拖高度的区域（整组区域另由 FillWrapped 收集）
        if (ctx.CollectRegions)
            ctx.Regions.Add(new ModuleRegion
            {
                Kind = ModuleRegionKind.SolutionQuestion,
                ModuleId = m.Id, Module = m,
                Label = cont ? $"第 {m.NumberFor(idx)} 题答题区（续）" : $"第 {m.NumberFor(idx)} 题答题区",
                PageIndex = ctx.PageIndex, Column = ctx.Column,
                Bounds = new RectMm(area.X, y, area.W, h),
                BaseWidthMm = area.W, ContentX = area.X,
                MinWidthMm = Math.Max(2, area.W * 0.2), MaxWidthMm = area.W,
                MinHeightMm = 8, MaxHeightMm = 300,
                ItemIndex = idx, IsFirstSegment = !cont, CanResizeHeight = !cont
            });
    }
}

public sealed class InfoFlow : ModuleFlow
{
    public InfoFlow(ModuleBase m) : base(m) { }
    protected override int Total() => 1;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (StudentInfoModule)Module;
        double y = area.Y;
        double h = StudentInfoRenderer.Measure(m, area.W, ctx);
        if (!TitleDrawn)
        {
            if (!Fits(y, TitleHeight(ctx) + h, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        if (Cursor < 1)
        {
            double real = Math.Min(h, Math.Max(10, bottom - y));
            StudentInfoRenderer.Build(m, new RectMm(area.X, y, area.W, real), ctx, cmds);
            Cursor = 1;
            y += real + ctx.S.LineGapMm;
        }
        Done = true;
        newY = Math.Min(y, bottom);
    }
}

public sealed class CustomFlow : ModuleFlow
{
    public CustomFlow(ModuleBase m) : base(m) { }
    protected override int Total() => 1;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (CustomModule)Module;
        double y = area.Y;
        string txt = ctx.S.TextColor, box = m.EffectiveBoxColor(ctx.S);
        if (!TitleDrawn)
        {
            if (!Fits(y, TitleHeight(ctx) + m.HeightMm, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        if (Cursor < 1)
        {
            double h = Math.Min(m.HeightMm, Math.Max(8, bottom - y));
            if (m.ShowFrame) cmds.Add(DrawCmd.Rect(area.X, y, area.W, h, box, "", ctx.S.BoxStrokeMm,
                dashed: m.ItemFramePattern == LinePattern.Dashed));
            cmds.Add(DrawCmd.Text(area.X + m.PaddingMm, y + m.PaddingMm,
                Math.Max(1, area.W - m.PaddingMm * 2), Math.Max(1, h - m.PaddingMm * 2),
                m.Text, m.FontSizeMm, ctx.Family, txt, m.Align, VAlign.Top, wrap: true));
            Cursor = 1;
            y += h + ctx.S.LineGapMm;
        }
        Done = true;
        newY = Math.Min(y, bottom);
    }
}

/// <summary>作文答题区：语文方格稿纸（每行独立成框、行间留空）/ 英语等距实线横线，行数多时自动跨栏跨面续排。</summary>
public sealed class EssayFlow : ModuleFlow
{
    public EssayFlow(ModuleBase m) : base(m) { }
    protected override int Total() => ((EssayModule)Module).Rows;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (EssayModule)Module;
        double y = area.Y;
        string line = string.IsNullOrWhiteSpace(m.GridLineColor)
            ? m.EffectiveBoxColor(ctx.S) : m.GridLineColor;
        string markColor = string.IsNullOrWhiteSpace(m.WordMarkColor)
            ? ctx.S.TextColor : m.WordMarkColor;
        double stroke = m.GridLineStrokeMm;
        // 只有两种稿纸：语文方格 / 英语实线横线。旧项目的 FourLine 已在加载时迁移，这里兜底按横线处理
        bool squares = m.GridMode == EssayGridMode.Squares;
        int cols = Math.Max(1, m.Columns);
        int perMark = Math.Max(1, m.WordsPerMark);
        // 字数刻度写在「当前行下方的行间隔」里：间隔至少要放得下刻度文字（字号 ×1.35 行高 + 余量），
        // 否则数字会被上下裁切或压到相邻行的格子。关闭刻度时用户填多少就是多少（0 = 行紧贴）。
        double rowGap = Math.Max(0, m.RowGapMm);
        if (squares && m.ShowWordMarks)
            rowGap = Math.Max(rowGap, m.WordMarkSizeMm * 1.35 + 0.4);

        // 刻度文字宽度按「最大累计字数」的位数估宽（1000 字也要完整显示，不能被截断）。
        // 不再为刻度预留右侧独立竖列：格子可以用满整栏，刻度放在该行下方间隔带的右端。
        int maxWords = Math.Max(1, m.Rows * cols);
        string markProbe = new string('0', Math.Max(3, maxWords.ToString().Length));
        double markW = m.ShowWordMarks && squares
            ? Math.Max(6, TextMeasurer.Width(markProbe, m.WordMarkSizeMm, ctx.Family) + 1.5)
            : 0;
        double maxGridW = Math.Max(1, area.W - 0.5);
        double cell = squares
            ? Math.Max(1, Math.Min(Math.Max(2, m.CellSizeMm), maxGridW / cols))
            : Math.Max(3, m.CellSizeMm);
        double gridW = squares ? cols * cell : area.W;
        double rowH = cell;
        // 行间隔：语文格纸每行之间留空白；英语横线模式下即相邻两条实线的额外间距
        double step = rowH + rowGap;

        if (!TitleDrawn)
        {
            // 续排页（第 2 面、下一栏）不画任何标题 / 标识：作文区只在最开始出现一次标题
            if (Cursor == 0)
            {
                if (!Fits(y, TitleHeight(ctx) + step, bottom, force)) { newY = y; return; }
                double h = TitleHeight(ctx);
                if (h > 0)
                {
                    cmds.Add(DrawCmd.Text(area.X, y, area.W, h, Module.Title,
                        Module.TitleSizeMm, ctx.Family, ctx.S.TextColor,
                        TextAlign.Left, VAlign.Middle, true));
                    y += h + ctx.S.LineGapMm;
                }
            }
            TitleDrawn = true;
        }

        double firstY = y;
        int drawn = 0;
        // 英语作文：段落顶部一条实线，之后每行补一条等距实线，全部为实线（没有虚线 / 四线三格）
        if (!squares)
            cmds.Add(DrawCmd.Line(area.X, firstY, area.X + area.W, firstY, line, stroke));

        while (Cursor < m.Rows)
        {
            double ry = y;
            // 行间隔里要放字数刻度，整行（格子 + 间隔）必须一起放得下，刻度才不会画到区域外
            if (!Fits(ry, step, bottom, force)) break;
            double h = Math.Min(rowH, Math.Max(1, bottom - ry));

            if (squares)
            {
                // 每行各自成封闭框：上下横线 + 本行独立竖线；行与行之间不共用线，中间留 RowGapMm 空白
                cmds.Add(DrawCmd.Line(area.X, ry, area.X + gridW, ry, line, stroke));
                cmds.Add(DrawCmd.Line(area.X, ry + h, area.X + gridW, ry + h, line, stroke));
                for (int c = 0; c <= cols; c++)
                {
                    double vx = area.X + c * cell;
                    cmds.Add(DrawCmd.Line(vx, ry, vx, ry + h, line, stroke));
                }
                if (m.ShowWordMarks)
                {
                    int words = (Cursor + 1) * cols;
                    if (words % perMark == 0)
                    {
                        // 刻度放在「当前行下方的行间隔」里：从本行底线开始，占满整条间隔带，
                        // 横向右对齐贴着格子区右端，不再另占格子右侧的竖列。
                        double gapH = Math.Min(rowGap, Math.Max(0, bottom - (ry + h)));
                        double boxW = Math.Min(markW, Math.Max(1, gridW - 1));
                        if (gapH >= m.WordMarkSizeMm * 1.35 && ry + h + gapH <= bottom + 0.001)
                            cmds.Add(DrawCmd.Text(area.X + gridW - boxW - 0.5, ry + h,
                                boxW, gapH, words.ToString(),
                                m.WordMarkSizeMm, ctx.Family, markColor,
                                TextAlign.Right, VAlign.Middle));
                    }
                }
            }
            else
            {
                cmds.Add(DrawCmd.Line(area.X, Math.Min(bottom, ry + step),
                    area.X + area.W, Math.Min(bottom, ry + step), line, stroke));
            }

            Cursor++;
            drawn++;
            y += step;
            if (force) break;
        }

        Done = Cursor >= m.Rows;
        if (!Done) TitleDrawn = false;
        newY = Math.Min(y, bottom);
    }
}

/// <summary>非作答区：背景色矩形 + 可选边框（实线 / 虚线）+ 可选斜线阴影 + 文字。</summary>
public sealed class NonAnswerFlow : ModuleFlow
{
    public NonAnswerFlow(ModuleBase m) : base(m) { }
    protected override int Total() => 1;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (NonAnswerModule)Module;
        double y = area.Y;
        string border = string.IsNullOrWhiteSpace(m.BorderColor)
            ? m.EffectiveBoxColor(ctx.S) : m.BorderColor;
        string text = string.IsNullOrWhiteSpace(m.TextColor) ? ctx.S.TextColor : m.TextColor;
        string fill = string.IsNullOrWhiteSpace(m.BackgroundColor) ? "" : m.BackgroundColor;
        bool showBorder = m.BorderStyle != BorderPattern.None && m.BorderStrokeMm > 0;

        if (!TitleDrawn)
        {
            if (!Fits(y, TitleHeight(ctx) + m.HeightMm, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        if (Cursor < 1)
        {
            double h = Math.Min(m.HeightMm, Math.Max(8, bottom - y));
            // 背景与边框是同一个矩形：无边框时把描边设空，仅保留填充
            if (!string.IsNullOrWhiteSpace(fill) || showBorder)
                cmds.Add(DrawCmd.Rect(area.X, y, area.W, h,
                    showBorder ? border : "", fill,
                    showBorder ? m.BorderStrokeMm : 0,
                    dashed: showBorder && m.BorderStyle == BorderPattern.Dashed));
            if (m.Hatch)
                AddHatch(cmds, area.X, y, area.W, h, border,
                    Math.Max(2, m.HatchGapMm), Math.Max(0.08, m.BorderStrokeMm * 0.7));
            double pad = m.PaddingMm;
            cmds.Add(DrawCmd.Text(area.X + pad, y + pad,
                Math.Max(1, area.W - pad * 2), Math.Max(1, h - pad * 2),
                m.Text, m.FontSizeMm, ctx.Family, text,
                m.Align, m.TextVAlign, wrap: true));
            Cursor = 1;
            y += h + ctx.S.LineGapMm;
        }
        Done = true;
        newY = Math.Min(y, bottom);
    }

    /// <summary>在矩形内画 45° 平行斜线（y = x + c），端点按矩形四边裁剪，绝不画出区域。</summary>
    private static void AddHatch(List<DrawCmd> cmds, double X, double Y,
        double W, double H, string color, double gap, double stroke)
    {
        double cMin = Y - (X + W), cMax = Y + H - X;
        for (double c = cMin + gap; c < cMax - 0.001; c += gap)
        {
            double x1 = X, y1 = X + c;
            if (y1 < Y) { y1 = Y; x1 = Y - c; }
            double x2 = X + W, y2 = X + W + c;
            if (y2 > Y + H) { y2 = Y + H; x2 = Y + H - c; }
            if (x1 <= x2 && x1 >= X - 0.01 && x2 <= X + W + 0.01 &&
                y1 >= Y - 0.01 && y2 <= Y + H + 0.01)
                cmds.Add(DrawCmd.Line(x1, y1, x2, y2, color, stroke));
        }
    }
}

public static class ImageUtil
{
    public static bool TrySize(string path, out double pxW, out double pxH)
    {
        pxW = pxH = 0;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(path));
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            pxW = bi.PixelWidth; pxH = bi.PixelHeight;
            return pxW > 0 && pxH > 0;
        }
        catch { return false; }
    }
}

public sealed class ImageFlow : ModuleFlow
{
    public ImageFlow(ModuleBase m) : base(m) { }
    protected override int Total() => 1;

    public override void Fill(RectMm area, double bottom, bool force,
        LayoutContext ctx, List<DrawCmd> cmds, out double newY)
    {
        var m = (ImageModule)Module;
        double y = area.Y;
        string path = ctx.Project.ResolveImagePath(m.ImagePath);
        bool ok = ImageUtil.TrySize(path, out double pw, out double ph);
        double w = area.W * Math.Clamp(m.WidthPercent, 5, 100) / 100.0;
        double h = ok ? w * ph / pw : 18;
        double captionH = string.IsNullOrWhiteSpace(m.Caption) ? 0 : m.CaptionSizeMm * 1.7;
        double total = h + captionH;
        if (!TitleDrawn)
        {
            if (!Fits(y, TitleHeight(ctx) + total, bottom, force)) { newY = y; return; }
            EmitTitle(area, ref y, ctx, cmds);
        }
        if (Cursor < 1)
        {
            double real = Math.Min(total, Math.Max(8, bottom - y));
            double realImgH = Math.Max(2, real - captionH);
            double x = m.Align switch
            {
                ImageAlign.Center => area.X + (area.W - w) / 2,
                ImageAlign.Right => area.X + area.W - w,
                _ => area.X
            };
            if (ok)
                cmds.Add(DrawCmd.Image(x, y, w, realImgH, path));
            else
            {
                cmds.Add(DrawCmd.Rect(x, y, w, realImgH, m.EffectiveBoxColor(ctx.S), "",
                    ctx.S.BoxStrokeMm, dashed: true));
                cmds.Add(DrawCmd.Text(x, y, w, realImgH, "（未选择图片 / 图片文件缺失）",
                    m.CaptionSizeMm, ctx.Family, ctx.S.TextColor, TextAlign.Center, VAlign.Middle));
            }
            if (captionH > 0)
                cmds.Add(DrawCmd.Text(x, y + realImgH, w, captionH, m.Caption,
                    m.CaptionSizeMm, ctx.Family, ctx.S.TextColor, TextAlign.Center, VAlign.Middle));
            Cursor = 1;
            y += real + ctx.S.LineGapMm;
        }
        Done = true;
        newY = Math.Min(y, bottom);
    }
}
