namespace AnswerSheetStudio.Models;

public enum PaperSize { A4, A3 }
public enum ChoiceKind { Single, Multiple, Judgement }
public enum ChoiceDisplay { Bracket, Underline, Both }
public enum BracketBoxStyle { Box, Literal }
/// <summary>保留兼容：旧项目里考号区用的枚举（数值与 BracketBoxStyle 一致）。</summary>
public enum DigitBoxStyle { Box, Literal }
/// <summary>填涂尺寸来源：跟随全卷模板，或本模块单独设定。</summary>
public enum FillSizeSource { FollowTemplate, Custom }
public enum MarkerStyle { Square, Triangle }
public enum FaceIdStyle { CombinedBoxes, MovingDots }
public enum ImageAlign { Left, Center, Right }
/// <summary>模块区域变窄后在栏内的水平位置。</summary>
public enum RegionAlign { Left, Center, Right }
public enum InfoBarMode { BarcodeOnly, CandidateNoOnly, Both }
/// <summary>选择题的题目排列：横排 = 每行尽量多题；竖排 = 每行只放一题。</summary>
public enum ChoiceLayout { Horizontal, Vertical }
/// <summary>选择题的选项排列：横排 = A B C D 左右并排；竖排 = 每个选项单独占一行。</summary>
public enum OptionLayout { Horizontal, Vertical }
public enum HeaderScope { FrontFirstColumn, AllFrontColumns, SpanFrontSide }
public enum CornerAnchor { TopLeft, TopRight, BottomLeft, BottomRight }
public enum IdOrientation { Horizontal, Vertical }
public enum TextAlign { Left, Center, Right }
public enum VAlign { Top, Middle, Bottom }
public enum DrawKind { Text, Rect, Line, Polygon, Image }
/// <summary>作文答题区的稿纸样式：语文方格稿纸 / 英语实线横线。</summary>
/// <remarks>FourLine 仅为兼容早期项目里保存的数值 2 而保留，不再出现在属性面板；加载时统一迁移成 Ruled。</remarks>
public enum EssayGridMode { Squares = 0, Ruled = 1, FourLine = 2 }
/// <summary>线条样式：实线 / 虚线（解答题引导线等）。</summary>
public enum LinePattern { Solid, Dashed }
/// <summary>矩形边框样式：无 / 实线 / 虚线（非作答区等）。</summary>
public enum BorderPattern { None, Solid, Dashed }

public static class EnumText
{
    private static readonly Dictionary<object, string> Map = new()
    {
        [PaperSize.A4] = "A4（210×297，双面，1 栏）",
        [PaperSize.A3] = "A3 横向（420×297；一面 = 整张纸，内含多栏）",
        [ChoiceKind.Single] = "单选题",
        [ChoiceKind.Multiple] = "多选题",
        [ChoiceKind.Judgement] = "判断题",
        [ChoiceDisplay.Bracket] = "中括号 [A] / 方框填涂",
        [ChoiceDisplay.Underline] = "横线手写",
        [ChoiceDisplay.Both] = "填涂 + 手写",
        [BracketBoxStyle.Box] = "方框填涂区（推荐阅卷机）",
        [BracketBoxStyle.Literal] = "字面中括号 [A] / [0]（线段绘制，尺寸可设计）",
        [DigitBoxStyle.Box] = "细线方框 + 框内数字",
        [DigitBoxStyle.Literal] = "字面中括号 [0]",
        [FillSizeSource.FollowTemplate] = "跟随「全卷填涂样式」模板（默认）",
        [FillSizeSource.Custom] = "单独设定（用本模块自己的尺寸参数）",
        [MarkerStyle.Square] = "实心方块",
        [MarkerStyle.Triangle] = "实心三角",
        [FaceIdStyle.CombinedBoxes] = "矩形框黑白组合（位置固定）",
        [FaceIdStyle.MovingDots] = "黑色方块定位点（位置随面变化）",
        [ImageAlign.Left] = "左对齐",
        [ImageAlign.Center] = "居中",
        [ImageAlign.Right] = "右对齐",
        [RegionAlign.Left] = "靠左",
        [RegionAlign.Center] = "居中",
        [RegionAlign.Right] = "靠右",
        [InfoBarMode.BarcodeOnly] = "仅条形码区",
        [InfoBarMode.CandidateNoOnly] = "仅涂考号区",
        [InfoBarMode.Both] = "条形码区 + 涂考号区",
        [ChoiceLayout.Horizontal] = "横排（每行按栏宽尽量多题）",
        [ChoiceLayout.Vertical] = "竖排（每题独占区块，可多列并排）",
        [OptionLayout.Horizontal] = "横排（A B C D 左右并排）",
        [OptionLayout.Vertical] = "竖排（A B C D 上下堆叠）",
        [HeaderScope.FrontFirstColumn] = "只在每张纸正面第一栏上方（默认）",
        [HeaderScope.AllFrontColumns] = "每张纸正面每一栏",
        [HeaderScope.SpanFrontSide] = "A3 正面整幅通栏（可选）",
        [CornerAnchor.TopLeft] = "左上角",
        [CornerAnchor.TopRight] = "右上角",
        [CornerAnchor.BottomLeft] = "左下角",
        [CornerAnchor.BottomRight] = "右下角",
        [IdOrientation.Horizontal] = "横向排列",
        [IdOrientation.Vertical] = "纵向排列",
        [TextAlign.Left] = "靠左",
        [TextAlign.Center] = "居中",
        [TextAlign.Right] = "靠右",
        [VAlign.Top] = "靠上",
        [VAlign.Middle] = "垂直居中",
        [VAlign.Bottom] = "靠下",
        [EssayGridMode.Squares] = "方格稿纸（语文作文，每格一字，行间可留空隙）",
        [EssayGridMode.Ruled] = "实线横线（英语作文，单行书写）",
        [EssayGridMode.FourLine] = "实线横线（英语作文，旧版兼容）",
        [LinePattern.Solid] = "实线",
        [LinePattern.Dashed] = "虚线",
        [BorderPattern.None] = "无边框",
        [BorderPattern.Solid] = "实线边框",
        [BorderPattern.Dashed] = "虚线边框",
    };

    public static string Of(object value) => value != null && Map.TryGetValue(value, out var s) ? s : value?.ToString() ?? "";

    /// <summary>属性面板下拉项。旧版的「四线三格」已取消，不再提供该选项（旧项目加载时会自动迁移）。</summary>
    public static string[] OptionsOf(Type enumType) =>
        Enum.GetValues(enumType).Cast<object>()
            .Where(v => !(enumType == typeof(EssayGridMode) && (EssayGridMode)v == EssayGridMode.FourLine))
            .Select(Of).ToArray();

    public static object ValueOf(Type enumType, string label)
    {
        foreach (var v in Enum.GetValues(enumType).Cast<object>())
            if (Of(v) == label) return v;
        return Enum.GetValues(enumType).GetValue(0);
    }
}

/// <summary>标记可在属性面板编辑的属性。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PropAttribute : Attribute
{
    public string Label { get; }
    public string Group { get; set; } = "常规";
    public string Hint { get; set; } = "";
    public double Min { get; set; } = double.MinValue;
    public double Max { get; set; } = double.MaxValue;
    public bool Multiline { get; set; }
    public bool IsColor { get; set; }
    public string[] Options { get; set; }
    public int Order { get; set; }
    public PropAttribute(string label) { Label = label; }
}
