using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AnswerSheetStudio.Models;
using Microsoft.Win32;

namespace AnswerSheetStudio.UI;

/// <summary>
/// 引导页「新建项目」表单：填标题、选纸张 / 栏数 / 保存位置后直接落盘创建 .dtk.json。
/// 创建逻辑抽成静态方法 CreateProjectFile，便于自动化验证无需弹出真实对话框。
/// </summary>
public sealed class NewProjectDialog : Window
{
    private readonly TextBox _titleBox = new();
    private readonly TextBox _subtitleBox = new();
    private readonly ComboBox _paperBox = new();
    private readonly ComboBox _columnBox = new();
    private readonly TextBox _folderBox = new();
    private readonly TextBox _nameBox = new();
    private readonly CheckBox _sampleBox = new();
    private readonly TextBlock _pathPreview = new();
    private bool _nameEdited;
    private bool _settingName;

    /// <summary>创建成功后的项目（FilePath 已指向新文件）。</summary>
    public SheetProject CreatedProject { get; private set; }
    public string CreatedPath => CreatedProject?.FilePath ?? "";

    public NewProjectDialog()
    {
        Title = "新建答题卡项目";
        Width = 720;
        Height = 700;
        MinWidth = 620;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        _titleBox.Text = "2026 年普通高中学业水平考试";
        _titleBox.FontSize = 14;
        _titleBox.Padding = new Thickness(6);
        _titleBox.TextChanged += (_, _) =>
        {
            if (!_nameEdited)
            {
                _settingName = true;
                _nameBox.Text = SuggestedName(_titleBox.Text);
                _settingName = false;
            }
            UpdatePreview();
        };

        // 副标题：默认带出项目内置的「答题卡 / 科目 / 时间」占位文案，可改成任意多行；清空 = 不显示副标题
        _subtitleBox.Text = DefaultSubtitle;
        _subtitleBox.FontSize = 13;
        _subtitleBox.Padding = new Thickness(6);
        _subtitleBox.AcceptsReturn = true;
        _subtitleBox.TextWrapping = TextWrapping.Wrap;
        _subtitleBox.Height = 52;
        _subtitleBox.VerticalContentAlignment = VerticalAlignment.Center;
        _subtitleBox.TextChanged += (_, _) => UpdatePreview();
        _subtitleBox.ToolTip = "可多行（Enter 换行）；清空则不显示副标题";

        _paperBox.FontSize = 14;
        _paperBox.Padding = new Thickness(6);
        _paperBox.Items.Add(new ComboBoxItem { Content = "A4（210×297，双面，每面 1 栏）", Tag = PaperSize.A4 });
        _paperBox.Items.Add(new ComboBoxItem { Content = "A3 横向（420×297，一面 = 整张纸）", Tag = PaperSize.A3 });
        _paperBox.SelectedIndex = 0;
        _paperBox.SelectionChanged += (_, _) => { SyncColumns(); UpdatePreview(); };

        _columnBox.FontSize = 14;
        _columnBox.Padding = new Thickness(6);
        _columnBox.Items.Add(new ComboBoxItem { Content = "1 栏", Tag = 1 });
        _columnBox.Items.Add(new ComboBoxItem { Content = "2 栏（左右各一张 A4 规格）", Tag = 2 });
        _columnBox.Items.Add(new ComboBoxItem { Content = "3 栏（三等分）", Tag = 3 });
        _columnBox.SelectedIndex = 0;

        string last = WelcomeStore.LastFolder;
        _folderBox.Text = last.Length > 0 ? last
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _folderBox.FontSize = 13;
        _folderBox.Padding = new Thickness(6);
        _folderBox.TextChanged += (_, _) => UpdatePreview();

        _nameBox.Text = SuggestedName(_titleBox.Text);
        _nameBox.FontSize = 13;
        _nameBox.Padding = new Thickness(6);
        _nameBox.TextChanged += (_, _) =>
        {
            if (!_settingName) _nameEdited = true;
            UpdatePreview();
        };

        _sampleBox.Content = "使用示例题型模板（选择题 / 判断题 / 填空题 / 解答题）；取消勾选 = 空白项目，之后自行添加模块";
        _sampleBox.IsChecked = true;
        _sampleBox.Margin = new Thickness(0, 4, 0, 4);

        _pathPreview.FontSize = 12;
        _pathPreview.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x66, 0x99));
        _pathPreview.TextWrapping = TextWrapping.Wrap;

        var root = new DockPanel { Margin = new Thickness(18) };

        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        head.Children.Add(new TextBlock
        {
            Text = "新建答题卡项目", FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x3D))
        });
        head.Children.Add(new TextBlock
        {
            Text = "填写基本信息并选择保存位置，创建后立即打开编辑。",
            FontSize = 12, Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x80))
        });
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var cancel = new Button { Content = "取消", Width = 96, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => { DialogResult = false; };
        var create = new Button
        {
            Content = "创建项目", Width = 130, Height = 32,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x6B, 0xDE)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0)
        };
        create.Click += Create_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(create);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var previewBorder = new Border
        {
            Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF6, 0xFC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xE4, 0xF2)),
            BorderThickness = new Thickness(1),
            Child = _pathPreview
        };
        DockPanel.SetDock(previewBorder, Dock.Bottom);
        root.Children.Add(previewBorder);

        var form = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 7; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddRow(form, 0, "文件标题", _titleBox, "同时作为答题卡主标题与默认文件名");
        AddRow(form, 1, "副标题", _subtitleBox, "显示在主标题下方，可多行；留空则不显示（如「答题卡 科目：____ 考试时间：__ 分钟」）");
        AddRow(form, 2, "纸张类型", _paperBox, "A4 每面 1 栏；A3 是一整张纸，可再分栏");

        var colPanel = new StackPanel { Orientation = Orientation.Horizontal };
        colPanel.Children.Add(_columnBox);
        AddRow(form, 3, "栏数", colPanel, "A4 固定 1 栏；A3 可选 2 / 3 栏");

        var folderPanel = new DockPanel();
        var browse = new Button
        {
            Content = "浏览…", Width = 82, Height = 32, Margin = new Thickness(8, 0, 0, 0)
        };
        browse.Click += Browse_Click;
        DockPanel.SetDock(browse, Dock.Right);
        folderPanel.Children.Add(browse);
        folderPanel.Children.Add(_folderBox);
        AddRow(form, 4, "保存位置", folderPanel, "选择项目文件要保存到的文件夹");

        AddRow(form, 5, "文件名", _nameBox, "可修改；自动补 .dtk.json 后缀");
        AddRow(form, 6, "初始内容", _sampleBox, "");
        root.Children.Add(form);

        Content = root;
        SyncColumns();
        UpdatePreview();
    }

    private static void AddRow(Grid grid, int row, string label, UIElement editor, string hint)
    {
        var lb = new TextBlock
        {
            Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 10, 6)
        };
        Grid.SetRow(lb, row); Grid.SetColumn(lb, 0);
        grid.Children.Add(lb);

        var cell = new StackPanel { Margin = new Thickness(0, 5, 0, 5), VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(editor);
        if (!string.IsNullOrEmpty(hint))
            cell.Children.Add(new TextBlock
            {
                Text = hint, FontSize = 11, Margin = new Thickness(2, 3, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0x9C))
            });
        Grid.SetRow(cell, row); Grid.SetColumn(cell, 1);
        grid.Children.Add(cell);
    }

    private void SyncColumns()
    {
        bool a3 = SelectedPaper() == PaperSize.A3;
        _columnBox.IsEnabled = a3;
        // A4 强制 1 栏；A3 至少 2 栏（与 LayoutEngine 的 Clamp(2,3) 一致）
        foreach (ComboBoxItem item in _columnBox.Items)
        {
            int n = (int)item.Tag;
            item.IsEnabled = a3 ? n >= 2 : n == 1;
        }
        if (!a3) _columnBox.SelectedIndex = 0;
        else if (_columnBox.SelectedIndex < 1) _columnBox.SelectedIndex = 1;
    }

    private PaperSize SelectedPaper() =>
        _paperBox.SelectedItem is ComboBoxItem pi && pi.Tag is PaperSize ps ? ps : PaperSize.A4;

    private int SelectedColumns() =>
        _columnBox.SelectedItem is ComboBoxItem ci && ci.Tag is int n ? n : 1;

    /// <summary>把标题清洗成合法文件名（不含扩展名）。</summary>
    public static string SuggestedName(string title)
    {
        string s = (title ?? "").Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        s = s.Replace('.', '_').Trim('_', ' ');
        if (s.Length > 60) s = s.Substring(0, 60);
        return string.IsNullOrWhiteSpace(s) ? "答题卡" : s;
    }

    /// <summary>最终文件全名（保证 .dtk.json 后缀）。</summary>
    public static string NormalizeFileName(string name)
    {
        string s = (name ?? "").Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        s = s.Trim();
        if (string.IsNullOrWhiteSpace(s)) s = SuggestedName("");
        if (s.EndsWith(".dtk.json", StringComparison.OrdinalIgnoreCase)) return s;
        if (s.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 5);
        return s + ".dtk.json";
    }

    private string FullPath()
    {
        string folder = _folderBox.Text.Trim();
        if (folder.Length == 0) return NormalizeFileName(_nameBox.Text);
        try { return Path.Combine(Path.GetFullPath(folder), NormalizeFileName(_nameBox.Text)); }
        catch { return Path.Combine(folder, NormalizeFileName(_nameBox.Text)); }
    }

    private void UpdatePreview()
    {
        string sub = _subtitleBox.Text.Trim();
        string head = "将创建文件：  " + FullPath();
        _pathPreview.Text = head + (sub.Length == 0
            ? "\n标题预览：  " + _titleBox.Text.Trim() + "（不显示副标题）"
            : "\n标题预览：  " + _titleBox.Text.Trim() + "\n               " + sub.Replace("\r\n", "\n").Replace("\n", " / "));
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "选择项目保存位置",
            InitialDirectory = Directory.Exists(_folderBox.Text.Trim())
                ? _folderBox.Text.Trim()
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog() == true)
        {
            _folderBox.Text = dlg.FolderName;
            UpdatePreview();
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        string title = _titleBox.Text.Trim();
        if (title.Length == 0)
        {
            System.Windows.MessageBox.Show("请填写文件标题。", "新建项目",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        string path = FullPath();
        if (File.Exists(path) &&
            System.Windows.MessageBox.Show("该文件已存在，确定覆盖吗？\n\n" + path,
                "新建项目", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            CreatedProject = CreateProjectFile(title, SelectedPaper(), SelectedColumns(),
                _folderBox.Text.Trim(), _nameBox.Text, _sampleBox.IsChecked == true, path,
                _subtitleBox.Text);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("创建失败：" + ex.Message, "新建项目",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>副标题默认文案（与 PageSettings.Subtitle 默认值一致）。</summary>
    public const string DefaultSubtitle = "答 题 卡    科目：____________    考试时间：______ 分钟";

    /// <summary>
    /// 真正创建并保存项目文件。fullPath 为空时由 folder + fileName 推导。
    /// 纸张 A4 时栏数强制 1，A3 时收敛到 2~3（与排版引擎一致）。
    /// subtitle = null 表示沿用模板自带的副标题（示例模板 / 空白项目默认值）；传空串表示不要副标题。
    /// </summary>
    public static SheetProject CreateProjectFile(string title, PaperSize paper, int columns,
        string folder, string fileName, bool useSample, string fullPath = "", string subtitle = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("项目标题不能为空");
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("请选择保存位置");

        Directory.CreateDirectory(folder);
        string path = string.IsNullOrWhiteSpace(fullPath)
            ? Path.Combine(Path.GetFullPath(folder), NormalizeFileName(fileName))
            : Path.GetFullPath(fullPath);

        var p = useSample ? SheetProject.CreateSample() : new SheetProject();
        p.Settings.Title = title.Trim();
        if (subtitle != null) p.Settings.Subtitle = subtitle.Trim().Length == 0 ? "" : subtitle.Trim();
        p.Settings.Paper = paper;
        p.Settings.Columns = paper == PaperSize.A4 ? 1 : Math.Clamp(columns, 2, 3);
        p.Save(path);

        WelcomeStore.LastFolder = Path.GetDirectoryName(path) ?? folder;
        WelcomeStore.Add(path, p.Settings.Title);
        return p;
    }
}
