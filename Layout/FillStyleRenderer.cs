using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

/// <summary>
/// 一次排版中实际生效的填涂规格：样式类型（方框 / 字面中括号）+ 全部尺寸。
/// 由「全卷填涂样式」模板或模块自己的参数解析得到，选择题与考号区共用同一套解析与绘制逻辑。
/// </summary>
public sealed class FillStyleSpec
{
    public BracketBoxStyle Style;
    public double CharSizeMm, HeightMm, ArmMm, GapLeftMm, GapRightMm, StrokeMm, OffsetYMm;
    public double BoxWidthMm, BoxHeightMm;
    /// <summary>字号大于填涂框高度时自动缩小字符，保证字符完整可见（渲染层另有不裁切兜底）。</summary>
    public bool FitCharToBox = true;
    /// <summary>本处字符字体设定；空 /「跟随页面字体」时使用页面字体。</summary>
    public string FontFamily = "";

    /// <summary>填涂框线型：true = 虚线。由选择题「选项框线型」/ 考号区「填涂框线型」各自解析。</summary>
    public bool Dashed;

    /// <summary>把字符模板字体解析成真正要用的字体名。</summary>
    public string Font(string pageFamily) =>
        string.IsNullOrWhiteSpace(FontFamily) || FontFamily == FillStyleSettings.FollowPageFont
            ? (string.IsNullOrWhiteSpace(pageFamily) ? "Microsoft YaHei" : pageFamily)
            : FontFamily;

    public static FillStyleSpec FromTemplate(FillStyleSettings t, BracketBoxStyle style) =>
        FromTemplate(t, style, t.CharSizeMm, FillStyleSettings.FollowPageFont);

    public static FillStyleSpec FromTemplate(FillStyleSettings t, BracketBoxStyle style,
        double charSizeMm, string fontFamily, bool dashed = false)
    {
        double h = Math.Max(1, t.HeightMm);
        return new FillStyleSpec
        {
            Style = style,
            CharSizeMm = Math.Max(0.5, charSizeMm),
            FontFamily = string.IsNullOrWhiteSpace(fontFamily)
                ? FillStyleSettings.FollowPageFont : fontFamily,
            HeightMm = h,
            ArmMm = Math.Max(0.2, t.ArmMm),
            GapLeftMm = Math.Max(0, t.GapMm),
            GapRightMm = t.SeparateRightGap ? Math.Max(0, t.RightGapMm) : Math.Max(0, t.GapMm),
            StrokeMm = Math.Max(0.05, t.StrokeMm),
            OffsetYMm = t.OffsetYMm,
            BoxWidthMm = Math.Max(1.0, t.BoxWidthMm),
            BoxHeightMm = t.BoxHeightMm > 0 ? Math.Max(1.0, t.BoxHeightMm) : h,
            FitCharToBox = t.FitCharToBox,
            Dashed = dashed
        };
    }

    public static FillStyleSpec ForChoice(PageSettings s, ChoiceModule m)
    {
        var t = s.FillStyle;
        var style = m.BracketStyle;
        // 尺寸共用一套模板，字符字号与字体用「选择题字符模板」
        if (m.FillSizeSource == FillSizeSource.FollowTemplate)
            return FromTemplate(t, style, t.ChoiceCharSizeMm, t.ChoiceFontFamily,
                m.OptionFramePattern == LinePattern.Dashed);

        double side = Math.Max(1, m.BracketBoxMm);
        return new FillStyleSpec
        {
            Style = style,
            CharSizeMm = Math.Max(0.5, m.LiteralCharSizeMm),
            FontFamily = t.ChoiceFontFamily,
            HeightMm = Math.Max(1, m.LiteralBracketHeightMm),
            ArmMm = Math.Max(0.2, m.LiteralBracketArmMm),
            GapLeftMm = Math.Max(0, m.LiteralBracketGapMm),
            GapRightMm = m.LiteralUseSeparateRightGap
                ? Math.Max(0, m.LiteralBracketRightGapMm) : Math.Max(0, m.LiteralBracketGapMm),
            StrokeMm = Math.Max(0.05, m.LiteralBracketStrokeMm),
            OffsetYMm = m.LiteralBracketOffsetYMm,
            BoxWidthMm = side, BoxHeightMm = side,
            FitCharToBox = s.FillStyle.FitCharToBox,
            Dashed = m.OptionFramePattern == LinePattern.Dashed
        };
    }

    public static FillStyleSpec ForCandidate(PageSettings s, StudentInfoModule m)
    {
        var t = s.FillStyle;
        var style = m.CandidateBoxStyle;
        // 尺寸共用一套模板，字符字号与字体用「考号区字符模板」
        if (m.FillSizeSource == FillSizeSource.FollowTemplate)
            return FromTemplate(t, style, t.CandidateCharSizeMm, t.CandidateFontFamily,
                m.CandidateFramePattern == LinePattern.Dashed);

        return new FillStyleSpec
        {
            Style = style,
            CharSizeMm = Math.Max(0.5, m.CandidateDigitSizeMm),
            FontFamily = t.CandidateFontFamily,
            HeightMm = Math.Max(1, m.CandidateBoxHeightMm),
            ArmMm = Math.Max(0.2, m.CandidateBracketArmMm),
            GapLeftMm = Math.Max(0, m.CandidateBracketGapMm),
            GapRightMm = m.CandidateSeparateRightGap
                ? Math.Max(0, m.CandidateBracketRightGapMm) : Math.Max(0, m.CandidateBracketGapMm),
            StrokeMm = Math.Max(0.05, m.CandidateLineStrokeMm),
            OffsetYMm = m.CandidateBracketOffsetYMm,
            BoxWidthMm = Math.Max(1, m.CandidateBoxWidthMm),
            BoxHeightMm = Math.Max(1, m.CandidateBoxHeightMm),
            FitCharToBox = s.FillStyle.FitCharToBox,
            Dashed = m.CandidateFramePattern == LinePattern.Dashed
        };
    }

