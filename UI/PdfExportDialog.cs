using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AnswerSheetStudio.UI;

/// <summary>
/// PDF 导出的轻量选项窗口（纯代码构建，无 XAML）：选择保存位置、输出精度、是否导出后打开。
/// 不依赖系统打印机，因此「该应用不支持打印」的环境也能拿到可直接付印的文件。
/// </summary>
public sealed class PdfExportDialog : Window
{
    private static readonly int[] DpiValues = { 150, 200, 300, 400, 600 };

    private readonly TextBox _path = new();
    private readonly ComboBox _dpi = new();
    private readonly CheckBox _open = new();

    public string FilePath => _path.Text.Trim();
    public int Dpi => DpiValues[Math.Clamp(_dpi.SelectedIndex, 0, DpiValues.Length - 1)];
    public bool OpenAfterExport => _open.IsChecked == true;

    public PdfExportDialog(string suggestedPath, int pageCount)
    {
        Title = "导出 PDF";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

        root.Children.Add(new TextBlock
        {
            Text = $"共 {pageCount} 个印刷面 → 1 个 PDF（每面一页）",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 6)
        });
        root.Children.Add(new TextBlock
        {
            Text = "每页保持真实纸张尺寸（A4 210×297mm / A3 420×297mm），打印时选「实际大小 / 100%」即可。\n" +
                   "PDF 内为无损图像，中文、字体、四角定位点与填涂框都与预览完全一致。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x55, 0x5C, 0x66)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        root.Children.Add(new TextBlock { Text = "保存位置", Margin = new Thickness(0, 0, 0, 4) });
        var fileRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var browse = new Button
        {
            Content = "浏览…", Width = 74, Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(6, 4, 6, 4)
        };
        DockPanel.SetDock(browse, Dock.Right);
        browse.Click += (_, _) => Browse();
        _path.Text = suggestedPath;
        _path.Height = 26;
        _path.VerticalContentAlignment = VerticalAlignment.Center;
        fileRow.Children.Add(browse);
        fileRow.Children.Add(_path);
        root.Children.Add(fileRow);

        root.Children.Add(new TextBlock { Text = "输出精度", Margin = new Thickness(0, 0, 0, 4) });
        _dpi.ItemsSource = new[]
        {
            "150 DPI · 文件最小，草稿预览",
            "200 DPI · 较小，普通打印",
            "300 DPI · 推荐，印刷 / 阅卷扫描",
            "400 DPI · 更锐利",
            "600 DPI · 最清晰，文件与内存占用最大"
        };
        _dpi.SelectedIndex = 2;
        _dpi.Padding = new Thickness(6, 4, 6, 4);
        _dpi.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(_dpi);

        _open.Content = "导出完成后打开 PDF（可直接用阅读器打印）";
        _open.IsChecked = true;
        _open.Margin = new Thickness(0, 0, 0, 14);
        root.Children.Add(_open);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "导出 PDF", Width = 110, Padding = new Thickness(8, 5, 8, 5), IsDefault = true };
        ok.Click += (_, _) => Confirm();
        var cancel = new Button
        {
            Content = "取消", Width = 76, Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 5, 8, 5), IsCancel = true
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = root;
    }

    private void Browse()
    {
        var dlg = new SaveFileDialog
        {
            Title = "保存 PDF 文件",
            Filter = "PDF 文档|*.pdf|所有文件|*.*",
            FileName = Path.GetFileName(FilePath),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(FilePath))
                ? Path.GetDirectoryName(FilePath) : null,
            OverwritePrompt = false
        };
        if (dlg.ShowDialog(this) == true) _path.Text = dlg.FileName;
    }

    private void Confirm()
    {
        string p = FilePath;
        if (string.IsNullOrWhiteSpace(p))
        {
            System.Windows.MessageBox.Show(this, "请先选择保存位置。", "导出 PDF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            if (!p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) p += ".pdf";
            p = Path.GetFullPath(p);
            string dir = Path.GetDirectoryName(p) ?? "";
            if (dir.Length > 0 && !Directory.Exists(dir))
            {
                System.Windows.MessageBox.Show(this, "目录不存在：\n" + dir, "导出 PDF",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (File.Exists(p) && System.Windows.MessageBox.Show(this,
                    "文件已存在，是否覆盖？\n" + p, "导出 PDF",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _path.Text = p;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, "路径无效：" + ex.Message, "导出 PDF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
