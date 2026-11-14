namespace AnswerSheetStudio.Models;

public abstract class ModuleBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Prop("模块名称", Group = "通用", Order = 0)]
    public string Name { get; set; } = "模块";

    [Prop("模块标题", Group = "通用", Order = 1, Hint = "留空则不占标题行")]
    public string Title { get; set; } = "";

    [Prop("显示标题", Group = "通用", Order = 2)]
    public bool ShowTitle { get; set; } = true;

    [Prop("标题字号 (mm)", Group = "通用", Order = 3, Min = 2, Max = 20)]
    public double TitleSizeMm { get; set; } = 5.0;

    [Prop("框线颜色", Group = "通用", Order = 4, IsColor = true, Hint = "本模块所有框线（填涂框、手写线、导写线等）的默认颜色；留空 = 使用页面设置的框线颜色")]
    public string BoxColor { get; set; } = "";

    [Prop("大外框颜色", Group = "通用", Order = 5, IsColor = true, Hint = "答题区大外框（整组外框）的颜色；留空 = 跟随上面的「框线颜色」")]
    public string GroupFrameColor { get; set; } = "";

    [Prop("小题框颜色", Group = "通用", Order = 6, IsColor = true, Hint = "每题 / 每空的答题框颜色（如解答题每题框、填空题每空框、考号网格）；留空 = 跟随上面的「框线颜色」")]
    public string ItemFrameColor { get; set; } = "";

    [Prop("答题区大外框", Group = "通用", Order = 7, Hint = "给整个模块的作答区域画一个大外框（跨栏续排时每栏各自成框）。选择题 / 填空题 / 解答题 / 自定义编辑区都能单独开关；填空题 / 解答题 / 自定义编辑区默认开启，选择题默认关闭")]
    public bool ShowGroupFrame { get; set; } = false;

    [Prop("大题框线型", Group = "通用", Order = 8, Hint = "整组答题区大外框 / 考生信息栏外框的线型：实线或虚线。与「大外框颜色」配套使用；默认实线")]
    public LinePattern GroupFramePattern { get; set; } = LinePattern.Solid;

    [Prop("小题框线型", Group = "通用", Order = 9, Hint = "每题 / 每空答题框、考号网格、自定义编辑区外框的线型：实线或虚线。与「小题框颜色」配套使用；默认实线")]
    public LinePattern ItemFramePattern { get; set; } = LinePattern.Solid;

    // ---- 区域大小：所有模块通用，填空题 / 解答题 / 自定义编辑区等主观题区域最常用 ----
    [Prop("区域宽度 (%)", Group = "区域大小", Order = 0, Min = 20, Max = 100, Hint = "相对所在栏宽度；100 = 占满整栏。调窄后内容重新排版，绝不越出栏")]
    public double RegionWidthPercent { get; set; } = 100;

    [Prop("水平对齐", Group = "区域大小", Order = 1, Hint = "区域变窄后在栏内的位置")]
    public RegionAlign RegionAlign { get; set; } = RegionAlign.Left;

    [Prop("水平偏移 (mm)", Group = "区域大小", Order = 2, Min = 0, Max = 400, Hint = "0 = 按上方「水平对齐」自动放置；在预览页里直接拖动区域右侧时会自动写入，用来固定左边界")]
    public double RegionOffsetMm { get; set; } = 0;

    [Prop("指定区域高度 (mm)", Group = "区域大小", Order = 3, Min = 0, Max = 400, Hint = "0 = 按内容自动。设定后整组外框固定为该高度：内容在上、下方留白供作答；内容超出时按比例压缩。跨栏续排只对第一段生效")]
    public double RegionHeightMm { get; set; } = 0;

    [Prop("最小区域高度 (mm)", Group = "区域大小", Order = 4, Min = 0, Max = 400, Hint = "内容不足时整组外框至少这么高（留白供作答）；0 = 不设下限")]
    public double RegionMinHeightMm { get; set; } = 0;

    public abstract string TypeKey { get; }
    public abstract string TypeName { get; }
    public abstract ModuleBase Clone();

    protected void CopyBaseTo(ModuleBase other)
    {
        other.Id = Guid.NewGuid().ToString("N");
        other.Name = Name;
        other.Title = Title;
        other.ShowTitle = ShowTitle;
        other.TitleSizeMm = TitleSizeMm;
        other.BoxColor = BoxColor;
        other.GroupFrameColor = GroupFrameColor;
        other.ItemFrameColor = ItemFrameColor;
        other.ShowGroupFrame = ShowGroupFrame;
        other.GroupFramePattern = GroupFramePattern;
        other.ItemFramePattern = ItemFramePattern;
        other.RegionWidthPercent = RegionWidthPercent;
        other.RegionAlign = RegionAlign;
        other.RegionOffsetMm = RegionOffsetMm;
        other.RegionHeightMm = RegionHeightMm;
        other.RegionMinHeightMm = RegionMinHeightMm;
    }

    public string EffectiveBoxColor(PageSettings s) =>
        string.IsNullOrWhiteSpace(BoxColor) ? s.BoxColor : BoxColor;

    /// <summary>答题区大外框颜色：大外框专用色 → 模块框线色 → 页面框线色。</summary>
    public string EffectiveGroupFrameColor(PageSettings s) =>
        string.IsNullOrWhiteSpace(GroupFrameColor) ? EffectiveBoxColor(s) : GroupFrameColor;

    /// <summary>小题框（每题 / 每空 / 考号网格）颜色：小题专用色 → 模块框线色 → 页面框线色。</summary>
    public string EffectiveItemFrameColor(PageSettings s) =>
        string.IsNullOrWhiteSpace(ItemFrameColor) ? EffectiveBoxColor(s) : ItemFrameColor;
}

/// <summary>选择题：单选 / 多选 / 判断，支持 [A] 填涂与横线手写。</summary>
public sealed class ChoiceModule : ModuleBase
{
    public override string TypeKey => "choice";
    public override string TypeName => "选择题";

    [Prop("题型", Group = "选择题", Order = 0)]
    public ChoiceKind Kind { get; set; } = ChoiceKind.Single;

    [Prop("作答方式", Group = "选择题", Order = 1)]
    public ChoiceDisplay Display { get; set; } = ChoiceDisplay.Bracket;

    [Prop("题目排列", Group = "选择题排列", Order = 0, Hint = "横排 = 每行按栏宽尽量多放几道题（当前默认）；竖排 = 每道题独占一个区块（选项上下堆叠），可在下方设多列并排")]
    public ChoiceLayout QuestionLayout { get; set; } = ChoiceLayout.Horizontal;

    [Prop("选项排列", Group = "选择题排列", Order = 1, Hint = "横排 = A B C D 左右并排；竖排 = 每个选项单独占一行，上下堆叠（便于阅卷机逐格识别）")]
    public OptionLayout OptionLayout { get; set; } = OptionLayout.Horizontal;

    [Prop("选项竖排间距 (mm)", Group = "选择题排列", Order = 2, Min = 0, Max = 20, Hint = "选项竖排时上下两个填涂框之间的间距")]
    public double OptionStackGapMm { get; set; } = 1.2;

    [Prop("竖排列数", Group = "选择题排列", Order = 3, Min = 1, Max = 12, Hint = "仅当「题目排列 = 竖排」时生效：一个横行内并排放几道题。横排时每行题数由栏宽自动决定，此值不起作用；实际列数还会受栏宽上限约束")]
    public int VerticalColumns { get; set; } = 1;

