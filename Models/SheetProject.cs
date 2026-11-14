using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnswerSheetStudio.Models;

public sealed class SheetProject
{
    public int Version { get; set; } = 1;
    public PageSettings Settings { get; set; } = new();

    /// <summary>每张纸正面自动放置的标题 + 考生信息栏模板。</summary>
    public ModuleBase HeaderTemplate { get; set; } = ModuleFactory.Create("info");

    public List<ModuleBase> Modules { get; set; } = new();

    [JsonIgnore] public string FilePath { get; set; } = "";

    [JsonIgnore]
    public string BaseDirectory => string.IsNullOrEmpty(FilePath)
        ? AppDomain.CurrentDomain.BaseDirectory
        : (Path.GetDirectoryName(Path.GetFullPath(FilePath)) ?? AppDomain.CurrentDomain.BaseDirectory);

    public string AssetsDirectory => Path.Combine(BaseDirectory, "assets");

    public static JsonSerializerOptions JsonOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        o.Converters.Add(new ModuleConverter());
        return o;
    }

    public void Save(string path)
    {
        var newPath = Path.GetFullPath(path);
        string oldBase = string.IsNullOrEmpty(FilePath)
            ? AppDomain.CurrentDomain.BaseDirectory
            : (Path.GetDirectoryName(Path.GetFullPath(FilePath)) ?? AppDomain.CurrentDomain.BaseDirectory);
        string newBase = Path.GetDirectoryName(newPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        // 项目换位置时，连同图片素材一起搬迁，保证相对路径仍有效
        if (!string.Equals(oldBase, newBase, StringComparison.OrdinalIgnoreCase))
        {
            var oldAssets = Path.Combine(oldBase, "assets");
            if (Directory.Exists(oldAssets))
            {
                var newAssets = Path.Combine(newBase, "assets");
                Directory.CreateDirectory(newAssets);
                foreach (var file in Directory.GetFiles(oldAssets))
                    File.Copy(file, Path.Combine(newAssets, Path.GetFileName(file)), true);
            }
        }

        FilePath = newPath;
        Directory.CreateDirectory(BaseDirectory);
        var json = JsonSerializer.Serialize(this, typeof(SheetProject), JsonOptions());
        File.WriteAllText(FilePath, json, System.Text.Encoding.UTF8);
    }

    public static SheetProject Load(string path)
    {
        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var p = JsonSerializer.Deserialize<SheetProject>(json, JsonOptions())
                ?? throw new InvalidDataException("项目文件无法解析");
        p.FilePath = Path.GetFullPath(path);
        p.Settings ??= new PageSettings();
        p.Settings.FaceId ??= new FaceIdSettings();
        p.Settings.FillStyle ??= new FillStyleSettings();

        // 旧项目只有一套「共用字号」、没有字符字体。现在字号与字体拆成
        // 「选择题 / 考号区」两套模板：用旧字号初始化两套，字体统一跟随页面字体，
        // 这样打开旧项目时的外观完全不变，之后两处即可分别设置。
        var fill = p.Settings.FillStyle;
        bool hasNewCharTemplates =
            json.Contains("\"ChoiceCharSizeMm\"", StringComparison.Ordinal) ||
            json.Contains("\"CandidateCharSizeMm\"", StringComparison.Ordinal);
        if (!hasNewCharTemplates)
        {
            fill.ChoiceCharSizeMm = fill.CharSizeMm;
            fill.CandidateCharSizeMm = fill.CharSizeMm;
            fill.ChoiceFontFamily = FillStyleSettings.FollowPageFont;
            fill.CandidateFontFamily = FillStyleSettings.FollowPageFont;
        }
        else
        {
            if (fill.ChoiceCharSizeMm <= 0) fill.ChoiceCharSizeMm = fill.CharSizeMm;
            if (fill.CandidateCharSizeMm <= 0) fill.CandidateCharSizeMm = fill.CharSizeMm;
            if (string.IsNullOrWhiteSpace(fill.ChoiceFontFamily))
                fill.ChoiceFontFamily = FillStyleSettings.FollowPageFont;
            if (string.IsNullOrWhiteSpace(fill.CandidateFontFamily))
                fill.CandidateFontFamily = FillStyleSettings.FollowPageFont;
        }

        p.HeaderTemplate ??= ModuleFactory.Create("info");
        p.Modules ??= new List<ModuleBase>();
        // 旧版本解答题没有「每题高度」字段（或写成 null），补成空列表 = 全部跟随统一高度
        EnsureSolutionHeights(p.HeaderTemplate);
        foreach (var m in p.Modules) EnsureSolutionHeights(m);

        // 早期版本作文区有「四线三格」（枚举值 2），现已取消；旧项目统一迁移成英语实线横线
        MigrateEssayMode(p.HeaderTemplate);
        foreach (var m in p.Modules) MigrateEssayMode(m);

        // 旧版本项目没有「填涂尺寸来源」字段。若曾单独调过样式尺寸，自动切到「单独设定」，
        // 免得打开旧项目后这些参数被新的全卷模板静默覆盖。
        if (!json.Contains("\"FillSizeSource\"", StringComparison.Ordinal))
        {
            MigrateLegacyFillSize(p.HeaderTemplate);
            foreach (var m in p.Modules) MigrateLegacyFillSize(m);
        }
        return p;
    }

    private static void EnsureSolutionHeights(ModuleBase m)
    {
        if (m is SolutionModule sol)
        {
            sol.QuestionHeightsMm ??= new List<double>();
            // 旧项目没有「每题分数」字段（或写成 null），补空列表 = 全部跟随统一分数
            sol.QuestionScores ??= new List<double>();
        }
    }

    private static void MigrateEssayMode(ModuleBase m)
    {
        if (m is EssayModule e && e.GridMode == EssayGridMode.FourLine)
            e.GridMode = EssayGridMode.Ruled;
    }

    private static void MigrateLegacyFillSize(ModuleBase m)
    {
        if (m is ChoiceModule c)
        {
            var d = new ChoiceModule();
            if (c.LiteralCharSizeMm != d.LiteralCharSizeMm ||
                c.LiteralBracketHeightMm != d.LiteralBracketHeightMm ||
                c.LiteralBracketArmMm != d.LiteralBracketArmMm ||
                c.LiteralBracketGapMm != d.LiteralBracketGapMm ||
                c.LiteralBracketStrokeMm != d.LiteralBracketStrokeMm ||
                c.LiteralBracketOffsetYMm != d.LiteralBracketOffsetYMm ||
                c.LiteralUseSeparateRightGap ||
                c.LiteralBracketRightGapMm != d.LiteralBracketRightGapMm ||
                c.BracketBoxMm != d.BracketBoxMm)
                c.FillSizeSource = FillSizeSource.Custom;
        }
        else if (m is StudentInfoModule i)
        {
            var d = new StudentInfoModule();
            if (i.CandidateBoxWidthMm != d.CandidateBoxWidthMm ||
                i.CandidateBoxHeightMm != d.CandidateBoxHeightMm ||
                i.CandidateDigitSizeMm != d.CandidateDigitSizeMm ||
                i.CandidateLineStrokeMm != d.CandidateLineStrokeMm ||
                i.CandidateBoxStyle != BracketBoxStyle.Box)
                i.FillSizeSource = FillSizeSource.Custom;
        }
    }

    /// <summary>把外部图片复制进项目 assets 目录，返回相对路径。</summary>
    public string ImportImage(string sourceFile)
    {
        Directory.CreateDirectory(AssetsDirectory);
        var ext = Path.GetExtension(sourceFile);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var name = Guid.NewGuid().ToString("N") + ext;
        var dest = Path.Combine(AssetsDirectory, name);
        File.Copy(sourceFile, dest, true);
        return Path.Combine("assets", name);
    }

    public string ResolveImagePath(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) return "";
        if (Path.IsPathRooted(relativeOrAbsolute)) return relativeOrAbsolute;
        return Path.Combine(BaseDirectory, relativeOrAbsolute);
    }

    public static SheetProject CreateSample()
    {
        var p = new SheetProject();
        var info = (StudentInfoModule)p.HeaderTemplate;
        info.InfoTitle = "考生信息 / 注意事项";
        info.Mode = InfoBarMode.Both;
        info.FieldLabels = "姓名,班级,学校,考号,考场,座位号";
        info.CandidateDigits = 10;
        info.CandidateTitle = "准考证号";
        info.ShowTitle = false;

        p.Modules.Add(new ChoiceModule
        {
            Name = "选择题（单选 1-40）", Title = "一、选择题（每小题只有一个选项符合题意）",
            Kind = ChoiceKind.Single, Display = ChoiceDisplay.Bracket,
            QuestionCount = 40, StartNumber = 1, OptionCount = 4
        });
        p.Modules.Add(new ChoiceModule
        {
            Name = "判断题（41-50）", Title = "二、判断题（正确的填 √，错误的填 ×）",
            Kind = ChoiceKind.Judgement, Display = ChoiceDisplay.Bracket,
            QuestionCount = 10, StartNumber = 41, JudgementLabels = "√,×"
        });
        p.Modules.Add(new BlankModule
        {
            Name = "填空题（51-60）", Title = "三、填空题（请把答案写在横线上）",
            BlankCount = 10, StartNumber = 51, LinesPerBlank = 1, LineHeightMm = 9
        });
        p.Modules.Add(new SolutionModule
        {
            Name = "解答题（61-64）", Title = "四、解答题（写出必要的文字说明与演算步骤）",
            QuestionCount = 4, StartNumber = 61, AreaHeightMm = 52
        });
        return p;
    }
}

public sealed class ModuleConverter : JsonConverter<ModuleBase>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(ModuleBase);

    private static JsonSerializerOptions WithoutConverter(JsonSerializerOptions options)
    {
        var copy = new JsonSerializerOptions(options);
        for (int i = copy.Converters.Count - 1; i >= 0; i--)
            if (copy.Converters[i] is ModuleConverter) copy.Converters.RemoveAt(i);
        return copy;
    }

    public override ModuleBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        string key = root.TryGetProperty("$type", out var t) ? (t.GetString() ?? "custom") : "custom";
        var blank = ModuleFactory.Resolve(key);
        var json = root.GetRawText();
        var result = (ModuleBase)JsonSerializer.Deserialize(json, blank.GetType(), WithoutConverter(options));
        return result ?? blank;
    }

    public override void Write(Utf8JsonWriter writer, ModuleBase value, JsonSerializerOptions options)
    {
        var node = JsonSerializer.SerializeToNode(value, value.GetType(), WithoutConverter(options));
        if (node is null) { writer.WriteNullValue(); return; }
        node["$type"] = value.TypeKey;
        node.WriteTo(writer);
    }
}
