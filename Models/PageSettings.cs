namespace AnswerSheetStudio.Models;

public sealed class FaceIdSettings
{
    [Prop("标识样式", Group = "每面唯一标识", Order = 0)]
    public FaceIdStyle Style { get; set; } = FaceIdStyle.CombinedBoxes;

    [Prop("方块数量 (1~5)", Group = "每面唯一标识", Order = 1, Min = 1, Max = 5)]
    public int BlockCount { get; set; } = 4;

    [Prop("方块宽 (mm)", Group = "每面唯一标识", Order = 2, Min = 1, Max = 30)]
    public double BlockWidthMm { get; set; } = 5.0;

    [Prop("方块高 (mm)", Group = "每面唯一标识", Order = 3, Min = 1, Max = 30)]
    public double BlockHeightMm { get; set; } = 5.0;

    [Prop("方块间距 (mm)", Group = "每面唯一标识", Order = 4, Min = 0, Max = 30)]
    public double GapMm { get; set; } = 2.0;

    [Prop("距边缘 X (mm)", Group = "每面唯一标识", Order = 5, Min = 0, Max = 100)]
    public double OffsetXMm { get; set; } = 4.0;

    [Prop("距边缘 Y (mm)", Group = "每面唯一标识", Order = 6, Min = 0, Max = 100)]
    public double OffsetYMm { get; set; } = 2.0;

    [Prop("位置锚点", Group = "每面唯一标识", Order = 7)]
    public CornerAnchor Anchor { get; set; } = CornerAnchor.TopRight;

    [Prop("排列方向", Group = "每面唯一标识", Order = 8)]
    public IdOrientation Orientation { get; set; } = IdOrientation.Horizontal;

    [Prop("留白格画细框", Group = "每面唯一标识", Order = 9, Hint = "黑白组合样式中，白色格是否画空心框")]
    public bool OutlineWhiteBlocks { get; set; } = true;

    [Prop("与定位点同一行", Group = "每面唯一标识", Order = 10,
        Hint = "开启后每面唯一标识与四角定位点共用同一条边缘带（自动避开两端定位点），不再单独占一行，可为正文多留出一行高度；关闭则标识仍排在定位点下方。距边缘 X/Y 仍可微调")]
    public bool SameRowAsMarkers { get; set; } = true;

    public FaceIdSettings Clone() => (FaceIdSettings)MemberwiseClone();
}

public sealed class PageSettings
{
    [Prop("纸张", Group = "纸张与分页", Order = 0)]
    public PaperSize Paper { get; set; } = PaperSize.A4;

    [Prop("A3 每面栏数", Group = "纸张与分页", Order = 1, Min = 2, Max = 3, Hint = "一个「面」= 整张 A3 纸的正面或反面；栏只是面内的内容分栏。2 栏 = 左右各一张 A4 规格；3 栏 = 三等分")]
    public int Columns { get; set; } = 2;

    [Prop("基础页边距 (mm)", Group = "纸张与分页", Order = 2, Min = 0, Max = 60)]
    public double BaseMarginMm { get; set; } = 8.0;

    [Prop("标题/信息栏位置", Group = "纸张与分页", Order = 3, Hint = "默认只出现在每张纸正面的第一栏上方；A4 即奇数面。整面只放一套定位点与唯一标识")]
    public HeaderScope HeaderScope { get; set; } = HeaderScope.FrontFirstColumn;

    [Prop("自动放置标题与考生信息栏", Group = "纸张与分页", Order = 4)]
    public bool AutoHeader { get; set; } = true;

    [Prop("行间距 (mm)", Group = "版式", Order = 0, Min = 0, Max = 40, Hint = "标题到第一行、行与行之间严格一致")]
    public double LineGapMm { get; set; } = 3.0;