    /// <summary>整块等比缩放（选择题溢出缩放、考号区整体大小 / 指定总高都走这里）。</summary>
    public FillStyleSpec Scaled(double k)
    {
        var c = (FillStyleSpec)MemberwiseClone();
        c.CharSizeMm *= k; c.HeightMm *= k; c.ArmMm *= k;
        c.GapLeftMm *= k; c.GapRightMm *= k; c.StrokeMm = Math.Max(0.05, c.StrokeMm * k);
        c.OffsetYMm *= k; c.BoxWidthMm *= k; c.BoxHeightMm *= k;
        return c;
    }

    /// <summary>本次绘制是否要因为「框高小于字号所需行高」而自动缩小字符。</summary>
    public bool NeedsAutoFit(double boxH) =>
        FitCharToBox && CharSizeMm * 1.35 > boxH + 0.01;

    /// <summary>
    /// 实际绘制用的字号：框高 / 可用宽度不足以容纳设定字号时等比缩小字符，
    /// 避免 WPF TextBlock 因高度小于行高把数字上下裁掉（只显示一条横线）。
    /// 关闭「字符自动适应框高」时返回原字号，由渲染层扩大文本框保证字符仍完整显示。
    /// </summary>
    public double EffectiveCharSize(string label, double textW, double boxH, string family)
    {
        family = Font(family);
        double fs = Math.Max(0.5, CharSizeMm);
        if (!FitCharToBox) return fs;
        if (boxH > 0.5 && fs * 1.35 > boxH) fs = boxH / 1.35;
        double needW = TextMeasurer.Width(label, fs, family);
        double maxW = Math.Max(0.5, textW - 0.2);
        if (needW > maxW && needW > 0.01) fs *= maxW / needW;
        return Math.Max(0.5, fs);
    }

    /// <summary>单个填涂单元（一个 [A] 或一个 [0]）的真实占用宽度。</summary>
    public double UnitWidth(string label, string family)
    {
        double cw = TextMeasurer.Width(label, CharSizeMm, Font(family));
        return Style == BracketBoxStyle.Literal
            ? 2 * ArmMm + GapLeftMm + GapRightMm + cw
            : Math.Max(BoxWidthMm, cw + 1.4);
    }

    public double UnitHeight => Style == BracketBoxStyle.Literal ? HeightMm : BoxHeightMm;
}

/// <summary>把规格画成绘制指令；(x, y, w, h) 是单个填涂单元的外框矩形。</summary>
public static class FillStyleRenderer
{
    public static void Draw(List<DrawCmd> cmds, double x, double y, double w, double h,
        string label, FillStyleSpec spec, string family, string textColor, string boxColor)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        double stroke = Math.Max(0.05, spec.StrokeMm);
        // 字符字体由「选择题 / 考号区字符模板」各自决定；页面字体只作为「跟随」时的兜底
        string charFamily = spec.Font(family);

        if (spec.Style == BracketBoxStyle.Box)
        {
            cmds.Add(DrawCmd.Rect(x, y, w, h, boxColor, "", stroke, dashed: spec.Dashed));
            cmds.Add(DrawCmd.Text(x, y, w, h, label,
                spec.EffectiveCharSize(label, w, h, charFamily), charFamily, textColor,
                TextAlign.Center, VAlign.Middle));
            return;
        }

        // 字面中括号：用线段画 [ 与 ]，字符单独绘制，因此每个尺寸都可调
        double arm = Math.Min(spec.ArmMm, w * 0.30);
        double gl = spec.GapLeftMm, gr = spec.GapRightMm;
        double need = 2 * arm + gl + gr + 1.0;
        if (need > w)
        {
            double k = Math.Max(0.02, (w - 1.0) / Math.Max(0.1, 2 * arm + gl + gr));
            arm *= k; gl *= k; gr *= k;
        }
        double cw = Math.Max(1, w - 2 * arm - gl - gr);

        cmds.Add(DrawCmd.Line(x, y, x + arm, y, boxColor, stroke, spec.Dashed));
        cmds.Add(DrawCmd.Line(x, y, x, y + h, boxColor, stroke, spec.Dashed));
        cmds.Add(DrawCmd.Line(x, y + h, x + arm, y + h, boxColor, stroke, spec.Dashed));

        cmds.Add(DrawCmd.Text(x + arm + gl, y, cw, h, label,
            spec.EffectiveCharSize(label, cw, h, charFamily), charFamily, textColor,
            TextAlign.Center, VAlign.Middle));

        double rx = x + w - arm;
        cmds.Add(DrawCmd.Line(rx, y, x + w, y, boxColor, stroke, spec.Dashed));
        cmds.Add(DrawCmd.Line(x + w, y, x + w, y + h, boxColor, stroke, spec.Dashed));
        cmds.Add(DrawCmd.Line(rx, y + h, x + w, y + h, boxColor, stroke, spec.Dashed));
    }
}