    [Prop("填涂框样式", Group = "选择题", Order = 2, Hint = "方框填涂区 = 标准阅卷机识别框；字面中括号 [A] = 用线段绘制。选择题与考号区可以各自选择不同样式")]
    public BracketBoxStyle BracketStyle { get; set; } = BracketBoxStyle.Literal;

    [Prop("填涂尺寸来源", Group = "选择题", Order = 3, Hint = "默认跟随「全卷填涂样式」模板，与考号区共用一套尺寸；切到「单独设定」后，下方「中括号样式」组与本模块的「填涂框边长」「字母字号」才生效。字体始终在「选择题字符模板」里设")]
    public FillSizeSource FillSizeSource { get; set; } = FillSizeSource.FollowTemplate;

    [Prop("题目数量", Group = "选择题", Order = 4, Min = 1, Max = 2000)]
    public int QuestionCount { get; set; } = 40;

    [Prop("起始题号", Group = "选择题", Order = 5, Min = 0, Max = 9999, Hint = "只作用于未单独填写「小题号」的题；填了「小题号」的题以各自填写的为准")]
    public int StartNumber { get; set; } = 1;

    [Prop("小题号 (逐题)", Group = "选择题", Order = 6, Hint = "按题号顺序逐行填写每题的小题号；留空 = 用默认「起始题号 + 序号」。可填任意内容（如 1-1、5、2…），不必连续也不必顺序，方便把答题卡对应到卷面不连续的题")]
    public List<string> QuestionNumbers { get; set; } = new();

    [Prop("选项数量", Group = "选择题", Order = 7, Min = 1, Max = 200, Hint = "超过 26 个自动用 AA / AB … 续排")]
    public int OptionCount { get; set; } = 4;

    [Prop("每题选项数 (逐题)", Group = "选择题", Order = 8, Hint = "按题号顺序逐行填写每题的选项个数；留空 = 使用上面的「选项数量」。例如某几题是 5 选 1、其余 4 选 1 时可分别设定。判断题的逐题选项数不生效")]
    public List<int> OptionCounts { get; set; } = new();

    [Prop("自定义标签", Group = "选择题", Order = 9, Hint = "逗号分隔；留空自动生成 A…Z, AA…")]
    public string CustomLabels { get; set; } = "";

    [Prop("判断题标签", Group = "选择题", Order = 10, Hint = "判断题专用，逗号分隔")]
    public string JudgementLabels { get; set; } = "√,×";

    [Prop("显示题号", Group = "选择题", Order = 11)]
    public bool ShowNumber { get; set; } = true;

    [Prop("题号后缀", Group = "选择题", Order = 12)]
    public string NumberSuffix { get; set; } = ".";

    [Prop("选项框线型", Group = "选择题", Order = 13, Hint = "每题 [A] 中括号 / 方框填涂框的线型：实线或虚线。与考号区填涂框各自独立；默认实线")]
    public LinePattern OptionFramePattern { get; set; } = LinePattern.Solid;

    [Prop("显示每题题框", Group = "选择题", Order = 14, Hint = "给每道选择题单独画一个小题框（包住题号 + 选项）。颜色 / 线型用「通用」组的「小题框颜色」「小题框线型」；默认关闭")]
    public bool ShowQuestionFrame { get; set; } = false;

    [Prop("行高 (mm)", Group = "版式", Order = 0, Min = 3, Max = 40)]
    public double RowHeightMm { get; set; } = 8.5;

    [Prop("列间距 (mm)", Group = "版式", Order = 1, Min = 0, Max = 60)]
    public double ColumnGapMm { get; set; } = 6.0;

    [Prop("填涂框边长 (mm)", Group = "版式", Order = 2, Min = 2, Max = 20)]
    public double BracketBoxMm { get; set; } = 5.6;

    [Prop("选项间距 (mm)", Group = "版式", Order = 3, Min = 0, Max = 20)]
    public double OptionGapMm { get; set; } = 1.6;

    [Prop("手写横线长 (mm)", Group = "版式", Order = 4, Min = 5, Max = 250)]
    public double UnderlineLengthMm { get; set; } = 34;

    [Prop("内容字号 (mm)", Group = "版式", Order = 5, Min = 1.5, Max = 15, Hint = "题号字号；[A] 里的字母字号在「全卷填涂样式 → 选择题字符模板」里设定，本模块切到「单独设定」时改用下方「中括号样式」的数值")]
    public double FontSizeMm { get; set; } = 3.6;

    // ---- 单独设定时的精细样式：位置、高度、大小、字符大小均可独立调整 ----
    // 尺寸来源 = 跟随全卷模板时，这些数值被模板覆盖，但会保留在项目中，切回「单独设定」即可恢复。
    [Prop("字母/数字字号 (mm)", Group = "中括号样式", Order = 0, Min = 1, Max = 15, Hint = "括号内 A / B / AA 等字符的大小，与题号字号互不影响；仅「单独设定」时生效（跟随模板时用「选择题字符模板」的字号）。字体在「全卷填涂样式」的两套字符模板里设。字号大于括号高度时默认自动缩小，保证字符完整显示")]
    public double LiteralCharSizeMm { get; set; } = 3.6;

    [Prop("括号高度 (mm)", Group = "中括号样式", Order = 1, Min = 1, Max = 25, Hint = "[ 与 ] 竖线的总高度；比字符字号矮时字符会自动缩小，不会被裁掉")]
    public double LiteralBracketHeightMm { get; set; } = 5.4;

    [Prop("括号臂长 (mm)", Group = "中括号样式", Order = 2, Min = 0.2, Max = 10, Hint = "[ 与 ] 上下两横的长度，决定括号整体的横向大小")]
    public double LiteralBracketArmMm { get; set; } = 1.4;

    [Prop("括号与字符间距 (mm)", Group = "中括号样式", Order = 3, Min = 0, Max = 15, Hint = "[ 到字母、字母到 ] 的水平距离；越大括号离字符越远")]
    public double LiteralBracketGapMm { get; set; } = 0.9;

    [Prop("括号线宽 (mm)", Group = "中括号样式", Order = 4, Min = 0.05, Max = 2)]
    public double LiteralBracketStrokeMm { get; set; } = 0.3;

    [Prop("括号垂直偏移 (mm)", Group = "中括号样式", Order = 5, Min = -6, Max = 6, Hint = "正数向下微调括号，用于与字符视觉居中")]
    public double LiteralBracketOffsetYMm { get; set; } = 0;

    [Prop("左右间距分别设置", Group = "中括号样式", Order = 6, Hint = "开启后，右括号到字符的距离使用单独的数值")]
    public bool LiteralUseSeparateRightGap { get; set; } = false;

    [Prop("右括号与字符间距 (mm)", Group = "中括号样式", Order = 7, Min = 0, Max = 15, Hint = "仅当上一项开启时生效")]
    public double LiteralBracketRightGapMm { get; set; } = 0.9;

