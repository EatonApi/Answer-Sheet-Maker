using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AnswerSheetStudio.UI;

/// <summary>一条最近打开过的项目记录。</summary>
public sealed class RecentEntry
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime LastOpened { get; set; } = DateTime.Now;
}

/// <summary>引导页配置：是否随启动显示、最近项目、上次使用的保存文件夹。</summary>
public sealed class WelcomeConfig
{
    public bool ShowWelcome { get; set; } = true;
    public string LastFolder { get; set; } = "";
    public List<RecentEntry> Recent { get; set; } = new();
}

/// <summary>
/// 引导页设置的本地持久化。默认与程序放在同一目录（<c>welcome.json</c> 挨着 exe，便携、可随文件夹拷走）；
/// 若程序目录不可写（例如装在 Program Files），自动退回 %APPDATA%\AnswerSheetStudio\welcome.json。
/// 读写一律容错（文件损坏 / 无权限时退回默认值，绝不影响主程序启动）。
/// StoragePathOverride 供自动化验证把配置写到临时目录，避免污染真实用户数据。
/// </summary>
public static class WelcomeStore
{
    private const int MaxRecent = 12;
    private const string FileName = "welcome.json";

    private static string _defaultPath;
    private static bool _migrated;

    public static string StoragePathOverride { get; set; } = "";

    /// <summary>配置文件完整路径（默认在程序目录下）。</summary>
    public static string StoragePath => !string.IsNullOrWhiteSpace(StoragePathOverride)
        ? StoragePathOverride
        : _defaultPath ??= ResolveDefaultPath();

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AnswerSheetStudio", FileName);

    private static string ResolveDefaultPath()
    {
        // 首选：应用程序所在目录，跟着 exe 走
        string appDir = AppContext.BaseDirectory;
        if (CanWrite(appDir)) return Path.Combine(appDir, FileName);
        // 回退：程序目录只读（系统保护 / Program Files）时用漫游配置
        return AppDataPath;
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".write_probe_" + Environment.ProcessId);
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    /// <summary>首次切到程序目录时，把旧版 %APPDATA% 里的历史记录搬过来，避免用户历史丢失。
    /// 搬完把旧文件改名 .bak（不删除用户数据），防止「清空历史后又从旧文件复活」。</summary>
    private static void EnsureMigrated(string path)
    {
        if (_migrated) return;
        _migrated = true;
        if (!string.IsNullOrWhiteSpace(StoragePathOverride)) return;
        if (File.Exists(path)) return;
        try
        {
            if (!string.Equals(AppDataPath, path, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(AppDataPath))
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
                if (dir.Length > 0) Directory.CreateDirectory(dir);
                File.Copy(AppDataPath, path, true);
                try { File.Move(AppDataPath, AppDataPath + ".bak", true); } catch { }
            }
        }
        catch { /* 迁移失败就用空配置，不影响使用 */ }
    }

    public static WelcomeConfig Load()
    {
        try
        {
            string path = StoragePath;
            EnsureMigrated(path);
            if (!File.Exists(path)) return new WelcomeConfig();
            var cfg = JsonSerializer.Deserialize<WelcomeConfig>(File.ReadAllText(path, System.Text.Encoding.UTF8));
            cfg ??= new WelcomeConfig();
            cfg.Recent ??= new List<RecentEntry>();
            cfg.LastFolder ??= "";
            return cfg;
        }
        catch { return new WelcomeConfig(); }
    }

    private static void Save(WelcomeConfig cfg)
    {
        try
        {
            string path = StoragePath;
            string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            if (dir.Length > 0) Directory.CreateDirectory(dir);
            File.WriteAllText(path,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // 中文不再转成 \uXXXX，方便用户直接打开文件查看/手工维护
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }),
                // 无 BOM 的 UTF-8，便于记事本等直接编辑
                new System.Text.UTF8Encoding(false));
        }
        catch { /* 配置写不进去不影响使用 */ }
    }

    public static bool ShowWelcome
    {
        get => Load().ShowWelcome;
        set { var c = Load(); c.ShowWelcome = value; Save(c); }
    }

    public static string LastFolder
    {
        get => Load().LastFolder;
        set { var c = Load(); c.LastFolder = value ?? ""; Save(c); }
    }

    /// <summary>最近项目，按最后打开时间倒序。</summary>
    public static List<RecentEntry> Entries() =>
        Load().Recent.OrderByDescending(e => e.LastOpened).ToList();

    /// <summary>新建 / 打开 / 另存为成功后登记一条；同路径去重并置顶。</summary>
    public static void Add(string path, string title)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            string full = Path.GetFullPath(path);
            var c = Load();
            c.Recent ??= new List<RecentEntry>();
            c.Recent.RemoveAll(e => string.Equals(e.Path, full, StringComparison.OrdinalIgnoreCase));
            c.Recent.Insert(0, new RecentEntry
            {
                Path = full,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(full) : title,
                LastOpened = DateTime.Now
            });
            if (c.Recent.Count > MaxRecent) c.Recent = c.Recent.Take(MaxRecent).ToList();
            Save(c);
        }
        catch { /* 历史记录失败不影响项目本身 */ }
    }

    public static void Remove(string path)
    {
        var c = Load();
        c.Recent.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        Save(c);
    }

    public static void Clear()
    {
        var c = Load();
        c.Recent = new List<RecentEntry>();
        Save(c);
    }
}