    [Prop("字体", Group = "版式", Order = 1, Options = new[] { "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "Arial" })]
    public string FontFamily { get; set; } = "Microsoft YaHei";

    [Prop("文字颜色", Group = "版式", Order = 2, IsColor = true)]
    public string TextColor { get; set; } = "#111111";

    [Prop("框线颜色", Group = "版式", Order = 3, IsColor = true)]
    public string BoxColor { get; set; } = "#333333";

    [Prop("框线粗细 (mm)", Group = "版式", Order = 4, Min = 0.05, Max = 3)]
    public double BoxStrokeMm { get; set; } = 0.3;

    [Prop("整栏答题区大外框", Group = "答题区外框", Order = 0, Hint = "给每一栏的全部内容（标题 / 考生信息栏 + 选择题 + 非选择题）整体画一个大外框；A3 多栏时每栏各自成框。模块自身的「答题区大外框」仍然独立生效")]
    public bool ShowAnswerAreaFrame { get; set; } = false;

    [Prop("答题区外框颜色", Group = "答题区外框", Order = 1, IsColor = true, Hint = "留空 = 使用页面设置的框线颜色")]
    public string AnswerAreaFrameColor { get; set; } = "";

    [Prop("答题区外框粗细 (mm)", Group = "答题区外框", Order = 2, Min = 0.05, Max = 3)]
    public double AnswerAreaFrameStrokeMm { get; set; } = 0.4;

    [Prop("答题区外框内缩 (mm)", Group = "答题区外框", Order = 3, Min = 0, Max = 20, Hint = "外框与内容之间的留白")]
    public double AnswerAreaFramePadMm { get; set; } = 2.0;

    [Prop("答题区外框线型", Group = "答题区外框", Order = 4, Hint = "每栏整幅答题区大外框的线型：实线或虚线；默认实线")]
    public LinePattern AnswerAreaFramePattern { get; set; } = LinePattern.Solid;

    [Prop("主标题", Group = "标题文字", Order = 0, Multiline = true)]
    public string Title { get; set; } = "2026 年普通高中学业水平考试";

    [Prop("副标题", Group = "标题文字", Order = 1, Multiline = true)]
    public string Subtitle { get; set; } = "答 题 卡    科目：____________    考试时间：______ 分钟";

    [Prop("主标题字号 (mm)", Group = "标题文字", Order = 2, Min = 2, Max = 25)]
    public double TitleSizeMm { get; set; } = 7.2;

    [Prop("副标题字号 (mm)", Group = "标题文字", Order = 3, Min = 2, Max = 20)]
    public double SubtitleSizeMm { get; set; } = 4.0;

    [Prop("标题与信息栏间距 (mm)", Group = "标题文字", Order = 4, Min = 0, Max = 30)]
    public double HeaderGapMm { get; set; } = 3.0;

    [Prop("显示页眉", Group = "页眉页脚", Order = 0)]
    public bool ShowHeader { get; set; } = true;

    [Prop("页眉文字", Group = "页眉页脚", Order = 1, Hint = "{面}/{总面}=印刷面序号；{页}/{总页}=纸张序号；{栏}/{总栏}=栏序号。整面只有一条页眉")]
    public string HeaderText { get; set; } = "{标题}  ·  第 {面} 面 / 共 {总面} 面  ·  第 {页} 张纸";

    [Prop("显示页脚", Group = "页眉页脚", Order = 2)]
    public bool ShowFooter { get; set; } = true;

    [Prop("页脚文字", Group = "页眉页脚", Order = 3)]
    public string FooterText { get; set; } = "请在各题指定区域内作答，超出区域的答案无效   ·   第 {面} 面";

    [Prop("页眉页脚字号 (mm)", Group = "页眉页脚", Order = 4, Min = 1.5, Max = 12)]
    public double HeaderFooterSizeMm { get; set; } = 3.0;

    [Prop("定位点样式", Group = "四角定位点", Order = 0)]
    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.Square;

    [Prop("定位点边长 (mm)", Group = "四角定位点", Order = 1, Min = 2, Max = 10)]
    public double MarkerSizeMm { get; set; } = 5.0;

    [Prop("定位点贴边距 (mm)", Group = "四角定位点", Order = 2, Min = 0, Max = 20)]
    public double MarkerInsetMm { get; set; } = 1.5;

    [Prop("定位点颜色", Group = "四角定位点", Order = 3, IsColor = true)]
    public string MarkerColor { get; set; } = "#000000";

    [Prop("强制输出底色", Group = "四角定位点", Order = 4, Hint = "打印时在每个定位点下强制铺白色底，保证阅卷机识别")]
    public bool ForceMarkerBackground { get; set; } = true;

    [Prop("预览显示栏辅助线", Group = "编辑器", Order = 0, Hint = "仅预览可见，不打印")]
    public bool ShowGuides { get; set; } = true;

    public FaceIdSettings FaceId { get; set; } = new();

    /// <summary>全卷填涂尺寸模板：选择题 [A] 与考号区 [0] 默认共用；样式类型各自可选。</summary>
    public FillStyleSettings FillStyle { get; set; } = new();
}
