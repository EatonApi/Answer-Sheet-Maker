using System.IO;
using System.Windows;
using AnswerSheetStudio.Models;
using AnswerSheetStudio.UI;

namespace AnswerSheetStudio;

public sealed class App : Application
{
    /// <summary>Main 收到的命令行参数（不同 WPF 版本 e.Args 的填充时机不一致，作为后备）。</summary>
    public static string[] CommandLineArgs { get; set; } = Array.Empty<string>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 引导页是模态窗口，期间不允许「主窗口关闭即退出」提前生效
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        SheetProject initial = null;

        // 命令行直接带项目文件（例如双击 .dtk.json 关联打开）时跳过引导
        var args = (e.Args != null && e.Args.Length > 0) ? e.Args : CommandLineArgs;
        string argFile = args.FirstOrDefault(a =>
            a.EndsWith(".dtk.json", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (argFile != null)
        {
            try
            {
                initial = WelcomeWindow.OpenFromPath(argFile);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("打开失败：" + ex.Message, "启动",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else if (WelcomeStore.ShowWelcome)
        {
            var welcome = new WelcomeWindow();
            MainWindow = welcome;
            bool? ok = welcome.ShowDialog();
            if (ok != true || welcome.ResultProject == null)
            {
                Shutdown();
                return;
            }
            initial = welcome.ResultProject;
        }

        var window = new MainWindow(initial);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }
}

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();
        App.CommandLineArgs = args ?? Array.Empty<string>();
        app.Run();
    }
}
