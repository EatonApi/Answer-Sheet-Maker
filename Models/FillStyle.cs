namespace AnswerSheetStudio.Models;

/// <summary>
/// 全卷填涂样式：括号 / 方框等「尺寸」仍是选择题 [A] 与考号区 [0] 共用的一套模板（改一次两处一起变）；
/// 而「字符字号」与「字符字体」拆成两套独立模板，两处可以各自设定、互不影响。
/// 样式类型（方框 / 字面中括号）由各模块自己选择；若某一处需要不同尺寸，
/// 把该模块的「尺寸来源」切到「单独设定」即可（字号也随模块自己走，字体仍由本页两套模板决定）。
/// </summary>
public sealed class FillStyleSettings
{
    /// <summary>旧版共用字号：仅用于读取早期项目；现在字号在下面两套字符模板里分别设定。</summary>
    public double CharSizeMm { get; set; } = 3.6;

    public const string FollowPageFont = "跟随页面字体";
    private const string FontOptionsKey = "跟随页面字体";

    // ---- 两套字符模板：尺寸共用上面一组，字号与字体各自独立 ----
    [Prop("字母字号 (mm)", Group = "选择题字符模板", Order = 0, Min = 0.5, Max = 20,
        Hint = "只作用于选择题 [A] 里的 A / B / AA 等字母；与考号区的数字字号互不影响。括号 / 方框尺寸仍由上方共用模板统一控制")]
    public double ChoiceCharSizeMm { get; set; } = 3.6;

    [Prop("字母字体", Group = "选择题字符模板", Order = 1,
        Options = new[] { FontOptionsKey, "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "Arial", "Times New Roman", "Consolas" },
        Hint = "只作用于 [A] 里的字母；「跟随页面字体」= 使用「纸张与版式 → 字体」。下拉里可直接输入本机已安装的字体名")]
    public string ChoiceFontFamily { get; set; } = FollowPageFont;

    [Prop("数字字号 (mm)", Group = "考号区字符模板", Order = 0, Min = 0.5, Max = 20,
        Hint = "只作用于考号区 [0] 里的 0~9 数字；与选择题的字母字号互不影响")]
    public double CandidateCharSizeMm { get; set; } = 3.6;

    [Prop("数字字体", Group = "考号区字符模板", Order = 1,
        Options = new[] { FontOptionsKey, "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "Arial", "Times New Roman", "Consolas" },
        Hint = "只作用于 [0] 里的数字；「跟随页面字体」= 使用「纸张与版式 → 字体」。下拉里可直接输入本机已安装的字体名")]
    public string CandidateFontFamily { get; set; } = FollowPageFont;

    [Prop("括号高度 (mm)", Group = "全卷填涂模板", Order = 1, Min = 1, Max = 30,
        Hint = "[ 与 ] 竖线的总高度；同时也是方框样式的默认框高")]
    public double HeightMm { get; set; } = 5.4;

    [Prop("括号臂长 (mm)", Group = "全卷填涂模板", Order = 2, Min = 0.2, Max = 15,
        Hint = "[ 与 ] 上下两横的长度，决定括号整体的横向大小")]
    public double ArmMm { get; set; } = 1.4;

    [Prop("括号与字符间距 (mm)", Group = "全卷填涂模板", Order = 3, Min = 0, Max = 20,
        Hint = "[ 到字符、字符到 ] 的水平距离；越大括号离字符越远")]
    public double GapMm { get; set; } = 0.9;

    [Prop("左右间距分别设置", Group = "全卷填涂模板", Order = 4,
        Hint = "开启后，右括号到字符的距离使用单独的数值")]
    public bool SeparateRightGap { get; set; } = false;

    [Prop("右括号与字符间距 (mm)", Group = "全卷填涂模板", Order = 5, Min = 0, Max = 20,
        Hint = "仅当上一项开启时生效")]
    public double RightGapMm { get; set; } = 0.9;

    [Prop("括号线宽 (mm)", Group = "全卷填涂模板", Order = 6, Min = 0.05, Max = 3)]
    public double StrokeMm { get; set; } = 0.3;

    [Prop("垂直偏移 (mm)", Group = "全卷填涂模板", Order = 7, Min = -10, Max = 10,
        Hint = "正数向下微调括号，用于与字符视觉居中")]
    public double OffsetYMm { get; set; } = 0;

    [Prop("方框宽度 (mm)", Group = "全卷填涂模板", Order = 8, Min = 1.5, Max = 40,
        Hint = "样式选「方框填涂区」时的方框宽度")]
    public double BoxWidthMm { get; set; } = 8.0;

    [Prop("方框高度 (mm)", Group = "全卷填涂模板", Order = 9, Min = 1.5, Max = 30,
        Hint = "样式选「方框填涂区」时的方框高度；0 表示与「括号高度」一致")]
    public double BoxHeightMm { get; set; } = 5.0;

    [Prop("字符自动适应框高", Group = "全卷填涂模板", Order = 10,
        Hint = "开启（默认）：字号大于填涂框 / 括号高度时，自动等比缩小字符，保证 A、0~9 完整显示且不出框（框高、字号设定值不变）。关闭：保持设定字号，字符允许略超出框，但同样不会被裁切。选择题与考号区都生效，包括「单独设定」")]
    public bool FitCharToBox { get; set; } = true;

    public FillStyleSettings Clone() => (FillStyleSettings)MemberwiseClone();
}