    public override ModuleBase Clone()
    {
        var m = new ChoiceModule
        {
            Kind = Kind, Display = Display, BracketStyle = BracketStyle,
            QuestionLayout = QuestionLayout, OptionLayout = OptionLayout,
            OptionStackGapMm = OptionStackGapMm, VerticalColumns = VerticalColumns,
            FillSizeSource = FillSizeSource, QuestionCount = QuestionCount,
            StartNumber = StartNumber,
            QuestionNumbers = QuestionNumbers == null ? new List<string>() : new List<string>(QuestionNumbers),
            OptionCounts = OptionCounts == null ? new List<int>() : new List<int>(OptionCounts),
            OptionCount = OptionCount,
            CustomLabels = CustomLabels, JudgementLabels = JudgementLabels,
            ShowNumber = ShowNumber, NumberSuffix = NumberSuffix,
            OptionFramePattern = OptionFramePattern, ShowQuestionFrame = ShowQuestionFrame,
            RowHeightMm = RowHeightMm, ColumnGapMm = ColumnGapMm,
            BracketBoxMm = BracketBoxMm, OptionGapMm = OptionGapMm,
            UnderlineLengthMm = UnderlineLengthMm, FontSizeMm = FontSizeMm,
            LiteralCharSizeMm = LiteralCharSizeMm,
            LiteralBracketHeightMm = LiteralBracketHeightMm,
            LiteralBracketArmMm = LiteralBracketArmMm,
            LiteralBracketGapMm = LiteralBracketGapMm,
            LiteralBracketStrokeMm = LiteralBracketStrokeMm,
            LiteralBracketOffsetYMm = LiteralBracketOffsetYMm,
            LiteralUseSeparateRightGap = LiteralUseSeparateRightGap,
            LiteralBracketRightGapMm = LiteralBracketRightGapMm
        };
        CopyBaseTo(m);
        return m;
    }

    /// <summary>Excel 式标签序列：A…Z, AA, AB … ZZ, AAA …</summary>
    public static string AutoLabel(int index)
    {
        int n = index + 1;
        var s = "";
        while (n > 0)
        {
            n--;
            s = (char)('A' + n % 26) + s;
            n /= 26;
        }
        return s;
    }

