using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AnswerSheetStudio.Layout;
using AnswerSheetStudio.Models;
using AnswerSheetStudio.Render;
using Microsoft.Win32;
using ShapeRect = System.Windows.Shapes.Rectangle;

namespace AnswerSheetStudio.UI;

public sealed class MainWindow : Window
{
    private const string BaseTitle = "答题卡制作工具  ·  Answer Sheet Maker";

    /// <summary>
    /// 「是 / 否 / 取消」确认弹窗；默认使用 WPF 消息框。
    /// 抽出这一层便于自动化验证模拟用户选择，也方便以后替换成自定义对话框。
    /// </summary>
    public static Func<string, string, System.Windows.MessageBoxResult> ConfirmDialog =
        (message, title) => System.Windows.MessageBox.Show(message, title,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

    /// <summary>打印不可用时「是否改导出 PDF」的询问；抽出委托便于自动化验证。</summary>
    public static Func<string, string, System.Windows.MessageBoxResult> PrintFallbackDialog =
        (message, title) => System.Windows.MessageBox.Show(message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);

    private SheetProject _project;
    private LayoutResult _layout;
    /// <summary>最近一次「新建 / 打开 / 保存」时的项目快照，用于判断是否有未保存修改。</summary>
    private string _savedSnapshot = "";

    private readonly ListBox _moduleList = new();
    private readonly ScrollViewer _previewScroll = new();
    private readonly StackPanel _pagesPanel = new();
    private readonly ScaleTransform _zoom = new(0.72, 0.72);
    private readonly TextBlock _status = new();
    private readonly TextBlock _statusCenter = new();
    private readonly TextBlock _statusRight = new();
    private readonly TabControl _tabs = new();
    private readonly PropertyEditor _moduleProps = new();
    private readonly PropertyEditor _pageProps = new();
    private readonly PropertyEditor _infoProps = new();
    private readonly PropertyEditor _faceProps = new();
    private readonly PropertyEditor _fillProps = new();
    private readonly Slider _zoomSlider = new();
    private readonly CheckBox _guidesCheck = new();
    private readonly CheckBox _directResizeCheck = new();
    private readonly List<FrameworkElement> _pageItems = new();
    /// <summary>每页的透明交互层，与页面 Canvas 尺寸、坐标系完全一致。</summary>
    private readonly List<(Canvas Overlay, PagePlan Page)> _overlays = new();

    private int _currentPage;
    private int _insertIndex = -1;
    private Point _dragStart;
    private ModuleBase _dragModule;
    private bool _dragging;
    private bool _suppressSelection;
    private string _selectedRegionModuleId = "";
    /// <summary>在预览页精确点中的区域（解答题每题各有独立区域，只按模块 ID 无法区分）。</summary>
    private string _selectedRegionKey = "";
    private ResizeDrag _resize;

    private sealed class ResizeDrag
    {
        public Canvas Overlay;
        public ModuleRegion Region;
        public Point Start;
        public double StartW, StartH, CurrentW, CurrentH;
        public bool ChangeW, ChangeH;
        public ShapeRect Rubber;
        public Border SizeLabel;
        public TextBlock SizeText;
    }

    /// <summary>无参构造：验证程序与直接打开主窗口时使用，自动新建一个示例项目。</summary>
    public MainWindow() : this(null) { }

    /// <summary>带初始项目：由启动引导页（新建 / 打开原有 / 打开历史）创建主窗口。</summary>
    public MainWindow(SheetProject initial)
    {
        Title = BaseTitle;
        Width = 1560;
        Height = 960;
        MinWidth = 1100;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        var dock = new DockPanel { LastChildFill = true };
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        dock.Children.Add(toolbar);
        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        dock.Children.Add(status);
        dock.Children.Add(BuildContent());
        Content = dock;

        WireDragDrop();
        // 关闭窗口（标题栏 × / Alt+F4 / 系统关闭）时，若有未保存修改先询问是否保存
        Closing += (_, e) =>
        {
            if (!IsDirty()) return;
            var r = ConfirmDialog(
                "当前项目有未保存的修改，关闭后会丢失。\n\n是否在关闭前保存？\n" +
                "（是 = 保存后关闭；否 = 不保存直接关闭；取消 = 返回继续编辑）",
                "关闭前保存");
            if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            // 用户取消「另存为」或写盘失败时不能关闭，否则改动会丢失
            if (r == MessageBoxResult.Yes && !SaveCurrentProject(false)) e.Cancel = true;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _resize != null) { CancelResize(); RebuildOverlays(); }
        };
        _moduleProps.ValueChanged += () => RefreshAll(false);
        _pageProps.ValueChanged += () => RefreshAll(false);
        _infoProps.ValueChanged += () => RefreshAll(false);
        _faceProps.ValueChanged += () => RefreshAll(false);
        _fillProps.ValueChanged += () => RefreshAll(false);

        if (initial == null) NewProject();
        else AdoptProject(initial);
    }

    /// <summary>把引导页选好的项目装载进主窗口，并登记到历史记录。</summary>
    private void AdoptProject(SheetProject p)
    {
        _project = p ?? SheetProject.CreateSample();
        _guidesCheck.IsChecked = _project.Settings.ShowGuides;
        RebuildPanels();
        RefreshAll(true);
        MarkSaved();
        if (!string.IsNullOrEmpty(_project.FilePath))
            WelcomeStore.Add(_project.FilePath, _project.Settings.Title);
    }

    /// <summary>工具栏「引导页」：不关闭当前窗口，重新显示启动引导并按需装载项目。</summary>
    private void ShowWelcome()
    {
        var w = new WelcomeWindow { Owner = this };
        if (w.ShowDialog() != true || w.ResultProject == null) return;
        // 只在真的要切换项目时才检查未保存修改，单纯打开看看引导页不影响当前项目
        if (!ConfirmDiscard("切换到其它项目")) return;
        AdoptProject(w.ResultProject);
    }

    private ModuleBase SelectedModule => (_moduleList.SelectedItem as ListBoxItem)?.Tag as ModuleBase;

    // ---------------------------------------------------------------- 界面构造

