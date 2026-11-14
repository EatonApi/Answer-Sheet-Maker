using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.Layout;

public enum ModuleRegionKind
{
    /// <summary>模块区域（通用「区域大小」参数）。</summary>
    Region,
    /// <summary>考号填涂网格（「涂考号区」指定宽 / 指定总高参数）。</summary>
    CandidateGrid,
    /// <summary>解答题某一题的答题区（只调该题高度）。</summary>
    SolutionQuestion
}

/// <summary>
/// 一次排版中，页面上可以直接拖动的区域。坐标是「整张纸一面」的毫米坐标，
/// 与预览 Canvas 只差一个 Units.D 换算，因此拖拽可做到所见即所得。
/// </summary>
public sealed class ModuleRegion
{
    public ModuleRegionKind Kind;
    public string ModuleId = "";
    public ModuleBase Module;
    public string Label = "";
    public int PageIndex, Column;
    public RectMm Bounds;

    /// <summary>区域宽度百分比的换算基准（模块区域=栏内容宽；考号区=信息栏内宽）。</summary>
    public double BaseWidthMm;
    /// <summary>栏内容区左边界，用于把拖拽后的左边界换算成水平偏移。</summary>
    public double ContentX;
    public double MinWidthMm = 2, MaxWidthMm = 1000;
    public double MinHeightMm = 8, MaxHeightMm = 400;
    /// <summary>指定区域高度只对模块的第一段生效；续排段只显示、只调宽度。</summary>
    public bool IsFirstSegment;
    public bool CanResizeHeight;
    /// <summary>区域内第几项（解答题 = 0 基题序号）；其它区域为 -1。</summary>
    public int ItemIndex = -1;

    public string Key => $"{Kind}|{ModuleId}|{ItemIndex}|{PageIndex}|{Column}|" +
                         $"{Bounds.X:0.0}|{Bounds.Y:0.0}";
}

/// <summary>
/// 把页面上拖拽出来的毫米尺寸换算回模块属性。UI 与验证脚本共用同一套逻辑，
/// 保证「拖多少、实际就变多少」。
/// </summary>
public static class RegionResize
{
    public static double WidthPercent(ModuleRegion r, double wMm) =>
        r.Kind == ModuleRegionKind.Region
            ? Math.Clamp(wMm / Math.Max(1, r.BaseWidthMm) * 100.0, 20, 100)
            : Math.Clamp(wMm / Math.Max(1, r.BaseWidthMm) * 100.0, 0, 100);

    public static void Apply(ModuleRegion r, double wMm, double hMm)
    {
        double w = Math.Clamp(wMm, r.MinWidthMm, r.MaxWidthMm);
        double h = Math.Clamp(hMm, r.MinHeightMm, r.MaxHeightMm);
        if (r.Kind == ModuleRegionKind.CandidateGrid)
        {
            if (r.Module is not StudentInfoModule info) return;
            // 指定宽度 / 指定总高是绝对值，覆盖百分比与整体大小，所见即所得
            info.CandidateAreaWidthMm = Math.Round(w, 1);
            info.CandidateAreaHeightMm = Math.Round(h, 1);
            return;
        }
        if (r.Kind == ModuleRegionKind.SolutionQuestion)
        {
            // 单题只改高度；宽度仍由整组「区域大小」统一控制
            if (r.Module is SolutionModule sol) sol.SetQuestionHeight(r.ItemIndex, h);
            return;
        }
        var m = r.Module;
        if (m == null) return;
        m.RegionWidthPercent = Math.Round(Math.Clamp(WidthPercent(r, w), 20, 100), 1);
        // 拖拽以左上角为锚点：把当前左边界记成水平偏移，避免变宽后区域被对齐方式挪走
        m.RegionOffsetMm = Math.Round(Math.Clamp(r.Bounds.X - r.ContentX,
            0, Math.Max(0, r.BaseWidthMm - w)), 1);
        m.RegionAlign = RegionAlign.Left;
        if (r.CanResizeHeight)
        {
            m.RegionHeightMm = Math.Round(h, 1);
            m.RegionMinHeightMm = 0;
        }
    }

    /// <summary>把所选区域恢复成默认自适应大小。</summary>
    public static void Reset(ModuleRegion r)
    {
        if (r.Kind == ModuleRegionKind.CandidateGrid && r.Module is StudentInfoModule info)
        {
            info.CandidateAreaWidthMm = 0;
            info.CandidateAreaHeightMm = 0;
            info.CandidateScalePercent = 100;
        }
        else if (r.Kind == ModuleRegionKind.SolutionQuestion && r.Module is SolutionModule sol)
        {
            sol.ClearQuestionHeight(r.ItemIndex);
        }
        else if (r.Module != null)
        {
            r.Module.RegionWidthPercent = 100;
            r.Module.RegionOffsetMm = 0;
            r.Module.RegionHeightMm = 0;
            r.Module.RegionMinHeightMm = 0;
        }
    }
}