    public string[] Labels()
    {
        if (Kind == ChoiceKind.Judgement && !string.IsNullOrWhiteSpace(JudgementLabels))
            return JudgementLabels.Split(new[] { ',', '，', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => x.Trim()).ToArray();
        if (!string.IsNullOrWhiteSpace(CustomLabels))
            return CustomLabels.Split(new[] { ',', '，', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(x => x.Trim()).ToArray();
        return Enumerable.Range(0, Math.Max(1, OptionCount)).Select(AutoLabel).ToArray();
    }

    /// <summary>第 index 题（0 基）实际使用的选项标签数组（A…Z, AA…）；会按该题单独设定的选项个数生成，缺省则沿用「选项数量」。</summary>
    public string[] LabelsFor(int index)
    {
        if (Kind == ChoiceKind.Judgement && !string.IsNullOrWhiteSpace(JudgementLabels))
            return JudgementLabels.Split(new[] { ',', '，', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => x.Trim()).ToArray();
        int n = OptionCountFor(index);
        if (n <= 0) n = OptionCount;
        n = Math.Clamp(n, 1, 200);
        if (!string.IsNullOrWhiteSpace(CustomLabels))
        {
            var arr = CustomLabels.Split(new[] { ',', '，', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(x => x.Trim()).ToArray();
            if (arr.Length >= n) return arr.Take(n).ToArray();
            var more = Enumerable.Range(arr.Length, n - arr.Length).Select(AutoLabel).ToArray();
            return arr.Concat(more).ToArray();
        }
        return Enumerable.Range(0, n).Select(AutoLabel).ToArray();
    }

    /// <summary>第 index 题（0 基）单独设定的选项个数；0 = 未设定，沿用「选项数量」。</summary>
    public int CustomOptionCount(int index)
    {
        var list = OptionCounts;
        if (list != null && index >= 0 && index < list.Count) return list[index];
        return 0;
    }

    public bool HasCustomOptionCount(int index) => CustomOptionCount(index) > 0;

    /// <summary>第 index 题（0 基）实际生效的选项个数：单独设定过用设定值，否则「选项数量」。</summary>
    public int OptionCountFor(int index)
    {
        int v = CustomOptionCount(index);
        return v > 0 ? v : Math.Clamp(OptionCount, 1, 200);
    }

    public void SetOptionCount(int index, int value)
    {
        if (index < 0) return;
        OptionCounts ??= new List<int>();
        while (OptionCounts.Count <= index) OptionCounts.Add(0);
        OptionCounts[index] = Math.Clamp(value, 0, 200);
    }

    public void ClearOptionCount(int index)
    {
        if (OptionCounts == null || index < 0 || index >= OptionCounts.Count) return;
        OptionCounts[index] = 0;
    }

    public void ClearAllOptionCounts() => OptionCounts?.Clear();

    /// <summary>第 index 题（0 基）已单独填写的小题号原文（含空白）；未填返回空串。</summary>
    public string CustomNumber(int index)
    {
        var list = QuestionNumbers;
        if (list != null && index >= 0 && index < list.Count) return list[index] ?? "";
        return "";
    }

    public bool HasCustomNumber(int index) => !string.IsNullOrWhiteSpace(CustomNumber(index));

    /// <summary>第 index 题（0 基）实际显示的题号文字：填了「小题号」用填写值，否则用「起始题号 + 序号 + 后缀」。</summary>
    public string NumberFor(int index)
    {
        var s = CustomNumber(index);
        if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        return $"{StartNumber + index}{NumberSuffix}";
    }

    /// <summary>写入单题小题号（页面暂未提供拖拽，仅属性面板逐题填写）；value 为空表示恢复默认。</summary>
    public void SetQuestionNumber(int index, string value)
    {
        if (index < 0) return;
        QuestionNumbers ??= new List<string>();
        while (QuestionNumbers.Count <= index) QuestionNumbers.Add("");
        QuestionNumbers[index] = (value ?? "").Trim();
    }

    public void ClearQuestionNumber(int index)
    {
        if (QuestionNumbers == null || index < 0 || index >= QuestionNumbers.Count) return;
        QuestionNumbers[index] = "";
    }

    public void ClearAllQuestionNumbers() => QuestionNumbers?.Clear();
}

/// <summary>填空题。</summary>
public sealed class BlankModule : ModuleBase
{
    public BlankModule() { ShowGroupFrame = true; }

    public override string TypeKey => "blank";
    public override string TypeName => "填空题";

    [Prop("空格数量", Group = "填空题", Order = 0, Min = 1, Max = 500)]
    public int BlankCount { get; set; } = 10;

    [Prop("起始空号", Group = "填空题", Order = 1, Min = 0, Max = 9999, Hint = "只作用于未单独填写「小题号」的空；填了「小题号」的空以各自填写的为准")]
    public int StartNumber { get; set; } = 1;

    [Prop("小题号 (逐题)", Group = "填空题", Order = 2, Hint = "按空号顺序逐行填写每个空的小题号（填空也称空号）；留空 = 用默认「起始空号 + 序号」。可填任意内容，不必连续也不必顺序")]
    public List<string> QuestionNumbers { get; set; } = new();

    [Prop("每空行数", Group = "填空题", Order = 3, Min = 1, Max = 10)]
    public int LinesPerBlank { get; set; } = 1;

    [Prop("行高 (mm)", Group = "填空题", Order = 4, Min = 3, Max = 40)]
    public double LineHeightMm { get; set; } = 9.0;

    [Prop("每行空数", Group = "填空题", Order = 5, Min = 1, Max = 12,
        Hint = "1 = 每个空独占一行（默认）；填 N = 同一行横向排 N 个空，各自带空号与横线，按栏宽均分")]
    public int BlanksPerRow { get; set; } = 1;

    [Prop("同排空间距 (mm)", Group = "填空题", Order = 6, Min = 0, Max = 40,
        Hint = "「每行空数」大于 1 时，相邻两个空之间的水平留白")]
    public double BlankGapMm { get; set; } = 5.0;

    [Prop("横线宽度 (mm)", Group = "填空题", Order = 7, Min = 0, Max = 300,
        Hint = "0 = 占满本空的可用宽度；一行多空时是「每个空」的横线宽度，超出会自动收敛")]
    public double BlankWidthMm { get; set; } = 0;

    [Prop("画外框", Group = "填空题", Order = 8, Hint = "一行多空时每个空各自成框")]
    public bool ShowFrame { get; set; }

    [Prop("显示空号", Group = "填空题", Order = 9)]
    public bool ShowNumber { get; set; } = true;

    [Prop("题号前缀", Group = "填空题", Order = 10)]
    public string NumberPrefix { get; set; } = "";

    [Prop("内容字号 (mm)", Group = "填空题", Order = 11, Min = 1.5, Max = 15)]
    public double FontSizeMm { get; set; } = 3.6;

    /// <summary>第 index 空（0 基）已单独填写的小题号原文（含空白）；未填返回空串。</summary>
    public string CustomNumber(int index)
    {
        var list = QuestionNumbers;
        if (list != null && index >= 0 && index < list.Count) return list[index] ?? "";
        return "";
    }

    public bool HasCustomNumber(int index) => !string.IsNullOrWhiteSpace(CustomNumber(index));

    /// <summary>第 index 空（0 基）实际显示的空号文字（不含后缀点）：填了「小题号」用填写值，否则用「前缀 + 起始空号 + 序号」。</summary>
    public string NumberFor(int index)
    {
        var s = CustomNumber(index);
        if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        return $"{NumberPrefix}{StartNumber + index}";
    }

    public void SetQuestionNumber(int index, string value)
    {
        if (index < 0) return;
        QuestionNumbers ??= new List<string>();
        while (QuestionNumbers.Count <= index) QuestionNumbers.Add("");
        QuestionNumbers[index] = (value ?? "").Trim();
    }

    public void ClearQuestionNumber(int index)
    {
        if (QuestionNumbers == null || index < 0 || index >= QuestionNumbers.Count) return;
        QuestionNumbers[index] = "";
    }

    public void ClearAllQuestionNumbers() => QuestionNumbers?.Clear();

    public override ModuleBase Clone()
    {
        var m = new BlankModule
        {
            BlankCount = BlankCount, StartNumber = StartNumber,
            QuestionNumbers = QuestionNumbers == null ? new List<string>() : new List<string>(QuestionNumbers),
            LinesPerBlank = LinesPerBlank,
            LineHeightMm = LineHeightMm, BlanksPerRow = BlanksPerRow, BlankGapMm = BlankGapMm,
            BlankWidthMm = BlankWidthMm, ShowFrame = ShowFrame,
            ShowNumber = ShowNumber, NumberPrefix = NumberPrefix, FontSizeMm = FontSizeMm
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>解答题。</summary>
public sealed class SolutionModule : ModuleBase
{
    public SolutionModule() { ShowGroupFrame = true; }

    public override string TypeKey => "solution";
    public override string TypeName => "解答题";

    [Prop("题目数量", Group = "解答题", Order = 0, Min = 1, Max = 200)]
    public int QuestionCount { get; set; } = 4;

    [Prop("起始题号", Group = "解答题", Order = 1, Min = 0, Max = 9999, Hint = "只作用于未单独填写「小题号」的题；填了「小题号」的题以各自填写的为准")]
    public int StartNumber { get; set; } = 1;

    [Prop("小题号 (逐题)", Group = "解答题", Order = 2, Hint = "按题号顺序逐行填写每题的小题号；留空 = 用默认「起始题号 + 序号」。可填任意内容（如 1-1、5、2…），不必连续也不必顺序，方便把答题卡对应到卷面不连续的题")]
    public List<string> QuestionNumbers { get; set; } = new();

    [Prop("每题答题区高 (mm)", Group = "解答题", Order = 3, Min = 10, Max = 260,
        Hint = "所有题的默认高度；下面「每题高度」里单独设定过的题以各自数值为准")]
    public double AreaHeightMm { get; set; } = 48;

    /// <summary>逐题高度（mm，0 = 跟随上面的统一高度）。可在属性面板逐题填写，也可在预览页直接拖每题下边手柄。</summary>
    [Prop("每题高度 (mm)", Group = "解答题", Order = 4,
        Hint = "逐题设定答题区高度，互不影响；填 0 = 恢复默认值。也可以直接在预览页点中某一题，拖下方蓝色手柄调整")]
    public List<double> QuestionHeightsMm { get; set; } = new();

    [Prop("显示外框", Group = "解答题", Order = 5)]
    public bool ShowFrame { get; set; } = true;

    [Prop("显示横向导写线", Group = "解答题", Order = 6)]
    public bool ShowGuideLines { get; set; } = true;

    [Prop("导写线间距 (mm)", Group = "解答题", Order = 7, Min = 3, Max = 40,
        Hint = "自动排线时相邻横线的目标距离；实际间距会按答题框高度微调，使横线均匀排满到框底")]
    public double GuideGapMm { get; set; } = 9;

    [Prop("引导线条数", Group = "解答题", Order = 8, Min = 0, Max = 30,
        Hint = "0 = 按上面间距自动排满；填 N = 每题固定 N 条横线，从题号所在行均匀分布到框底")]
    public int GuideLineCount { get; set; } = 0;

    [Prop("首行横线与题号同行", Group = "解答题", Order = 9,
        Hint = "开启（默认）：第一条横线就是题号所在行的书写底线、从题号右侧开始；关闭：横线整体排在题号下方另起一行，全部通栏且上下均匀留白")]
    public bool GuideLineWithNumber { get; set; } = true;

    [Prop("引导线样式", Group = "解答题", Order = 10, Hint = "仅当上面「显示横向导写线」开启时生效：可改成实线或虚线")]
    public LinePattern GuideLinePattern { get; set; } = LinePattern.Dashed;

    [Prop("引导线颜色", Group = "解答题", Order = 11, IsColor = true, Hint = "留空 = 跟随模块「框线颜色」；可与答题框颜色分别设置")]
    public string GuideLineColor { get; set; } = "";

    [Prop("引导线线宽 (mm)", Group = "解答题", Order = 12, Min = 0, Max = 2, Hint = "0 = 跟随页面框线线宽的 0.6 倍")]
    public double GuideLineStrokeMm { get; set; } = 0;

    [Prop("题干文字", Group = "解答题", Order = 13, Multiline = true, Hint = "每行对应一题；留空只显示题号")]
    public string QuestionTexts { get; set; } = "";

    [Prop("内容字号 (mm)", Group = "解答题", Order = 14, Min = 1.5, Max = 15)]
    public double FontSizeMm { get; set; } = 3.6;

    [Prop("题号后显示分数", Group = "解答题", Order = 15,
        Hint = "开启后在每个题号后面显示该题分数，例如「17.（12分）」；不开启则完全不显示")]
    public bool ShowScore { get; set; } = false;

    [Prop("统一分数 (分)", Group = "解答题", Order = 16, Min = 0, Max = 300,
        Hint = "所有题的默认分数；下面「每题分数」单独填过的题以各自数值为准。填 0 = 不显示分数")]
    public double DefaultScore { get; set; } = 10;

    [Prop("分数显示格式", Group = "解答题", Order = 17,
        Hint = "题号后显示的文字，{0} 自动替换为分数；如「（{0}分）」「[{0}′]」「本小题满分 {0} 分」。留空 = （{0}分）")]
    public string ScoreFormat { get; set; } = "（{0}分）";

    [Prop("每题分数 (分)", Group = "解答题", Order = 18,
        Hint = "逐题设定分数，留空或 0 = 跟随统一分数；可填小数")]
    public List<double> QuestionScores { get; set; } = new();

    [Prop("分数字号 (mm)", Group = "解答题", Order = 19, Min = 0, Max = 15,
        Hint = "0 = 跟随「内容字号」；可单独把分数设小一号")]
    public double ScoreFontSizeMm { get; set; } = 0;

    [Prop("分数颜色", Group = "解答题", Order = 20, IsColor = true,
        Hint = "留空 = 跟随页面文字颜色")]
    public string ScoreColor { get; set; } = "";

    [Prop("允许单题跨页", Group = "解答题", Order = 21,
        Hint = "开启（默认）：某题高度超过本栏剩余空间时，自动拆到下一栏 / 下一页继续作答，续排处标注「N题续」；关闭：整题移到下一页，本页留白")]
    public bool AllowQuestionSplit { get; set; } = true;

    /// <summary>第 index 题（0 基）实际显示的分数；未单独填过时用统一分数。</summary>
    public double ScoreFor(int index)
    {
        var list = QuestionScores;
        if (list != null && index >= 0 && index < list.Count && list[index] > 0) return list[index];
        return DefaultScore;
    }

    public bool HasCustomScore(int index) =>
        QuestionScores != null && index >= 0 && index < QuestionScores.Count &&
        QuestionScores[index] > 0;

    /// <summary>写入单题分数（分）；≤ 0 表示恢复为统一分数。</summary>
    public void SetQuestionScore(int index, double value)
    {
        if (index < 0) return;
        QuestionScores ??= new List<double>();
        while (QuestionScores.Count <= index) QuestionScores.Add(0);
        QuestionScores[index] = value > 0 ? Math.Round(Math.Clamp(value, 0, 300), 1) : 0;
    }

    public void ClearQuestionScore(int index)
    {
        if (QuestionScores == null || index < 0 || index >= QuestionScores.Count) return;
        QuestionScores[index] = 0;
    }

    public void ClearAllQuestionScores() => QuestionScores?.Clear();

    /// <summary>题号后要显示的分数文字；未开启显示、或分数 ≤ 0 时返回空串。</summary>
    public string ScoreTextFor(int index)
    {
        if (!ShowScore) return "";
        double s = ScoreFor(index);
        if (s <= 0) return "";
        string fmt = string.IsNullOrWhiteSpace(ScoreFormat) ? "（{0}分）" : ScoreFormat;
        string num = s.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return fmt.Replace("{0}", num);
    }

    public double EffectiveScoreFontSizeMm => ScoreFontSizeMm > 0 ? ScoreFontSizeMm : FontSizeMm;

    /// <summary>第 index 题（0 基）已单独填写的小题号原文（含空白）；未填返回空串。</summary>
    public string CustomNumber(int index)
    {
        var list = QuestionNumbers;
        if (list != null && index >= 0 && index < list.Count) return list[index] ?? "";
        return "";
    }

    public bool HasCustomNumber(int index) => !string.IsNullOrWhiteSpace(CustomNumber(index));

    /// <summary>第 index 题（0 基）实际显示的题号文字（不含后缀点）：填了「小题号」用填写值，否则用「起始题号 + 序号」。</summary>
    public string NumberFor(int index)
    {
        var s = CustomNumber(index);
        if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        return $"{StartNumber + index}";
    }

    public void SetQuestionNumber(int index, string value)
    {
        if (index < 0) return;
        QuestionNumbers ??= new List<string>();
        while (QuestionNumbers.Count <= index) QuestionNumbers.Add("");
        QuestionNumbers[index] = (value ?? "").Trim();
    }

    public void ClearQuestionNumber(int index)
    {
        if (QuestionNumbers == null || index < 0 || index >= QuestionNumbers.Count) return;
        QuestionNumbers[index] = "";
    }

    public void ClearAllQuestionNumbers() => QuestionNumbers?.Clear();

    /// <summary>第 index 题（0 基）的答题区高度；未单独设定时用统一高度。</summary>
    public double HeightFor(int index)
    {
        var list = QuestionHeightsMm;
        if (list != null && index >= 0 && index < list.Count && list[index] > 0)
            return list[index];
        return AreaHeightMm;
    }

    public bool HasCustomHeight(int index) =>
        QuestionHeightsMm != null && index >= 0 && index < QuestionHeightsMm.Count &&
        QuestionHeightsMm[index] > 0;

    /// <summary>写入单题高度（页面拖拽与属性面板共用），mm ≤ 0 表示恢复默认。</summary>
    public void SetQuestionHeight(int index, double mm)
    {
        if (index < 0) return;
        QuestionHeightsMm ??= new List<double>();
        while (QuestionHeightsMm.Count <= index) QuestionHeightsMm.Add(0);
        QuestionHeightsMm[index] = mm > 0 ? Math.Round(Math.Clamp(mm, 8, 300), 1) : 0;
    }

    public void ClearQuestionHeight(int index)
    {
        if (QuestionHeightsMm == null || index < 0 || index >= QuestionHeightsMm.Count) return;
        QuestionHeightsMm[index] = 0;
    }

    public void ClearAllQuestionHeights() => QuestionHeightsMm?.Clear();

    public override ModuleBase Clone()
    {
        var m = new SolutionModule
        {
            QuestionCount = QuestionCount, StartNumber = StartNumber,
            QuestionNumbers = QuestionNumbers == null ? new List<string>() : new List<string>(QuestionNumbers),
            AreaHeightMm = AreaHeightMm,
            QuestionHeightsMm = QuestionHeightsMm == null
                ? new List<double>() : new List<double>(QuestionHeightsMm),
            ShowFrame = ShowFrame,
            ShowGuideLines = ShowGuideLines, GuideGapMm = GuideGapMm,
            GuideLineCount = GuideLineCount, GuideLineWithNumber = GuideLineWithNumber,
            GuideLinePattern = GuideLinePattern, GuideLineColor = GuideLineColor,
            GuideLineStrokeMm = GuideLineStrokeMm,
            QuestionTexts = QuestionTexts, FontSizeMm = FontSizeMm,
            ShowScore = ShowScore, DefaultScore = DefaultScore, ScoreFormat = ScoreFormat,
            QuestionScores = QuestionScores == null ? new List<double>() : new List<double>(QuestionScores),
            ScoreFontSizeMm = ScoreFontSizeMm, ScoreColor = ScoreColor,
            AllowQuestionSplit = AllowQuestionSplit
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>考生信息栏（标题 + 字段 + 条形码区 / 涂考号区）。</summary>
public sealed class StudentInfoModule : ModuleBase
{
    public override string TypeKey => "info";
    public override string TypeName => "考生信息栏";

    [Prop("信息区组合", Group = "考生信息", Order = 0)]
    public InfoBarMode Mode { get; set; } = InfoBarMode.Both;

    [Prop("栏目标题", Group = "考生信息", Order = 1)]
    public string InfoTitle { get; set; } = "考生信息 / 注意事项";

    [Prop("显示栏目标题", Group = "考生信息", Order = 2)]
    public bool ShowInfoTitle { get; set; } = true;

    [Prop("显示信息栏外框", Group = "考生信息", Order = 2, Hint = "关掉后不画最外层大矩形；栏目标题横线、考号网格线与注意事项分隔线仍保留")]
    public bool ShowInfoFrame { get; set; } = true;

    [Prop("字段标签", Group = "考生信息", Order = 3, Hint = "逗号分隔，每行两个字段，自动排成两列")]
    public string FieldLabels { get; set; } = "姓名,班级,学校,考号,考场,座位号";

    [Prop("字段行高 (mm)", Group = "考生信息", Order = 4, Min = 4, Max = 30)]
    public double FieldHeightMm { get; set; } = 9.5;

    [Prop("条形码区宽 (mm)", Group = "考生信息", Order = 5, Min = 10, Max = 200)]
    public double BarcodeWidthMm { get; set; } = 56;

    [Prop("条形码区高 (mm)", Group = "考生信息", Order = 6, Min = 8, Max = 100)]
    public double BarcodeHeightMm { get; set; } = 20;

    [Prop("条形码提示文字", Group = "考生信息", Order = 7)]
    public string BarcodeText { get; set; } = "贴条形码处（请勿污损）";

    // ---- 涂考号区：顶部标题行 + 手写数字行 + 10 行 × N 列填涂网格 ----
    [Prop("考号区标题", Group = "涂考号区", Order = 0, Hint = "图中「准考证号」那一行")]
    public string CandidateTitle { get; set; } = "准考证号";

    [Prop("考号位数", Group = "涂考号区", Order = 1, Min = 1, Max = 20, Hint = "网格列数；每列对应一位考号")]
    public int CandidateDigits { get; set; } = 10;

    [Prop("整体大小 (%)", Group = "涂考号区", Order = 2, Min = 30, Max = 250, Hint = "一键等比缩放整块考号区：宽、高、标题字号、数字框、数字字号、线宽一起变；宽度不会超过所在栏")]
    public double CandidateScalePercent { get; set; } = 100;

    [Prop("指定宽度 (mm)", Group = "涂考号区", Order = 3, Min = 0, Max = 400, Hint = "直接设定整块宽度；0 = 按下方「宽度 (%)」自动。超过栏宽时自动收敛")]
    public double CandidateAreaWidthMm { get; set; } = 0;

    [Prop("指定总高 (mm)", Group = "涂考号区", Order = 4, Min = 0, Max = 300, Hint = "直接设定整块总高；0 = 按各行高自动。设定后标题行/手写行/10 行按原比例重排，框与字号同步缩放")]
    public double CandidateAreaHeightMm { get; set; } = 0;

    [Prop("宽度 (%)", Group = "涂考号区", Order = 5, Min = 20, Max = 100, Hint = "相对考生信息栏宽度；100 = 整幅通栏。仅当「指定宽度」为 0 时生效")]
    public double CandidateAreaWidthPercent { get; set; } = 100;

    [Prop("标题行高 (mm)", Group = "涂考号区", Order = 6, Min = 4, Max = 30)]
    public double CandidateTitleHeightMm { get; set; } = 10;

    [Prop("标题字号 (mm)", Group = "涂考号区", Order = 7, Min = 2, Max = 15)]
    public double CandidateTitleSizeMm { get; set; } = 5.2;

    [Prop("显示手写数字行", Group = "涂考号区", Order = 8, Hint = "标题行下方的一行空格，供手写考号")]
    public bool ShowHandwriteRow { get; set; } = true;

    [Prop("手写行高 (mm)", Group = "涂考号区", Order = 9, Min = 3, Max = 30)]
    public double HandwriteRowHeightMm { get; set; } = 8;

    [Prop("填涂行高 (mm)", Group = "涂考号区", Order = 10, Min = 3, Max = 20, Hint = "0~9 共 10 行，每行的高度；调大可让整块更接近示例图的比例")]
    public double CandidateRowHeightMm { get; set; } = 7;

    [Prop("填涂框样式", Group = "涂考号区", Order = 11, Hint = "可以设成与选择题同款的字面中括号 [0]，也可以保持细线方框；两处互不影响的独立选择")]
    public BracketBoxStyle CandidateBoxStyle { get; set; } = BracketBoxStyle.Box;

    [Prop("填涂尺寸来源", Group = "涂考号区", Order = 11, Hint = "默认跟随「全卷填涂样式」模板，与选择题共用一套尺寸；切到「单独设定」后，下方「考号填涂样式」组与本组的数字框宽/高/字号才生效。字体始终在「考号区字符模板」里设")]
    public FillSizeSource FillSizeSource { get; set; } = FillSizeSource.FollowTemplate;

    [Prop("数字框宽 (mm)", Group = "涂考号区", Order = 12, Min = 1.5, Max = 30, Hint = "「单独设定」时的方框宽度 / 中括号单元宽度；实际宽度不会超过单元格宽度，位数再多也不会挤在一起")]
    public double CandidateBoxWidthMm { get; set; } = 9.0;

    [Prop("数字框高 (mm)", Group = "涂考号区", Order = 13, Min = 1.5, Max = 20, Hint = "「单独设定」时的框高 / 括号高度；调得比数字字号还矮时，数字会自动缩小但始终完整显示，不会被裁掉")]
    public double CandidateBoxHeightMm { get; set; } = 4.6;

    [Prop("数字字号 (mm)", Group = "涂考号区", Order = 14, Min = 1, Max = 12, Hint = "「单独设定」时 0~9 的字号（跟随模板时用「考号区字符模板」的字号）；字体在「全卷填涂样式」的两套字符模板里设。字号大于框高时默认自动缩小到能完整显示，可在「全卷填涂样式」关闭自动适应")]
    public double CandidateDigitSizeMm { get; set; } = 3.0;

    [Prop("绘制网格线", Group = "涂考号区", Order = 15, Hint = "列竖线、行横线与外框")]
    public bool CandidateGridLines { get; set; } = true;

    [Prop("网格线宽 (mm)", Group = "涂考号区", Order = 16, Min = 0.05, Max = 2, Hint = "网格线粗细；「单独设定」时也作为括号 / 方框的线宽")]
    public double CandidateLineStrokeMm { get; set; } = 0.25;

    // ---- 尺寸来源 = 「单独设定」时，考号区自己的 [0] / 方框尺寸 ----
    [Prop("括号臂长 (mm)", Group = "考号填涂样式", Order = 0, Min = 0.2, Max = 15, Hint = "仅「单独设定」生效；[ 与 ] 上下两横的长度")]
    public double CandidateBracketArmMm { get; set; } = 1.2;

    [Prop("括号与数字间距 (mm)", Group = "考号填涂样式", Order = 1, Min = 0, Max = 20, Hint = "仅「单独设定」生效；[ 到数字、数字到 ] 的水平距离")]
    public double CandidateBracketGapMm { get; set; } = 0.9;

    [Prop("左右间距分别设置", Group = "考号填涂样式", Order = 2, Hint = "仅「单独设定」生效")]
    public bool CandidateSeparateRightGap { get; set; } = false;

    [Prop("右括号与数字间距 (mm)", Group = "考号填涂样式", Order = 3, Min = 0, Max = 20, Hint = "仅当上一项开启时生效")]
    public double CandidateBracketRightGapMm { get; set; } = 0.9;

    [Prop("垂直偏移 (mm)", Group = "考号填涂样式", Order = 4, Min = -10, Max = 10, Hint = "仅「单独设定」生效；正数向下微调括号")]
    public double CandidateBracketOffsetYMm { get; set; } = 0;

    // ---- 仅涂考号区时的左右并排布局 ----
    [Prop("仅涂考号时置于右方", Group = "涂考号区", Order = 17, Hint = "信息区组合 = 仅涂考号区时，把考号网格放到字段区右侧并排；关掉则仍是上下排列")]
    public bool CandidateSideBySide { get; set; } = true;

    [Prop("右方占比 (%)", Group = "涂考号区", Order = 18, Min = 25, Max = 75, Hint = "并排时考号区占整幅宽度的比例；也可直接用「指定宽度 (mm)」精确设定")]
    public double CandidateSidePercent { get; set; } = 52;

    [Prop("填涂框线型", Group = "涂考号区", Order = 19, Hint = "考号区 [0] 中括号 / 方框的线型：实线或虚线。与选择题「选项框线型」各自独立；默认实线")]
    public LinePattern CandidateFramePattern { get; set; } = LinePattern.Solid;

    [Prop("注意事项", Group = "考生信息", Order = 11, Multiline = true)]
    public string NoticeText { get; set; } = "1. 答题前请核对并填涂考号，将条形码贴在指定区域。\n2. 选择题必须用 2B 铅笔填涂；非选择题用黑色签字笔在指定区域内作答。\n3. 保持卡面清洁，不要折叠、破损或做其他标记。";

    [Prop("显示注意事项", Group = "考生信息", Order = 12)]
    public bool ShowNotice { get; set; } = true;

    [Prop("右置时注意事项放字段下方", Group = "考生信息", Order = 13, Hint = "「仅涂考号区 + 考号区置于右方」时生效：注意事项移到左侧字段区正下方，与右侧考号网格共用同一段高度，节约版面；关掉则仍在整幅底部独占一行")]
    public bool NoticeUnderFieldsWhenSide { get; set; } = true;

    [Prop("内容字号 (mm)", Group = "考生信息", Order = 14, Min = 1.5, Max = 12)]
    public double FontSizeMm { get; set; } = 3.2;

    public override ModuleBase Clone()
    {
        var m = new StudentInfoModule
        {
            Mode = Mode, InfoTitle = InfoTitle, ShowInfoTitle = ShowInfoTitle,
            ShowInfoFrame = ShowInfoFrame,
            FieldLabels = FieldLabels, FieldHeightMm = FieldHeightMm,
            BarcodeWidthMm = BarcodeWidthMm, BarcodeHeightMm = BarcodeHeightMm,
            BarcodeText = BarcodeText, CandidateDigits = CandidateDigits,
            CandidateTitle = CandidateTitle,
            CandidateScalePercent = CandidateScalePercent,
            CandidateAreaWidthMm = CandidateAreaWidthMm,
            CandidateAreaHeightMm = CandidateAreaHeightMm,
            CandidateAreaWidthPercent = CandidateAreaWidthPercent,
            CandidateTitleHeightMm = CandidateTitleHeightMm,
            CandidateTitleSizeMm = CandidateTitleSizeMm,
            ShowHandwriteRow = ShowHandwriteRow,
            HandwriteRowHeightMm = HandwriteRowHeightMm,
            CandidateRowHeightMm = CandidateRowHeightMm,
            CandidateBoxStyle = CandidateBoxStyle,
            FillSizeSource = FillSizeSource,
            CandidateBoxWidthMm = CandidateBoxWidthMm,
            CandidateBoxHeightMm = CandidateBoxHeightMm,
            CandidateDigitSizeMm = CandidateDigitSizeMm,
            CandidateGridLines = CandidateGridLines,
            CandidateLineStrokeMm = CandidateLineStrokeMm,
            CandidateBracketArmMm = CandidateBracketArmMm,
            CandidateBracketGapMm = CandidateBracketGapMm,
            CandidateSeparateRightGap = CandidateSeparateRightGap,
            CandidateBracketRightGapMm = CandidateBracketRightGapMm,
            CandidateBracketOffsetYMm = CandidateBracketOffsetYMm,
            CandidateSideBySide = CandidateSideBySide,
            CandidateSidePercent = CandidateSidePercent,
            CandidateFramePattern = CandidateFramePattern,
            NoticeText = NoticeText, ShowNotice = ShowNotice,
            NoticeUnderFieldsWhenSide = NoticeUnderFieldsWhenSide, FontSizeMm = FontSizeMm
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>作文答题区：语文方格稿纸（行间可留空隙）/ 英语实线横线，行数多时自动跨栏、跨面续排。</summary>
public sealed class EssayModule : ModuleBase
{
    /// <summary>稿纸自身最外一圈线已是外框，默认不再叠加「整组外框」以免边框变粗。</summary>
    public EssayModule() { ShowGroupFrame = false; }

    public override string TypeKey => "essay";
    public override string TypeName => "作文答题区";

    [Prop("稿纸样式", Group = "作文区", Order = 0, Hint = "语文作文用方格稿纸；英语作文用实线横线（没有四线三格）")]
    public EssayGridMode GridMode { get; set; } = EssayGridMode.Squares;

    [Prop("总行数", Group = "作文区", Order = 1, Min = 1, Max = 300, Hint = "整块作文区共多少行；本面放不下会自动续到下一栏 / 下一面")]
    public int Rows { get; set; } = 24;

    [Prop("每行格数", Group = "作文区", Order = 2, Min = 1, Max = 60, Hint = "方格稿纸每行几格（语文常 20 格）；实线横线模式下该项不生效")]
    public int Columns { get; set; } = 20;

    [Prop("格宽 / 行高 (mm)", Group = "作文区", Order = 3, Min = 3, Max = 25, Hint = "方格边长，也是横线模式的行高；超过栏宽时自动等比收窄，绝不越界")]
    public double CellSizeMm { get; set; } = 8.0;

    [Prop("行间隔 (mm)", Group = "作文区", Order = 4, Min = 0, Max = 20,
        Hint = "行与行之间留出的空白：语文作文格纸行间有间隔，方格每行各自成框；英语横线模式下为相邻两条实线的额外间距。字数刻度就写在这一条间隔里，所以开启刻度时会至少留够刻度文字的高度；关闭刻度后填 0 = 行紧贴")]
    public double RowGapMm { get; set; } = 4.0;

    [Prop("格子线颜色", Group = "作文区", Order = 5, IsColor = true, Hint = "留空 = 跟随模块「框线颜色」")]
    public string GridLineColor { get; set; } = "";

    [Prop("格子线宽 (mm)", Group = "作文区", Order = 6, Min = 0.05, Max = 1)]
    public double GridLineStrokeMm { get; set; } = 0.2;

    [Prop("显示字数刻度", Group = "作文区", Order = 7, Hint = "在累计字数达到整百的「当前行下方的行间隔」里、格子区右端标注 100 / 200 / …，不占格子右侧的独立竖列；仅方格稿纸生效")]
    public bool ShowWordMarks { get; set; } = true;

    [Prop("刻度间隔 (字)", Group = "作文区", Order = 8, Min = 10, Max = 1000)]
    public int WordsPerMark { get; set; } = 100;

    [Prop("刻度字号 (mm)", Group = "作文区", Order = 9, Min = 1.5, Max = 10,
        Hint = "刻度写在行间隔里；若间隔小于文字所需高度，行间隔会自动加大，保证数字完整不压线")]
    public double WordMarkSizeMm { get; set; } = 2.4;

    [Prop("刻度颜色", Group = "作文区", Order = 10, IsColor = true, Hint = "留空 = 使用页面文字颜色")]
    public string WordMarkColor { get; set; } = "";

    public override ModuleBase Clone()
    {
        var m = new EssayModule
        {
            GridMode = GridMode == EssayGridMode.FourLine ? EssayGridMode.Ruled : GridMode,
            Rows = Rows, Columns = Columns,
            CellSizeMm = CellSizeMm, RowGapMm = RowGapMm,
            GridLineColor = GridLineColor, GridLineStrokeMm = GridLineStrokeMm,
            ShowWordMarks = ShowWordMarks, WordsPerMark = WordsPerMark,
            WordMarkSizeMm = WordMarkSizeMm, WordMarkColor = WordMarkColor
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>非作答区：带文字 / 背景色 / 边框色的提示块（可作「请勿作答」区、草稿区、装订区）。</summary>
public sealed class NonAnswerModule : ModuleBase
{
    public override string TypeKey => "nonanswer";
    public override string TypeName => "非作答区";

    [Prop("文字内容", Group = "非作答区", Order = 0, Multiline = true, Hint = "支持换行，例如「非作答区 / 请勿在此区域内作答」")]
    public string Text { get; set; } = "非作答区\n请勿在此区域内作答";

    [Prop("区域高度 (mm)", Group = "非作答区", Order = 1, Min = 5, Max = 260)]
    public double HeightMm { get; set; } = 40;

    [Prop("文字颜色", Group = "非作答区", Order = 2, IsColor = true, Hint = "留空 = 使用页面文字颜色")]
    public string TextColor { get; set; } = "";

    [Prop("背景颜色", Group = "非作答区", Order = 3, IsColor = true, Hint = "留空 = 透明不填充；推荐浅色，如 #F2F2F2、#FFF4E5")]
    public string BackgroundColor { get; set; } = "#F2F2F2";

    [Prop("边框颜色", Group = "非作答区", Order = 4, IsColor = true, Hint = "留空 = 跟随模块「框线颜色」")]
    public string BorderColor { get; set; } = "";

    [Prop("边框样式", Group = "非作答区", Order = 5, Hint = "无边框 / 实线 / 虚线；背景填充在三种情况下都会保留")]
    public BorderPattern BorderStyle { get; set; } = BorderPattern.Solid;

    [Prop("边框线宽 (mm)", Group = "非作答区", Order = 6, Min = 0.05, Max = 2)]
    public double BorderStrokeMm { get; set; } = 0.3;

    [Prop("斜线阴影", Group = "非作答区", Order = 7, Hint = "在背景上叠加 45° 斜线，更直观地标示「不可作答」；斜线使用边框颜色")]
    public bool Hatch { get; set; } = false;

    [Prop("斜线间距 (mm)", Group = "非作答区", Order = 8, Min = 2, Max = 40)]
    public double HatchGapMm { get; set; } = 8;

    [Prop("文字字号 (mm)", Group = "非作答区", Order = 9, Min = 1.5, Max = 30)]
    public double FontSizeMm { get; set; } = 6.0;

    [Prop("水平对齐", Group = "非作答区", Order = 10)]
    public TextAlign Align { get; set; } = TextAlign.Center;

    [Prop("垂直对齐", Group = "非作答区", Order = 11)]
    public VAlign TextVAlign { get; set; } = VAlign.Middle;

    [Prop("内边距 (mm)", Group = "非作答区", Order = 12, Min = 0, Max = 30)]
    public double PaddingMm { get; set; } = 3;

    public override ModuleBase Clone()
    {
        var m = new NonAnswerModule
        {
            Text = Text, HeightMm = HeightMm, TextColor = TextColor,
            BackgroundColor = BackgroundColor, BorderColor = BorderColor,
            BorderStyle = BorderStyle, BorderStrokeMm = BorderStrokeMm,
            Hatch = Hatch, HatchGapMm = HatchGapMm,
            FontSizeMm = FontSizeMm, Align = Align,
            TextVAlign = TextVAlign, PaddingMm = PaddingMm
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>自定义编辑区：任意文字 + 可选边框。</summary>
public sealed class CustomModule : ModuleBase
{
    public CustomModule() { ShowGroupFrame = true; }

    public override string TypeKey => "custom";
    public override string TypeName => "自定义编辑区";

    [Prop("区域高度 (mm)", Group = "自定义区", Order = 0, Min = 5, Max = 260)]
    public double HeightMm { get; set; } = 35;

    [Prop("文字内容", Group = "自定义区", Order = 1, Multiline = true)]
    public string Text { get; set; } = "在此输入自定义文字，支持换行。";

    [Prop("显示内框", Group = "自定义区", Order = 2, Hint = "默认由「通用 → 整组外框」提供外框；开启后额外再画一圈内框")]
    public bool ShowFrame { get; set; } = false;

    [Prop("内容字号 (mm)", Group = "自定义区", Order = 3, Min = 1.5, Max = 15)]
    public double FontSizeMm { get; set; } = 3.6;

    [Prop("内边距 (mm)", Group = "自定义区", Order = 4, Min = 0, Max = 30)]
    public double PaddingMm { get; set; } = 3;

    [Prop("文字对齐", Group = "自定义区", Order = 5)]
    public TextAlign Align { get; set; } = TextAlign.Left;

    public override ModuleBase Clone()
    {
        var m = new CustomModule
        {
            HeightMm = HeightMm, Text = Text, ShowFrame = ShowFrame,
            FontSizeMm = FontSizeMm, PaddingMm = PaddingMm, Align = Align
        };
        CopyBaseTo(m);
        return m;
    }
}

/// <summary>图片插图。</summary>
public sealed class ImageModule : ModuleBase
{
    public override string TypeKey => "image";
    public override string TypeName => "图片插图";

    [Prop("图片路径", Group = "图片", Order = 4, Hint = "相对项目文件 assets 目录，或绝对路径；工具箱可一键选择")]
    public string ImagePath { get; set; } = "";

    [Prop("宽度 (%)", Group = "图片", Order = 0, Min = 5, Max = 100, Hint = "相对所在栏宽度")]
    public double WidthPercent { get; set; } = 60;

    [Prop("对齐", Group = "图片", Order = 1)]
    public ImageAlign Align { get; set; } = ImageAlign.Center;

    [Prop("图片说明", Group = "图片", Order = 2)]
    public string Caption { get; set; } = "";

    [Prop("说明字号 (mm)", Group = "图片", Order = 3, Min = 1.5, Max = 12)]
    public double CaptionSizeMm { get; set; } = 3.2;


    public override ModuleBase Clone()
    {
        var m = new ImageModule
        {
            ImagePath = ImagePath, WidthPercent = WidthPercent,
            Align = Align, Caption = Caption, CaptionSizeMm = CaptionSizeMm
        };
        CopyBaseTo(m);
        return m;
    }
}

public static class ModuleFactory
{
    public static ModuleBase Create(string key) => key switch
    {
        "choice" => new ChoiceModule { Name = "选择题", Title = "一、选择题" },
        "blank" => new BlankModule { Name = "填空题", Title = "二、填空题" },
        "solution" => new SolutionModule { Name = "解答题", Title = "三、解答题" },
        "essay" => new EssayModule { Name = "作文答题区", Title = "作文答题区" },
        "nonanswer" => new NonAnswerModule { Name = "非作答区", ShowTitle = false },
        "info" => new StudentInfoModule { Name = "考生信息栏", ShowTitle = false },
        "custom" => new CustomModule { Name = "自定义编辑区", ShowTitle = false },
        "image" => new ImageModule { Name = "图片插图", ShowTitle = false },
        _ => new CustomModule { Name = "自定义编辑区", ShowTitle = false }
    };

    public static ModuleBase Resolve(string key) => key switch
    {
        "choice" => new ChoiceModule(),
        "blank" => new BlankModule(),
        "solution" => new SolutionModule(),
        "essay" => new EssayModule(),
        "nonanswer" => new NonAnswerModule(),
        "info" => new StudentInfoModule(),
        "custom" => new CustomModule(),
        "image" => new ImageModule(),
        _ => new CustomModule()
    };
}