    private FrameworkElement BuildToolbar()
    {
        var dock = new DockPanel { LastChildFill = false };

        // 顶部菜单栏：浅米色整宽条（对齐参考图 #F9F2E9），左对齐的菜单项，鼠标悬停展开下拉。
        // 使用 WPF 原生 Menu，得到与参考图一致的「整条菜单栏 + 下拉」行为。
        var menu = new Menu
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xF2, 0xE9)),
            Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1B, 0x16)),
            FontSize = 12.5,
            Padding = new Thickness(4, 2, 4, 2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xDA, 0xCC)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        DockPanel.SetDock(menu, Dock.Top);

        // 文件
        var fileMenu = new MenuItem { Header = "文件" };
        var newProj = new MenuItem { Header = "新建项目…" }; newProj.Click += (_, _) => NewProject();
        var openProj = new MenuItem { Header = "打开项目…" }; openProj.Click += (_, _) => OpenProject();
        var saveItem = new MenuItem { Header = "保存(_S)" }; saveItem.Click += (_, _) => SaveProject();
        var saveAsItem = new MenuItem { Header = "另存为…" }; saveAsItem.Click += (_, _) => SaveAs(true);
        fileMenu.Items.Add(newProj);
        fileMenu.Items.Add(openProj);
        fileMenu.Items.Add(new Separator());
        fileMenu.Items.Add(saveItem);
        fileMenu.Items.Add(saveAsItem);
        fileMenu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = "退出" }; exitItem.Click += (_, _) => Close();
        fileMenu.Items.Add(exitItem);
        menu.Items.Add(fileMenu);

        // 编辑
        var editMenu = new MenuItem { Header = "编辑" };
        var warnItem = new MenuItem { Header = "查看排版提示…" }; warnItem.Click += (_, _) => ShowWarnings();
        var resetItem = new MenuItem { Header = "重置所选区域大小" }; resetItem.Click += (_, _) => ResetSelectedRegion();
        editMenu.Items.Add(warnItem);
        editMenu.Items.Add(new Separator());
        editMenu.Items.Add(resetItem);
        menu.Items.Add(editMenu);

        // 视图
        var viewMenu = new MenuItem { Header = "视图" };
        var guidesItem = new MenuItem { Header = "显示栏辅助线（仅预览）", IsCheckable = true, IsChecked = true };
        guidesItem.Click += (_, _) => { if (_project == null) return; _project.Settings.ShowGuides = guidesItem.IsChecked; RefreshAll(false); };
        var resizeItem = new MenuItem { Header = "直接在页面拖拽调整区域大小", IsCheckable = true, IsChecked = true };
        resizeItem.Click += (_, _) => { _directResizeCheck.IsChecked = resizeItem.IsChecked; CancelResize(); RebuildOverlays(); };
        var fitItem = new MenuItem { Header = "适应宽度" }; fitItem.Click += (_, _) => FitWidth();
        var zoomInItem = new MenuItem { Header = "放大" }; zoomInItem.Click += (_, _) => _zoomSlider.Value = Math.Min(_zoomSlider.Maximum, _zoomSlider.Value + 0.1);
        var zoomOutItem = new MenuItem { Header = "缩小" }; zoomOutItem.Click += (_, _) => _zoomSlider.Value = Math.Max(_zoomSlider.Minimum, _zoomSlider.Value - 0.1);
        viewMenu.Items.Add(guidesItem);
        viewMenu.Items.Add(resizeItem);
        viewMenu.Items.Add(new Separator());
        viewMenu.Items.Add(fitItem);
        viewMenu.Items.Add(zoomInItem);
        viewMenu.Items.Add(zoomOutItem);
        menu.Items.Add(viewMenu);

        // 页面
        var pageMenu = new MenuItem { Header = "页面" };
        var prevPage = new MenuItem { Header = "上一页" }; prevPage.Click += (_, _) => GotoPage(_currentPage - 1);
        var nextPage = new MenuItem { Header = "下一页" }; nextPage.Click += (_, _) => GotoPage(_currentPage + 1);
        var welcomeItem = new MenuItem { Header = "返回引导页…" }; welcomeItem.Click += (_, _) => ShowWelcome();
        pageMenu.Items.Add(prevPage);
        pageMenu.Items.Add(nextPage);
        pageMenu.Items.Add(new Separator());
        pageMenu.Items.Add(welcomeItem);
        menu.Items.Add(pageMenu);

        // 输出
        var outMenu = new MenuItem { Header = "输出" };
        var printItem = new MenuItem { Header = "打印…" }; printItem.Click += (_, _) => Print();
        var pdfItem = new MenuItem { Header = "导出 PDF…" }; pdfItem.Click += (_, _) => ExportPdf();
        var pngItem = new MenuItem { Header = "导出 PNG…" }; pngItem.Click += (_, _) => ExportPng();
        outMenu.Items.Add(printItem);
        outMenu.Items.Add(pdfItem);
        outMenu.Items.Add(pngItem);
        menu.Items.Add(outMenu);

        // 帮助
        var helpMenu = new MenuItem { Header = "帮助" };
        var aboutItem = new MenuItem { Header = "关于" };
        aboutItem.Click += (_, _) => System.Windows.MessageBox.Show(
            "答题卡制作工作室\n\nWPF / .NET 10 桌面应用\n毫米级固定版面 · 所见即所得 · 打印 / PDF / PNG 输出\n\n所有文字、颜色、纸张、栏数、定位点与每面标识均可在右侧面板修改。\n系统打印不可用时，请用「导出 PDF…」生成保持真实纸张尺寸的文件。",
            "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        helpMenu.Items.Add(aboutItem);
        menu.Items.Add(helpMenu);

        dock.Children.Add(menu);

        // 工具栏（第二行）：缩放 / 翻页 / 辅助线 / 拖拽 / 输出，全部左对齐、一行排布。
        var bar = new WrapPanel { Margin = new Thickness(8, 5, 8, 5) };
        bar.Children.Add(new TextBlock { Text = "缩放", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _zoomSlider.Minimum = 0.30; _zoomSlider.Maximum = 2.0; _zoomSlider.Value = 0.72;
        _zoomSlider.Width = 150; _zoomSlider.TickFrequency = 0.05; _zoomSlider.IsSnapToTickEnabled = true;
        _zoomSlider.VerticalAlignment = VerticalAlignment.Center;
        _zoomSlider.ValueChanged += (_, e) =>
        {
            _zoom.ScaleX = e.NewValue; _zoom.ScaleY = e.NewValue;
            RebuildOverlays();
        };
        bar.Children.Add(_zoomSlider);
        bar.Children.Add(Btn("适应宽度", (_, _) => FitWidth()));
        bar.Children.Add(Sep());
        bar.Children.Add(Btn("◀ 上一页", (_, _) => GotoPage(_currentPage - 1)));
        bar.Children.Add(Btn("下一页 ▶", (_, _) => GotoPage(_currentPage + 1)));
        bar.Children.Add(Sep());
        _guidesCheck.Content = "显示栏辅助线（仅预览）";
        _guidesCheck.IsChecked = true;
        _guidesCheck.VerticalAlignment = VerticalAlignment.Center;
        _guidesCheck.Margin = new Thickness(0, 0, 6, 0);
        _guidesCheck.Checked += (_, _) => { if (_project == null) return; _project.Settings.ShowGuides = true; RefreshAll(false); };
        _guidesCheck.Unchecked += (_, _) => { if (_project == null) return; _project.Settings.ShowGuides = false; RefreshAll(false); };
        bar.Children.Add(_guidesCheck);
        bar.Children.Add(Sep());
        _directResizeCheck.Content = "直接在页面拖拽调整区域大小";
        _directResizeCheck.IsChecked = true;
        _directResizeCheck.VerticalAlignment = VerticalAlignment.Center;
        _directResizeCheck.Margin = new Thickness(0, 0, 6, 0);
        _directResizeCheck.ToolTip = "开启后：在预览页点击某个区域即可选中它，拖右侧 / 下侧 / 右下角的手柄直接改宽高；解答题可点中每一道题，拖下方手柄单独改该题高度，无需在右侧输入数值。仅影响编辑预览，不改变打印与导出。";
        _directResizeCheck.Checked += (_, _) => { CancelResize(); RebuildOverlays(); };
        _directResizeCheck.Unchecked += (_, _) => { CancelResize(); RebuildOverlays(); };
        bar.Children.Add(_directResizeCheck);
        bar.Children.Add(Btn("重置所选区域大小", (_, _) => ResetSelectedRegion()));
        bar.Children.Add(Sep());
        bar.Children.Add(Btn("打印…", (_, _) => Print()));
        bar.Children.Add(Btn("导出 PDF…", (_, _) => ExportPdf(), true));
        bar.Children.Add(Btn("导出 PNG…", (_, _) => ExportPng()));

        var barBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE2, 0xE7)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar
        };
        DockPanel.SetDock(barBorder, Dock.Top);
        dock.Children.Add(barBorder);

        return dock;
    }

    private FrameworkElement BuildStatusBar()
    {
        // 三部分状态栏（对齐参考图底部状态栏：左 / 中 / 右）
        _status.Margin = new Thickness(8, 0, 8, 0);
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;

        _statusCenter.Margin = new Thickness(8, 0, 8, 0);
        _statusCenter.TextWrapping = TextWrapping.NoWrap;
        _statusCenter.FontSize = 12;
        _statusCenter.VerticalAlignment = VerticalAlignment.Center;
        _statusCenter.HorizontalAlignment = HorizontalAlignment.Center;

        _statusRight.Margin = new Thickness(8, 0, 8, 0);
        _statusRight.TextWrapping = TextWrapping.NoWrap;
        _statusRight.FontSize = 12;
        _statusRight.VerticalAlignment = VerticalAlignment.Center;
        _statusRight.HorizontalAlignment = HorizontalAlignment.Right;

        var grid = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8)),
            Height = 24,
            ShowGridLines = false
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_status, 0);
        Grid.SetColumn(_statusCenter, 1);
        Grid.SetColumn(_statusRight, 2);
        grid.Children.Add(_status);
        grid.Children.Add(_statusCenter);
        grid.Children.Add(_statusRight);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE1, 0xE8)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid
        };
        DockPanel.SetDock(border, Dock.Bottom);
        return border;
    }

    private FrameworkElement BuildContent()
    {
        // 结构（对齐参考图）：模块列表 280 | 分隔 | 预览画布 * | 分隔 | 属性面板 340
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280), MinWidth = 200 });  // 0 模块列表
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });     // 1 分隔
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 }); // 2 画布
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });     // 3 分隔
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340), MinWidth = 260 });  // 4 属性

        var left = BuildLeftPanel();
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var splitter1 = new GridSplitter
        {
            Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE2, 0xE7))
        };
        Grid.SetColumn(splitter1, 1);
        grid.Children.Add(splitter1);

        var center = BuildCenterPanel();
        Grid.SetColumn(center, 2);
        grid.Children.Add(center);

        var splitter2 = new GridSplitter
        {
            Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE2, 0xE7))
        };
        Grid.SetColumn(splitter2, 3);
        grid.Children.Add(splitter2);

        var right = BuildRightPanel();
        Grid.SetColumn(right, 4);
        grid.Children.Add(right);
        return grid;
    }

    private FrameworkElement BuildLeftPanel()
    {
        var dock = new DockPanel { LastChildFill = true };

        var addWrap = new WrapPanel { Margin = new Thickness(4) };
        addWrap.Children.Add(Btn("＋ 选择题", (_, _) => AddModule("choice")));
        addWrap.Children.Add(Btn("＋ 填空题", (_, _) => AddModule("blank")));
        addWrap.Children.Add(Btn("＋ 解答题", (_, _) => AddModule("solution")));
        addWrap.Children.Add(Btn("＋ 作文答题区", (_, _) => AddModule("essay")));
        addWrap.Children.Add(Btn("＋ 非作答区", (_, _) => AddModule("nonanswer")));
        addWrap.Children.Add(Btn("＋ 考生信息栏", (_, _) => AddModule("info")));
        addWrap.Children.Add(Btn("＋ 自定义区", (_, _) => AddModule("custom")));
        addWrap.Children.Add(Btn("＋ 插入图片…", (_, _) => InsertImage()));
        var addBox = new GroupBox { Header = "① 添加模块（均为独立模块）", Content = addWrap, Margin = new Thickness(6, 6, 6, 3) };
        DockPanel.SetDock(addBox, Dock.Top);
        dock.Children.Add(addBox);

        var ctrl = new WrapPanel { Margin = new Thickness(4) };
        ctrl.Children.Add(Btn("复制", (_, _) => CopySelected()));
        ctrl.Children.Add(Btn("删除", (_, _) => DeleteSelected()));
        ctrl.Children.Add(Btn("↑ 上移", (_, _) => MoveSelected(-1)));
        ctrl.Children.Add(Btn("↓ 下移", (_, _) => MoveSelected(1)));
        var ctrlBox = new GroupBox { Header = "② 模块操作（列表内可拖拽排序）", Content = ctrl, Margin = new Thickness(6, 3, 6, 6) };
        DockPanel.SetDock(ctrlBox, Dock.Bottom);
        dock.Children.Add(ctrlBox);

        _moduleList.Margin = new Thickness(6, 3, 6, 3);
        _moduleList.AllowDrop = true;
        _moduleList.SelectionChanged += (_, _) =>
        {
            if (_suppressSelection) return;
            _moduleProps.Build(SelectedModule);
            _tabs.SelectedIndex = 0;
            _selectedRegionModuleId = SelectedModule?.Id ?? "";
            _selectedRegionKey = "";              // 列表里选模块 = 模块级高亮，不再是某一道题
            RebuildOverlays();
        };
        var listBox = new GroupBox { Header = "版面顺序（自上而下、逐面填充）", Content = _moduleList, Margin = new Thickness(6, 3, 6, 3) };
        dock.Children.Add(listBox);
        return dock;
    }

    private FrameworkElement BuildCenterPanel()
    {
        _previewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _previewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _previewScroll.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF8, 0xFA));
        _pagesPanel.Orientation = Orientation.Vertical;
        _pagesPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _pagesPanel.LayoutTransform = _zoom;
        _pagesPanel.Margin = new Thickness(10);
        _previewScroll.Content = _pagesPanel;
        return _previewScroll;
    }

    private FrameworkElement BuildRightPanel()
    {
        _tabs.Margin = new Thickness(4);
        _tabs.Items.Add(new TabItem { Header = "模块属性", Content = WrapScroll(_moduleProps) });
        _tabs.Items.Add(new TabItem { Header = "纸张与版式", Content = WrapScroll(_pageProps) });
        _tabs.Items.Add(new TabItem { Header = "全卷填涂样式", Content = WrapScroll(BuildFillPanel()) });
        _tabs.Items.Add(new TabItem { Header = "页首 / 考生信息", Content = WrapScroll(_infoProps) });
        _tabs.Items.Add(new TabItem { Header = "定位点 / 每面标识", Content = WrapScroll(_faceProps) });
        _tabs.Background = Brushes.White;
        _tabs.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDF, 0xE4));
        _tabs.BorderThickness = new Thickness(1, 1, 0, 0);
        return _tabs;
    }

    private FrameworkElement BuildFillPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "①「全卷填涂模板」是共用的尺寸模板：括号高度、臂长、间距、线宽、方框宽高改一次，选择题 [A] 与考号区 [0] 两处一起变。\n\n" +
                   "②「选择题字符模板」和「考号区字符模板」是两套独立的字号 / 字体模板：字母与数字可以各用不同字号、不同字体，互不影响；" +
                   "字体选「跟随页面字体」时使用「纸张与版式 → 字体」，也可以直接输入本机已安装的字体名。\n\n" +
                   "两者仍可各自选择「方框填涂区」或「字面中括号」样式（模块属性 / 页首考生信息里）；" +
                   "若某一处要换成不同尺寸，把该模块的「填涂尺寸来源」切到「单独设定」即可，此时字号也用模块自己的数值，字体仍由本页两套模板决定。\n\n" +
                   "当框高调得比字符字号还矮时（例如考号区框高 2mm、数字字号 3mm），默认会自动缩小字符，" +
                   "保证 0~9 完整显示且不出框；关掉「字符自动适应框高」则保持设定字号，字符略超出框但同样不会被裁切。",
            Margin = new Thickness(10, 8, 10, 4),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            FontSize = 12
        });
        panel.Children.Add(_fillProps);
        return panel;
    }

    private static ScrollViewer WrapScroll(UIElement content) => new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Content = content,
        Padding = new Thickness(2)
    };

    private static Button Btn(string text, RoutedEventHandler onClick, bool bold = false)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(3, 2, 3, 2),
            Padding = new Thickness(9, 4, 9, 4),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
        };
        b.Click += onClick;
        return b;
    }

    private static FrameworkElement Sep() => new Border
    {
        Width = 1, Height = 22, Margin = new Thickness(6, 0, 6, 0),
        Background = new SolidColorBrush(Color.FromRgb(0xCC, 0xD2, 0xDC))
    };

    // ---------------------------------------------------------------- 项目操作 / 未保存保护

    /// <summary>
    /// 用与「保存」完全相同的序列化结果做项目快照，任何属性 / 模块 / 排序 / 拖拽改动都会体现出来，
    /// 不需要在每个编辑入口手工打标记。「显示栏辅助线」只是编辑器视图状态，不参与比较。
    /// </summary>
    private static string ProjectSnapshot(SheetProject p)
    {
        string json = JsonSerializer.Serialize(p, typeof(SheetProject), SheetProject.JsonOptions());
        var node = JsonNode.Parse(json);
        if (node?["Settings"] is JsonObject settings) settings.Remove("ShowGuides");
        return node?.ToJsonString() ?? json;
    }

    private bool IsDirty()
    {
        if (_project == null || string.IsNullOrEmpty(_savedSnapshot)) return false;
        try { return !string.Equals(ProjectSnapshot(_project), _savedSnapshot, StringComparison.Ordinal); }
        catch { return true; }   // 快照异常时保守处理：宁可多问一次，也不静默丢改动
    }

    private void MarkSaved()
    {
        try { _savedSnapshot = ProjectSnapshot(_project); }
        catch { _savedSnapshot = ""; }
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        string name = string.IsNullOrEmpty(_project?.FilePath)
            ? "未命名项目" : System.IO.Path.GetFileName(_project.FilePath);
        Title = $"{name}{(IsDirty() ? " *" : "")}  ·  {BaseTitle}";
    }

    /// <summary>新建 / 打开其它项目前的确认：返回 false 表示取消本次动作。</summary>
    private bool ConfirmDiscard(string action)
    {
        if (!IsDirty()) return true;
        var r = ConfirmDialog(
            "当前项目有未保存的修改。\n\n" + action + " 会丢弃这些修改，是否先保存？\n" +
            "（是 = 保存后继续；否 = 不保存直接继续；取消 = 留在当前项目）",
            "未保存的修改");
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes && !SaveCurrentProject(true)) return false;
        return true;
    }

    private void NewProject()
    {
        if (!ConfirmDiscard("新建示例")) return;
        _project = SheetProject.CreateSample();
        _guidesCheck.IsChecked = _project.Settings.ShowGuides;
        RebuildPanels();
        RefreshAll(true);
        MarkSaved();   // 全新示例作为基准，用户没改过就不弹保存提示
    }

    private void OpenProject()
    {
        if (!ConfirmDiscard("打开其它项目")) return;
        var dlg = new OpenFileDialog
        {
            Filter = "答题卡项目|*.dtk.json|JSON|*.json|所有文件|*.*",
            Title = "打开答题卡项目"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _project = SheetProject.Load(dlg.FileName);
            _guidesCheck.IsChecked = _project.Settings.ShowGuides;
            RebuildPanels();
            RefreshAll(true);
            MarkSaved();
            WelcomeStore.Add(_project.FilePath, _project.Settings.Title);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("打开失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveProject() => SaveCurrentProject(true);

    /// <summary>返回是否真的保存成功；用户取消「另存为」或写盘失败时返回 false。</summary>
    private bool SaveCurrentProject(bool showSuccess)
    {
        if (string.IsNullOrEmpty(_project.FilePath)) return SaveAs(showSuccess);
        try
        {
            _project.Save(_project.FilePath);
            MarkSaved();
            WelcomeStore.Add(_project.FilePath, _project.Settings.Title);
            if (showSuccess) ShowSaved();
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存失败：" + ex.Message, "保存失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveAs(bool showSuccess)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "答题卡项目|*.dtk.json",
            FileName = "答题卡.dtk.json",
            Title = "另存为"
        };
        if (dlg.ShowDialog() != true) return false;
        try
        {
            _project.Save(dlg.FileName);
            MarkSaved();
            WelcomeStore.Add(_project.FilePath, _project.Settings.Title);
            if (showSuccess) ShowSaved();
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("保存失败：" + ex.Message, "保存失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void ShowSaved() => System.Windows.MessageBox.Show(
        "项目已保存到：\n" + _project.FilePath + "\n\n图片素材保存在同级 assets 目录。",
        "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

    private void RebuildPanels()
    {
        _pageProps.Build(_project.Settings);
        _fillProps.Build(_project.Settings.FillStyle);
        _infoProps.Build(_project.HeaderTemplate);
        _faceProps.Build(_project.Settings.FaceId);
        _moduleProps.Build(SelectedModule);
    }

    private void RefreshAll(bool rebuildList)
    {
        try
        {
            TextMeasurer.ClearCache();
            _layout = LayoutEngine.Build(_project);
            RenderPreview();
            UpdateStatus();
            UpdateWindowTitle();          // 标题栏的「*」随未保存状态实时更新
            if (rebuildList) RebuildModuleList();
            else UpdateListLabels();
        }
        catch (Exception ex)
        {
            _status.Text = "排版失败：" + ex.Message;
            System.Windows.MessageBox.Show("排版失败：\n" + ex, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderPreview()
    {
        _pagesPanel.Children.Clear();
        _pageItems.Clear();
        _overlays.Clear();
        foreach (var page in _layout.Pages)
        {
            var canvas = PageRenderer.BuildCanvas(page, _project, false);
            // 内容层 + 透明交互层共用同一坐标系（毫米 → DIP），拖拽换算才精确
            var sheet = new Grid
            {
                Width = page.WidthMm * Units.D,
                Height = page.HeightMm * Units.D
            };
            sheet.Children.Add(canvas);
            var overlay = new Canvas
            {
                Width = page.WidthMm * Units.D,
                Height = page.HeightMm * Units.D
            };
            overlay.PreviewMouseMove += OverlayMouseMove;
            overlay.PreviewMouseUp += OverlayMouseUp;
            sheet.Children.Add(overlay);
            _overlays.Add((overlay, page));

            var frame = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEC)),
                Padding = new Thickness(9),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = sheet
            };
            var caption = new TextBlock
            {
                Text = $"第 {page.Index + 1} 面（整张纸{(page.IsFront ? "正面" : "反面")}）  ·  第 {page.SheetIndex + 1} 张纸  ·  面内 {page.Faces.Count} 栏",
                Margin = new Thickness(2, 0, 0, 4), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x5F, 0x6D))
            };
            var item = new StackPanel { Margin = new Thickness(0, 0, 0, 20), HorizontalAlignment = HorizontalAlignment.Center };
            item.Children.Add(caption);
            item.Children.Add(frame);
            _pagesPanel.Children.Add(item);
            _pageItems.Add(item);
        }
        if (_currentPage >= _pageItems.Count) _currentPage = Math.Max(0, _pageItems.Count - 1);
        RebuildOverlays();
    }

    // ---------------------------------------------------------------- 页面上直接拖拽调整大小

    private static SolidColorBrush AccentBrush() =>
        new(Color.FromRgb(0x2F, 0x7D, 0xE1));

    private double ScreenPx(double dip) => dip / Math.Max(0.25, _zoom.ScaleX);

    private void RebuildOverlays()
    {
        if (_layout == null) return;
        bool on = _directResizeCheck.IsChecked == true;
        foreach (var (overlay, page) in _overlays)
        {
            overlay.Children.Clear();
            overlay.IsHitTestVisible = on;
            if (!on) continue;

            // 大面积区域先画，小面积（如考号填涂区）后画，保证小区域可以点中
            var regions = _layout.Regions.Where(r => r.PageIndex == page.Index)
                .OrderByDescending(r => r.Bounds.W * r.Bounds.H).ToList();
            foreach (var r in regions) AddRegionHit(overlay, r);

            var selected = regions.Where(IsSelected).ToList();
            // 模块级高亮（左侧列表选中）只画最大的那一个区域，避免解答题每题都套上蓝框；
            // 在页面点中某一题时按 Key 精确选中，只有该题出现手柄。
            if (string.IsNullOrEmpty(_selectedRegionKey) && selected.Count > 1)
                selected = selected.Take(1).ToList();
            var anchor = selected.FirstOrDefault();
            foreach (var r in selected)
                AddRegionSelection(overlay, r, anchor != null && r.Key == anchor.Key);
        }
    }

    private void AddRegionHit(Canvas overlay, ModuleRegion r)
    {
        double D = Units.D;
        var hit = new ShapeRect
        {
            Width = r.Bounds.W * D, Height = r.Bounds.H * D,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(hit, r.Bounds.X * D);
        Canvas.SetTop(hit, r.Bounds.Y * D);
        hit.ToolTip = $"{r.Label}\n当前 {r.Bounds.W:0.0} × {r.Bounds.H:0.0} mm\n" +
                      (r.Kind == ModuleRegionKind.SolutionQuestion
                          ? "点击选中，拖下方蓝色手柄单独改本题高度"
                          : "点击选中，拖蓝色手柄直接改大小");
        hit.PreviewMouseLeftButtonDown += (_, _) => SelectRegion(r);
        hit.MouseEnter += (_, _) =>
        {
            if (!IsSelected(r))
            {
                hit.Stroke = new SolidColorBrush(Color.FromArgb(130, 0x2F, 0x7D, 0xE1));
                hit.StrokeThickness = ScreenPx(1);
                hit.StrokeDashArray = new DoubleCollection { 4, 3 };
            }
        };
        hit.MouseLeave += (_, _) => { if (!IsSelected(r)) hit.Stroke = null; };
        overlay.Children.Add(hit);
    }

    private bool IsSelected(ModuleRegion r) =>
        !string.IsNullOrEmpty(_selectedRegionKey)
            ? r.Key == _selectedRegionKey
            : !string.IsNullOrEmpty(_selectedRegionModuleId) && r.ModuleId == _selectedRegionModuleId;

    private void AddRegionSelection(Canvas overlay, ModuleRegion r, bool withHandles)
    {
        double D = Units.D;
        double x = r.Bounds.X * D, y = r.Bounds.Y * D;
        double w = r.Bounds.W * D, h = r.Bounds.H * D;
        var border = new ShapeRect
        {
            Width = w, Height = h, Stroke = AccentBrush(),
            StrokeThickness = ScreenPx(1.4),
            StrokeDashArray = new DoubleCollection { 5, 3 },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, x); Canvas.SetTop(border, y);
        overlay.Children.Add(border);

        var label = new Border
        {
            Background = AccentBrush(),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(ScreenPx(5), ScreenPx(2), ScreenPx(5), ScreenPx(2)),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = $"{r.Label}   {r.Bounds.W:0.0} × {r.Bounds.H:0.0} mm",
                Foreground = Brushes.White, FontSize = ScreenPx(11)
            }
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, Math.Max(0, y - ScreenPx(20)));
        overlay.Children.Add(label);

        if (!withHandles) return;
        if (r.Kind == ModuleRegionKind.SolutionQuestion)
        {
            // 单题只允许改高度；宽度由整组「区域大小」统一控制
            AddHandle(overlay, r, r.Bounds.X + r.Bounds.W / 2, r.Bounds.Bottom,
                false, true, Cursors.SizeNS, "上下拖动只调整本题答题区高度");
            return;
        }
        AddHandle(overlay, r, r.Bounds.Right, r.Bounds.Y + r.Bounds.H / 2,
            true, false, Cursors.SizeWE, "左右拖动调整宽度");
        AddHandle(overlay, r, r.Bounds.X + r.Bounds.W / 2, r.Bounds.Bottom,
            false, r.CanResizeHeight, Cursors.SizeNS,
            r.CanResizeHeight ? "上下拖动调整高度" : "续排区域：高度跟随第一段，请在第一段调整");
        AddHandle(overlay, r, r.Bounds.Right, r.Bounds.Bottom,
            true, r.CanResizeHeight, Cursors.SizeNWSE, "同时调整宽高");
    }

    private void AddHandle(Canvas overlay, ModuleRegion r, double cxMm, double cyMm,
        bool changeW, bool changeH, Cursor cursor, string tip)
    {
        double D = Units.D, hs = ScreenPx(11);
        var handle = new ShapeRect
        {
            Width = hs, Height = hs,
            Fill = AccentBrush(), Stroke = Brushes.White,
            StrokeThickness = ScreenPx(1.5),
            Cursor = cursor,
            ToolTip = tip,
            IsHitTestVisible = changeW || changeH
        };
        Canvas.SetLeft(handle, cxMm * D - hs / 2);
        Canvas.SetTop(handle, cyMm * D - hs / 2);
        if (changeW || changeH)
            handle.PreviewMouseLeftButtonDown += (_, e) =>
                BeginResize(overlay, r, changeW, changeH, e);
        overlay.Children.Add(handle);
    }

    private void SelectRegion(ModuleRegion r)
    {
        _selectedRegionModuleId = r.ModuleId;
        if (r.Kind == ModuleRegionKind.CandidateGrid &&
            ReferenceEquals(r.Module, _project.HeaderTemplate))
        {
            _infoProps.Build(_project.HeaderTemplate);
            _tabs.SelectedIndex = 3;   // 页首 / 考生信息
        }
        else SelectModule(r.Module);
        // SelectModule 会触发列表 SelectionChanged（清空 Key），所以精确选中要在它之后设置
        _selectedRegionKey = r.Key;
        RebuildOverlays();
        string tip = r.Kind == ModuleRegionKind.SolutionQuestion
            ? "— 拖下方蓝色手柄即可单独改本题高度（其它题不变）"
            : "— 拖右侧 / 下侧 / 右下角蓝色手柄即可直接改大小";
        _status.Text = $"已选中「{r.Label}」：{r.Bounds.W:0.0} × {r.Bounds.H:0.0} mm  " + tip;
    }

    private void BeginResize(Canvas overlay, ModuleRegion r, bool changeW, bool changeH,
        MouseButtonEventArgs e)
    {
        if (_directResizeCheck.IsChecked != true) return;
        CancelResize();
        double D = Units.D;
        var rubber = new ShapeRect
        {
            Width = r.Bounds.W * D, Height = r.Bounds.H * D,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0x2F, 0x7D, 0xE1)),
            Stroke = AccentBrush(), StrokeThickness = ScreenPx(1.6),
            StrokeDashArray = new DoubleCollection { 5, 3 },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rubber, r.Bounds.X * D);
        Canvas.SetTop(rubber, r.Bounds.Y * D);
        overlay.Children.Add(rubber);

        var sizeText = new TextBlock
        {
            Foreground = Brushes.White, FontSize = ScreenPx(11),
            Text = $"{r.Bounds.W:0.0} × {r.Bounds.H:0.0} mm"
        };
        var sizeLabel = new Border
        {
            Background = AccentBrush(), CornerRadius = new CornerRadius(2),
            Padding = new Thickness(ScreenPx(5), ScreenPx(2), ScreenPx(5), ScreenPx(2)),
            IsHitTestVisible = false, Child = sizeText
        };
        Canvas.SetLeft(sizeLabel, (r.Bounds.Right) * D + ScreenPx(4));
        Canvas.SetTop(sizeLabel, r.Bounds.Bottom * D + ScreenPx(4));
        overlay.Children.Add(sizeLabel);

        _resize = new ResizeDrag
        {
            Overlay = overlay, Region = r, Start = e.GetPosition(overlay),
            StartW = r.Bounds.W, StartH = r.Bounds.H,
            CurrentW = r.Bounds.W, CurrentH = r.Bounds.H,
            ChangeW = changeW, ChangeH = changeH, Rubber = rubber,
            SizeLabel = sizeLabel, SizeText = sizeText
        };
        overlay.CaptureMouse();
        e.Handled = true;
    }

    private void OverlayMouseMove(object sender, MouseEventArgs e)
    {
        var d = _resize;
        if (d == null || !ReferenceEquals(d.Overlay, sender)) return;
        var p = e.GetPosition(d.Overlay);
        double w = d.ChangeW
            ? Math.Clamp(d.StartW + Units.Mm(p.X - d.Start.X), d.Region.MinWidthMm, d.Region.MaxWidthMm)
            : d.StartW;
        double h = d.ChangeH
            ? Math.Clamp(d.StartH + Units.Mm(p.Y - d.Start.Y), d.Region.MinHeightMm, d.Region.MaxHeightMm)
            : d.StartH;
        d.CurrentW = w; d.CurrentH = h;
        d.Rubber.Width = w * Units.D;
        d.Rubber.Height = h * Units.D;
        string pct = d.Region.Kind == ModuleRegionKind.Region
            ? $"（{RegionResize.WidthPercent(d.Region, w):0.0}%）" : "";
        _status.Text = $"调整「{d.Region.Label}」：宽 {w:0.0}mm{pct} × 高 {h:0.0}mm — 松开鼠标应用";
        d.SizeText.Text = $"{w:0.0} × {h:0.0} mm{pct}";
        Canvas.SetLeft(d.SizeLabel, Math.Clamp(
            (d.Region.Bounds.X + w) * Units.D + ScreenPx(4), 0,
            Math.Max(0, d.Overlay.Width - ScreenPx(90))));
        Canvas.SetTop(d.SizeLabel, Math.Clamp(
            (d.Region.Bounds.Y + h) * Units.D + ScreenPx(4), 0,
            Math.Max(0, d.Overlay.Height - ScreenPx(24))));
    }

    private void OverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        var d = _resize;
        if (d == null || !ReferenceEquals(d.Overlay, sender)) return;
        _resize = null;
        d.Overlay.ReleaseMouseCapture();
        d.Overlay.Children.Remove(d.Rubber);
        d.Overlay.Children.Remove(d.SizeLabel);
        if (Math.Abs(d.CurrentW - d.StartW) < 0.4 && Math.Abs(d.CurrentH - d.StartH) < 0.4)
        {
            RebuildOverlays();
            return;
        }
        RegionResize.Apply(d.Region, d.CurrentW, d.CurrentH);
        RefreshAll(false);
        if (d.Region.Kind == ModuleRegionKind.SolutionQuestion)
        {
            // 该题框位置随高度变化，按「模块 + 题序号」重新定位选中态，手柄跟着新框走
            var updated = _layout.Regions.FirstOrDefault(x =>
                x.Kind == ModuleRegionKind.SolutionQuestion &&
                x.ModuleId == d.Region.ModuleId && x.ItemIndex == d.Region.ItemIndex);
            _selectedRegionKey = updated?.Key ?? "";
            _selectedRegionModuleId = d.Region.ModuleId;
            RebuildOverlays();
        }
        if (d.Region.Kind == ModuleRegionKind.CandidateGrid &&
            ReferenceEquals(d.Region.Module, _project.HeaderTemplate))
            _infoProps.Build(_project.HeaderTemplate);
        else _moduleProps.Build(SelectedModule);
        _status.Text += d.Region.Kind == ModuleRegionKind.SolutionQuestion
            ? $"   |   ✔ 「{d.Region.Label}」高度已设为 {d.CurrentH:0.0} mm"
            : $"   |   ✔ 「{d.Region.Label}」已设为 {d.CurrentW:0.0} × {d.CurrentH:0.0} mm";
    }

    private void CancelResize()
    {
        if (_resize == null) return;
        var d = _resize;
        _resize = null;
        try
        {
            d.Overlay.ReleaseMouseCapture();
            d.Overlay.Children.Remove(d.Rubber);
            d.Overlay.Children.Remove(d.SizeLabel);
        }
        catch { /* 页面已重建时忽略 */ }
    }

    private void ResetSelectedRegion()
    {
        var all = _layout?.Regions ?? new List<ModuleRegion>();
        var r = (!string.IsNullOrEmpty(_selectedRegionKey)
                    ? all.FirstOrDefault(x => x.Key == _selectedRegionKey) : null)
                ?? all.FirstOrDefault(x => !string.IsNullOrEmpty(_selectedRegionModuleId) &&
                                           x.ModuleId == _selectedRegionModuleId);
        if (r == null)
        {
            System.Windows.MessageBox.Show("请先在预览页上点击一个区域，或在左侧列表选中一个模块。",
                "重置区域大小", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RegionResize.Reset(r);
        _selectedRegionKey = "";             // 恢复默认后区域矩形会变，旧 Key 不再有效
        RefreshAll(false);
        _moduleProps.Build(SelectedModule);
        _infoProps.Build(_project.HeaderTemplate);
        _status.Text += $"   |   ✔ 「{r.Label}」已恢复默认自适应大小";
    }

    private void UpdateStatus()
    {
        string warn = _layout.Warnings.Count > 0
            ? $"   ⚠ {_layout.Warnings.Count} 条提示（点击查看）" : "";
        _status.Text = _layout.Summary + warn + "   |   " +
                       (_directResizeCheck.IsChecked == true
                           ? "预览页：点击区域选中并拖手柄；解答题可逐题拖高度" : "");
        _status.ToolTip = _layout.Warnings.Count > 0 ? string.Join("\n", _layout.Warnings) : "无排版提示";

        int total = _layout.Pages.Count;
        _statusCenter.Text = total > 0
            ? $"预览：第 {_currentPage + 1} / {total} 面"
            : "预览：—";

        string name = string.IsNullOrEmpty(_project.FilePath)
            ? "未保存（尚无文件）" : System.IO.Path.GetFileName(_project.FilePath);
        _statusRight.Text = name + (IsDirty() ? "  ● 有未保存修改" : "  ✔ 已保存");
    }

    private void ShowWarnings()
    {
        var msg = _layout.Warnings.Count == 0
            ? "当前排版无异常提示。"
            : string.Join("\n\n", _layout.Warnings.Select((w, i) => $"{i + 1}. {w}"));
        System.Windows.MessageBox.Show(msg, "排版提示", MessageBoxButton.OK,
            _layout.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    // ---------------------------------------------------------------- 模块列表 / 拖拽

    private void RebuildModuleList()
    {
        var keep = SelectedModule;
        _suppressSelection = true;
        _moduleList.Items.Clear();
        for (int i = 0; i < _project.Modules.Count; i++)
        {
            var m = _project.Modules[i];
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = $"{i + 1}.  {m.Name}", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock
            {
                Text = m.TypeName + "   ·   " + Brief(m), FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x80, 0x8C))
            });
            var item = new ListBoxItem
            {
                Content = stack, Tag = m,
                Padding = new Thickness(6, 4, 6, 4),
                BorderThickness = new Thickness(0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x7D, 0xE1))
            };
            _moduleList.Items.Add(item);
            if (ReferenceEquals(m, keep)) _moduleList.SelectedItem = item;
        }
        _suppressSelection = false;
        if (_moduleList.SelectedItem == null && _moduleList.Items.Count > 0) _moduleList.SelectedIndex = 0;
        _moduleProps.Build(SelectedModule);
    }

    private void UpdateListLabels()
    {
        for (int i = 0; i < _moduleList.Items.Count; i++)
        {
            if (_moduleList.Items[i] is not ListBoxItem li || li.Tag is not ModuleBase m) continue;
            if (li.Content is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock title)
                title.Text = $"{i + 1}.  {m.Name}";
            if (li.Content is StackPanel sp2 && sp2.Children.Count > 1 && sp2.Children[1] is TextBlock sub)
                sub.Text = m.TypeName + "   ·   " + Brief(m);
        }
    }

    private static string Brief(ModuleBase m) => m switch
    {
        ChoiceModule c => c.OptionCounts != null && c.OptionCounts.Any(x => x > 0)
            ? $"{c.QuestionCount} 题 · 选项数逐题自定义 · {EnumText.Of(c.Display)}"
            : $"{c.QuestionCount} 题 × {c.Labels().Length} 选项 · {EnumText.Of(c.Display)}",
        BlankModule b => $"{b.BlankCount} 空 × {b.LinesPerBlank} 行" +
                         (b.BlanksPerRow > 1 ? $" · 每行 {b.BlanksPerRow} 空" : ""),
        SolutionModule s => $"{s.QuestionCount} 题 × {s.AreaHeightMm:0}mm",
        EssayModule e => (e.GridMode == EssayGridMode.Squares
                             ? $"{e.Rows} 行 × {e.Columns} 格 · 方格稿纸"
                             : $"{e.Rows} 行 · 英语实线横线") +
                         $" · 行间隔 {e.RowGapMm:0.#}mm",
        NonAnswerModule n => $"{n.HeightMm:0}mm 非作答区",
        ImageModule i => string.IsNullOrEmpty(i.ImagePath) ? "未选图片" : Path.GetFileName(i.ImagePath),
        StudentInfoModule => "条形码 / 涂考号",
        CustomModule c => $"{c.HeightMm:0}mm 自由区",
        _ => ""
    };

    private void WireDragDrop()
    {
        _moduleList.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(null);
            _dragModule = (FindItem(e.OriginalSource as DependencyObject)?.Tag) as ModuleBase;
        };
        _moduleList.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragModule == null || _dragging) return;
            var p = e.GetPosition(null);
            if (Math.Abs(p.X - _dragStart.X) < 5 && Math.Abs(p.Y - _dragStart.Y) < 5) return;
            _dragging = true;
            DragDrop.DoDragDrop(_moduleList, _dragModule, DragDropEffects.Move);
            _dragging = false;
            _dragModule = null;
            ClearInsertMarkers();
        };
        _moduleList.DragOver += (_, e) =>
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            _insertIndex = ComputeInsertIndex(e.GetPosition(_moduleList));
            ShowInsertMarker(_insertIndex);
        };
        _moduleList.Drop += (_, e) =>
        {
            e.Handled = true;
            var moving = _dragModule;
            if (moving == null) return;
            int target = ComputeInsertIndex(e.GetPosition(_moduleList));
            int from = _project.Modules.IndexOf(moving);
            if (from < 0) return;
            _project.Modules.RemoveAt(from);
            if (target > from) target--;
            target = Math.Clamp(target, 0, _project.Modules.Count);
            _project.Modules.Insert(target, moving);
            _dragModule = null;
            RefreshAll(true);
            SelectModule(moving);
        };
    }

    private static ListBoxItem FindItem(DependencyObject source)
    {
        while (source != null && source is not ListBoxItem)
            source = VisualTreeHelper.GetParent(source);
        return source as ListBoxItem;
    }

    private int ComputeInsertIndex(Point pt)
    {
        var item = FindItem(_moduleList.InputHitTest(pt) as DependencyObject);
        if (item == null) return _moduleList.Items.Count;
        int i = _moduleList.Items.IndexOf(item);
        var rect = item.TransformToAncestor(_moduleList)
            .TransformBounds(new Rect(0, 0, item.ActualWidth, item.ActualHeight));
        return pt.Y > rect.Top + rect.Height / 2 ? i + 1 : i;
    }

    private void ShowInsertMarker(int index)
    {
        for (int i = 0; i < _moduleList.Items.Count; i++)
        {
            if (_moduleList.Items[i] is not ListBoxItem li) continue;
            li.BorderThickness = new Thickness(0);
        }
        if (_moduleList.Items.Count == 0) return;
        int at = Math.Min(index, _moduleList.Items.Count - 1);
        if (_moduleList.Items[at] is ListBoxItem target)
            target.BorderThickness = index >= _moduleList.Items.Count
                ? new Thickness(0, 0, 0, 2)
                : new Thickness(0, 2, 0, 0);
    }

    private void ClearInsertMarkers()
    {
        foreach (var obj in _moduleList.Items)
            if (obj is ListBoxItem li) li.BorderThickness = new Thickness(0);
        _insertIndex = -1;
    }

    private void SelectModule(ModuleBase m)
    {
        foreach (var obj in _moduleList.Items)
            if (obj is ListBoxItem li && ReferenceEquals(li.Tag, m)) { _moduleList.SelectedItem = li; break; }
    }

    private void AddModule(string key)
    {
        var m = ModuleFactory.Create(key);
        _project.Modules.Add(m);
        RefreshAll(true);
        SelectModule(m);
        _tabs.SelectedIndex = 0;
    }

    private void InsertImage()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|所有文件|*.*",
            Title = "选择要插入的图片"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            string rel = _project.ImportImage(dlg.FileName);
            var m = (ImageModule)ModuleFactory.Create("image");
            m.ImagePath = rel;
            m.Name = Path.GetFileNameWithoutExtension(dlg.FileName);
            _project.Modules.Add(m);
            RefreshAll(true);
            SelectModule(m);
            _tabs.SelectedIndex = 0;
        }
        catch (Exception ex) { System.Windows.MessageBox.Show("插入图片失败：" + ex.Message); }
    }

    private void CopySelected()
    {
        var m = SelectedModule;
        if (m == null) return;
        var copy = m.Clone();
        copy.Name = m.Name + "（副本）";
        int i = _project.Modules.IndexOf(m);
        _project.Modules.Insert(i + 1, copy);
        RefreshAll(true);
        SelectModule(copy);
    }

    private void DeleteSelected()
    {
        var m = SelectedModule;
        if (m == null) return;
        _project.Modules.Remove(m);
        RefreshAll(true);
    }

    private void MoveSelected(int delta)
    {
        var m = SelectedModule;
        if (m == null) return;
        int i = _project.Modules.IndexOf(m);
        int j = Math.Clamp(i + delta, 0, _project.Modules.Count - 1);
        if (i == j) return;
        _project.Modules.RemoveAt(i);
        _project.Modules.Insert(j, m);
        RefreshAll(true);
        SelectModule(m);
    }

    // ---------------------------------------------------------------- 预览 / 输出

    private void FitWidth()
    {
        if (_layout == null || _layout.Pages.Count == 0) return;
        double pageDip = _layout.PageWidthMm * Units.D + 30;
        double v = (_previewScroll.ActualWidth - 36) / pageDip;
        v = Math.Clamp(v, 0.30, 2.0);
        _zoomSlider.Value = Math.Round(v / 0.05) * 0.05;
    }

    private void GotoPage(int index)
    {
        if (_pageItems.Count == 0) return;
        _currentPage = Math.Clamp(index, 0, _pageItems.Count - 1);
        _pageItems[_currentPage].BringIntoView();
        if (_layout != null)
            _statusCenter.Text = $"预览：第 {_currentPage + 1} / {_layout.Pages.Count} 面";
    }

    private void Print()
    {
        if (_layout == null || _layout.Pages.Count == 0) return;

        // 系统没有可用打印机时，PrintDialog 会直接抛「该应用不支持打印」之类的错误，
        // 这里先探测一次，避免把用户带进死路，直接引导到不依赖打印机的 PDF 导出。
        bool hasPrinter = false;
        try
        {
            using var server = new System.Printing.PrintServer();
            hasPrinter = server.GetPrintQueues().Any();
        }
        catch { hasPrinter = false; }
        if (!hasPrinter) { OfferPdfExport("当前系统没有检测到可用打印机"); return; }

        try
        {
            var dlg = new PrintDialog { PageRangeSelection = PageRangeSelection.AllPages };
            if (dlg.ShowDialog() != true) return;
            var doc = PageRenderer.CreateDocument(_layout, _project);
            dlg.PrintDocument(doc.DocumentPaginator, "答题卡 - " + _project.Settings.Title);
        }
        catch (Exception ex)
        {
            OfferPdfExport("系统打印调用失败（" + ex.Message + "）");
        }
    }

    /// <summary>打印不可用时给出可执行的替代方案，而不是只报一个错误。</summary>
    private void OfferPdfExport(string reason)
    {
        var r = PrintFallbackDialog(
            reason + "。\n\n是否改为导出 PDF 文件？\n" +
            "PDF 每个印刷面一页，保持 A4 / A3 真实纸张尺寸，用任意 PDF 阅读器即可打印或送印。",
            "无法打印 · 改用 PDF");
        if (r == MessageBoxResult.Yes) ExportPdf();
    }

    private void ExportPdf()
    {
        if (_layout == null || _layout.Pages.Count == 0) return;
        string dir = string.IsNullOrEmpty(_project.FilePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : (Path.GetDirectoryName(_project.FilePath) ?? "");
        string suggest = Path.Combine(dir, SafeFileName(_project.Settings.Title) + ".pdf");

        var opts = new PdfExportDialog(suggest, _layout.Pages.Count) { Owner = this };
        if (opts.ShowDialog() != true) return;
        try
        {
            PdfExporter.Export(_layout, _project, opts.FilePath, opts.Dpi);
            double mb = new FileInfo(opts.FilePath).Length / 1024.0 / 1024.0;
            if (opts.OpenAfterExport)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(opts.FilePath)
                    { UseShellExecute = true });
                }
                catch { /* 没有关联阅读器时忽略，文件仍已生成 */ }
            }
            System.Windows.MessageBox.Show(
                $"已导出 {_layout.Pages.Count} 页 PDF（{opts.Dpi} DPI，约 {mb:0.0} MB）到：\n{opts.FilePath}\n\n" +
                "打印时请选择「实际大小 / 100%」，不要勾选「适应页面」，否则四角定位点会偏移。",
                "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("导出 PDF 失败：" + ex.Message, "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SafeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "答题卡" : s.Trim();
    }

    private void ExportPng()
    {
        if (_layout == null || _layout.Pages.Count == 0) return;
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择导出 PNG 的文件夹",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            PageRenderer.ExportImages(_layout, _project, dlg.SelectedPath, 2);
            System.Windows.MessageBox.Show(
                $"已导出 {_layout.TotalPages} 张 PNG（约 192 DPI）到：\n{dlg.SelectedPath}",
                "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
