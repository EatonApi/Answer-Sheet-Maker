using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AnswerSheetStudio.Models;
using Microsoft.Win32;

namespace AnswerSheetStudio.UI;

/// <summary>
/// 启动引导页：新建项目 / 打开原有项目 / 打开历史项目三个入口，右侧常显最近项目列表。
/// 选择成功后 ResultProject 非空；应用据此创建主窗口。关闭引导页 = 退出程序。
/// </summary>
public sealed class WelcomeWindow : Window
{
    private readonly ListBox _historyList = new();
    private readonly TextBlock _historyHint = new();
    private readonly Button _openSelected = new();
    private readonly Button _removeSelected = new();
    private readonly Button _clearHistory = new();
    private readonly CheckBox _showOnStartup = new();

    /// <summary>本次引导选中的项目；为空表示用户取消 / 关闭引导页。</summary>
    public SheetProject ResultProject { get; private set; }

    public WelcomeWindow()
    {
        Title = "答题卡制作工具 · 开始";
        Width = 980;
        Height = 660;
        MinWidth = 840;
        MinHeight = 560;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        var grid = new Grid { Margin = new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = BuildLeft();
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var right = BuildRight();
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        Content = grid;
        RefreshHistory();
    }

    private FrameworkElement BuildLeft()
    {
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xFD)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF3)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(34, 40, 34, 24)
        };
        var dock = new DockPanel();

        var head = new StackPanel();
        head.Children.Add(new TextBlock
        {
            Text = "答题卡制作工具", FontSize = 30, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x3D))
        });
        head.Children.Add(new TextBlock
        {
            Text = "Answer Sheet Maker", FontSize = 14, Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x9A))
        });
        head.Children.Add(new TextBlock
        {
            Text = "毫米级固定版面 · 模块化题型 · 双面定位点 · 打印 / PDF / PNG",
            FontSize = 12, Margin = new Thickness(0, 16, 0, 0), TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x63, 0x78))
        });
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);

        var foot = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        _showOnStartup.Content = "启动时显示此引导页";
        _showOnStartup.FontSize = 12;
        _showOnStartup.IsChecked = WelcomeStore.ShowWelcome;
        _showOnStartup.Checked += (_, _) => WelcomeStore.ShowWelcome = true;
        _showOnStartup.Unchecked += (_, _) => WelcomeStore.ShowWelcome = false;
        foot.Children.Add(_showOnStartup);
        foot.Children.Add(new TextBlock
        {
            Text = "关闭本窗口将退出程序；也可直接进入空白示例项目。",
            FontSize = 11, Margin = new Thickness(0, 8, 0, 10), TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x92, 0xA0))
        });
        var lowButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var sampleBtn = new Button
        {
            Content = "直接进入示例项目", Width = 150, Height = 30, FontSize = 12,
            Margin = new Thickness(0, 0, 8, 0)
        };
        sampleBtn.Click += (_, _) => Finish(SheetProject.CreateSample());
        var exitBtn = new Button { Content = "退出", Width = 80, Height = 30, FontSize = 12 };
        exitBtn.Click += (_, _) => Close();
        lowButtons.Children.Add(sampleBtn);
        lowButtons.Children.Add(exitBtn);
        foot.Children.Add(lowButtons);
        DockPanel.SetDock(foot, Dock.Bottom);
        dock.Children.Add(foot);

        var actions = new StackPanel { Margin = new Thickness(0, 28, 0, 0) };
        actions.Children.Add(ActionCard("①  新建项目", "填写标题、纸张、栏数并选择保存位置，创建后立即开始编辑",
            New_Click, true));
        actions.Children.Add(ActionCard("②  打开原有项目", "从电脑中选择已有的 .dtk.json 项目文件",
            Open_Click, false));
        actions.Children.Add(ActionCard("③  打开历史项目", "从右侧最近项目列表中选择，或打开最近一次编辑的项目",
            History_Click, false));
        dock.Children.Add(actions);

        panel.Child = dock;
        return panel;
    }

    private static Border ActionCard(string title, string desc, RoutedEventHandler onClick, bool primary)
    {
        var b = new Button
        {
            Height = 88, Margin = new Thickness(0, 0, 0, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 10, 16, 10),
            Background = primary
                ? new SolidColorBrush(Color.FromRgb(0x2F, 0x6B, 0xDE)) : Brushes.White,
            Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x3D)),
            BorderBrush = primary
                ? new SolidColorBrush(Color.FromRgb(0x2F, 0x6B, 0xDE))
                : new SolidColorBrush(Color.FromRgb(0xD8, 0xDF, 0xEA)),
            BorderThickness = new Thickness(1)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.Bold });
        sp.Children.Add(new TextBlock
        {
            Text = desc, FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = primary
                ? new SolidColorBrush(Color.FromRgb(0xDD, 0xE8, 0xFF))
                : new SolidColorBrush(Color.FromRgb(0x70, 0x7A, 0x8A))
        });
        b.Content = sp;
        b.Click += onClick;
        return new Border { Child = b };
    }

    private FrameworkElement BuildRight()
    {
        var dock = new DockPanel { Margin = new Thickness(26, 34, 26, 24) };

        var head = new StackPanel();
        head.Children.Add(new TextBlock
        {
            Text = "最近项目", FontSize = 19, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x3D))
        });
        head.Children.Add(new TextBlock
        {
            Text = "双击列表项可直接打开；也可选中后点下方按钮。",
            FontSize = 11.5, Margin = new Thickness(0, 5, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x9A))
        });
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        _openSelected.Content = "打开所选";
        _openSelected.Width = 100; _openSelected.Height = 30; _openSelected.FontSize = 12;
        _openSelected.Click += (_, _) => OpenSelected();
        _removeSelected.Content = "从列表移除";
        _removeSelected.Width = 100; _removeSelected.Height = 30; _removeSelected.FontSize = 12;
        _removeSelected.Margin = new Thickness(8, 0, 0, 0);
        _removeSelected.Click += (_, _) =>
        {
            if (SelectedPath() is string path)
            {
                WelcomeStore.Remove(path);
                RefreshHistory();
            }
        };
        _clearHistory.Content = "清空历史";
        _clearHistory.Width = 90; _clearHistory.Height = 30; _clearHistory.FontSize = 12;
        _clearHistory.Margin = new Thickness(8, 0, 0, 0);
        _clearHistory.Click += (_, _) =>
        {
            if (System.Windows.MessageBox.Show("确定清空全部历史项目吗？\n（只清除列表，不会删除任何项目文件）",
                    "清空历史", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                WelcomeStore.Clear();
                RefreshHistory();
            }
        };
        tools.Children.Add(_openSelected);
        tools.Children.Add(_removeSelected);
        tools.Children.Add(_clearHistory);
        DockPanel.SetDock(tools, Dock.Bottom);
        dock.Children.Add(tools);

        _historyHint.FontSize = 12;
        _historyHint.TextWrapping = TextWrapping.Wrap;
        _historyHint.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0x9C));
        _historyHint.Margin = new Thickness(0, 8, 0, 0);
        DockPanel.SetDock(_historyHint, Dock.Bottom);
        dock.Children.Add(_historyHint);

        _historyList.FontSize = 12;
        _historyList.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDF, 0xEA));
        _historyList.BorderThickness = new Thickness(1);
        _historyList.Padding = new Thickness(4);
        _historyList.SelectionChanged += (_, _) => SyncButtons();
        _historyList.MouseDoubleClick += (_, _) => OpenSelected();
        dock.Children.Add(_historyList);

        return dock;
    }

    /// <summary>重载最近项目列表（按最后打开时间倒序，文件已移动的项目显示灰色提示）。</summary>
    public void RefreshHistory()
    {
        _historyList.Items.Clear();
        var items = WelcomeStore.Entries();
        foreach (var e in items)
        {
            bool missing = !File.Exists(e.Path);
            var sp = new StackPanel { Margin = new Thickness(4, 6, 4, 6) };
            sp.Children.Add(new TextBlock
            {
                Text = (missing ? "[文件已移动]  " : "") + e.Title,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(missing
                    ? Color.FromRgb(0x99, 0x99, 0x99) : Color.FromRgb(0x1F, 0x2A, 0x3D))
            });
            sp.Children.Add(new TextBlock
            {
                Text = e.Path, FontSize = 10.5, Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x89, 0x98))
            });
            sp.Children.Add(new TextBlock
            {
                Text = "最近打开：" + e.LastOpened.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 10, Margin = new Thickness(0, 2, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA2, 0xAE))
            });
            _historyList.Items.Add(new ListBoxItem { Content = sp, Tag = e.Path, ToolTip = e.Path });
        }
        if (items.Count == 0)
            _historyHint.Text = "暂无历史项目。可点左侧「新建项目」创建，或用「打开原有项目」打开一个已有文件。";
        else
            _historyHint.Text = $"共 {items.Count} 条记录（最多保留 12 条）。";
        if (items.Count > 0) _historyList.SelectedIndex = 0;
        SyncButtons();
    }

    private void SyncButtons()
    {
        bool has = _historyList.SelectedItem != null;
        _openSelected.IsEnabled = has;
        _removeSelected.IsEnabled = has;
        _clearHistory.IsEnabled = _historyList.Items.Count > 0;
    }

    private string SelectedPath() => (_historyList.SelectedItem as ListBoxItem)?.Tag as string;

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewProjectDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedProject != null)
            Finish(dlg.CreatedProject);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "答题卡项目|*.dtk.json|JSON|*.json|所有文件|*.*",
            Title = "打开答题卡项目"
        };
        if (dlg.ShowDialog() != true) return;
        try { Finish(OpenFromPath(dlg.FileName)); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("打开失败：" + ex.Message, "打开项目",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        if (_historyList.Items.Count == 0)
        {
            System.Windows.MessageBox.Show("暂无历史项目。\n请先用「打开原有项目」打开一个项目，或新建一个项目。",
                "打开历史项目", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenSelected();
    }

    private void OpenSelected()
    {
        string path = SelectedPath();
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path))
        {
            var r = System.Windows.MessageBox.Show(
                "该项目文件已不存在（可能被移动或删除）：\n" + path + "\n\n是否从历史列表中移除？",
                "打开历史项目", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes) { WelcomeStore.Remove(path); RefreshHistory(); }
            return;
        }
        try { Finish(OpenFromPath(path)); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("打开失败：" + ex.Message, "打开历史项目",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>加载项目文件并登记到历史记录（引导页与自动化验证共用）。</summary>
    public static SheetProject OpenFromPath(string path)
    {
        var p = SheetProject.Load(path);
        WelcomeStore.Add(p.FilePath, p.Settings.Title);
        return p;
    }

    private void Finish(SheetProject p)
    {
        ResultProject = p;
        DialogResult = true;   // ShowDialog 下设置后会自动关闭窗口
    }
}
