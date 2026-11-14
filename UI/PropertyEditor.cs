using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AnswerSheetStudio.Models;

namespace AnswerSheetStudio.UI;

/// <summary>按 [Prop] 特性自动生成属性编辑面板。</summary>
public sealed class PropertyEditor : StackPanel
{
    private static readonly string[] GroupOrder =
    {
        "通用", "区域大小", "常规", "纸张与分页", "标题文字", "版式", "答题区外框", "全卷填涂模板",
        "选择题字符模板", "考号区字符模板", "页眉页脚",
        "四角定位点", "每面唯一标识", "选择题", "选择题排列", "中括号样式", "填空题", "解答题",
        "考生信息", "涂考号区", "考号填涂样式", "自定义区", "图片", "编辑器"
    };

    public event Action ValueChanged;

    private object _target;

    public void Build(object target)
    {
        _target = target;
        Children.Clear();
        if (target is null)
        {
            Children.Add(new TextBlock
            {
                Text = "请在左侧选择一个模块。",
                Margin = new Thickness(10), Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var props = target.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<PropAttribute>() != null)
            .Select(p => (Prop: p, Attr: p.GetCustomAttribute<PropAttribute>()))
            .OrderBy(x => GroupRank(x.Attr.Group))
            .ThenBy(x => x.Attr.Order)
            .ThenBy(x => x.Prop.Name)
            .ToList();

        foreach (var group in props.GroupBy(x => x.Attr.Group))
        {
            var inner = new StackPanel { Margin = new Thickness(6, 4, 6, 8) };
            foreach (var (prop, attr) in group) inner.Children.Add(BuildRow(target, prop, attr));
            Children.Add(new GroupBox
            {
                Header = new TextBlock { Text = group.Key, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 2) },
                Content = inner,
                Margin = new Thickness(6, 4, 6, 6),
                Padding = new Thickness(2)
            });
        }
    }

    private static int GroupRank(string group)
    {
        int i = Array.IndexOf(GroupOrder, group);
        return i < 0 ? 999 : i;
    }

    private UIElement BuildRow(object target, PropertyInfo prop, PropAttribute attr)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = attr.Label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            TextWrapping = TextWrapping.Wrap,
            ToolTip = string.IsNullOrWhiteSpace(attr.Hint) ? null : attr.Hint
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        object value = prop.GetValue(target);
        FrameworkElement editor;
        Type t = prop.PropertyType;

        if (t == typeof(List<double>) && target is SolutionModule sol &&
            prop.Name == nameof(SolutionModule.QuestionHeightsMm))
        {
            editor = BuildSolutionHeights(sol);
        }
        else if (t == typeof(List<double>) && target is SolutionModule solScores &&
            prop.Name == nameof(SolutionModule.QuestionScores))
        {
            editor = BuildScoreListEditor(solScores);
        }
        else if (t == typeof(List<string>) && target is ChoiceModule cm &&
            prop.Name == nameof(ChoiceModule.QuestionNumbers))
        {
            editor = BuildNumberListEditor(cm.QuestionCount, cm.StartNumber,
                cm.CustomNumber, cm.HasCustomNumber, cm.SetQuestionNumber, cm.ClearAllQuestionNumbers);
        }
        else if (t == typeof(List<string>) && target is SolutionModule sm &&
            prop.Name == nameof(SolutionModule.QuestionNumbers))
        {
            editor = BuildNumberListEditor(sm.QuestionCount, sm.StartNumber,
                sm.CustomNumber, sm.HasCustomNumber, sm.SetQuestionNumber, sm.ClearAllQuestionNumbers);
        }
        else if (t == typeof(List<string>) && target is BlankModule bm &&
            prop.Name == nameof(BlankModule.QuestionNumbers))
        {
            editor = BuildNumberListEditor(bm.BlankCount, bm.StartNumber,
                bm.CustomNumber, bm.HasCustomNumber, bm.SetQuestionNumber, bm.ClearAllQuestionNumbers);
        }
        else if (t == typeof(List<int>) && target is ChoiceModule cm2 &&
            prop.Name == nameof(ChoiceModule.OptionCounts))
        {
            editor = BuildIntListEditor(cm2.QuestionCount, cm2.StartNumber,
                cm2.CustomOptionCount, cm2.HasCustomOptionCount, cm2.SetOptionCount, cm2.ClearAllOptionCounts);
        }
        else if (t == typeof(bool))
        {
            var cb = new CheckBox { IsChecked = value is bool b && b, VerticalAlignment = VerticalAlignment.Center };
            cb.Checked += (_, _) => Commit(prop, target, true);
            cb.Unchecked += (_, _) => Commit(prop, target, false);
            editor = cb;
        }
        else if (t.IsEnum)
        {
            var combo = new ComboBox { ItemsSource = EnumText.OptionsOf(t), IsEditable = false };
            combo.SelectedItem = EnumText.Of(value);
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string s) Commit(prop, target, EnumText.ValueOf(t, s));
            };
            editor = combo;
        }
        else if (t == typeof(string) && attr.Options != null && attr.Options.Length > 0)
        {
            var items = attr.Options.ToList();
            string cur = value as string ?? "";
            if (!items.Contains(cur)) items.Insert(0, cur);
            var combo = new ComboBox { ItemsSource = items, IsEditable = true };
            combo.SelectedItem = cur;
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s) Commit(prop, target, s); };
            combo.LostFocus += (_, _) => Commit(prop, target, combo.Text ?? "");
            editor = combo;
        }
        else if (t == typeof(string) && attr.IsColor)
        {
            var box = new ColorBox(value as string ?? "");
            box.ValueChanged += v => Commit(prop, target, v);
            editor = box;
        }
        else if (t == typeof(string))
        {
            var tb = new TextBox
            {
                Text = value as string ?? "",
                AcceptsReturn = attr.Multiline,
                TextWrapping = attr.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = attr.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                Height = attr.Multiline ? 72 : double.NaN,
                VerticalContentAlignment = attr.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center
            };
            tb.LostFocus += (_, _) => Commit(prop, target, tb.Text);
            tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter && !attr.Multiline) Commit(prop, target, tb.Text); };
            editor = tb;
        }
        else // 数值
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
            var tb = new TextBox { Text = text, VerticalContentAlignment = VerticalAlignment.Center };
            tb.LostFocus += (_, _) => CommitNumber(prop, target, attr, tb);
            tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitNumber(prop, target, attr, tb); };
            editor = tb;
        }

        if (!string.IsNullOrWhiteSpace(attr.Hint))
        {
            var row = new StackPanel();
            row.Children.Add(editor);
            row.Children.Add(new TextBlock
            {
                Text = attr.Hint, FontSize = 10, Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.85
            });
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
        }
        else
        {
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
        }
        return grid;
    }

    /// <summary>解答题「每题高度」：按题目数量逐行生成输入框，0 = 跟随统一高度。</summary>
    /// <summary>逐题「分数」：按题目数逐行生成输入框，留空或 0 = 跟随统一分数。</summary>
    private FrameworkElement BuildScoreListEditor(SolutionModule sol)
    {
        var panel = new StackPanel();
        var rows = new StackPanel();
        int count = Math.Clamp(sol.QuestionCount, 1, 200);
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"第 {sol.NumberFor(i)} 题" + (sol.HasCustomScore(i) ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                Foreground = sol.HasCustomScore(i) ? Brushes.DarkBlue : Brushes.Gray
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var tb = new TextBox
            {
                Text = sol.HasCustomScore(i)
                    ? sol.ScoreFor(i).ToString("0.#", CultureInfo.InvariantCulture) : "",
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "本题分数（分）；留空或 0 = 跟随「统一分数」。可填小数"
            };
            void CommitScore()
            {
                string s = tb.Text.Trim();
                double v = 0;
                if (!string.IsNullOrEmpty(s) &&
                    !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = 0;
                sol.SetQuestionScore(idx, v);
                tb.Text = sol.HasCustomScore(idx)
                    ? sol.ScoreFor(idx).ToString("0.#", CultureInfo.InvariantCulture) : "";
                label.Text = $"第 {sol.NumberFor(idx)} 题" + (sol.HasCustomScore(idx) ? " *" : "");
                label.Foreground = sol.HasCustomScore(idx) ? Brushes.DarkBlue : Brushes.Gray;
                ValueChanged?.Invoke();
            }
            tb.LostFocus += (_, _) => CommitScore();
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) CommitScore();
            };
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
            rows.Children.Add(grid);
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = rows, MaxHeight = 230,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var reset = new Button
        {
            Content = "全部恢复统一分数", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 2, 6, 2), FontSize = 11
        };
        reset.Click += (_, _) =>
        {
            sol.ClearAllQuestionScores();
            if (_target != null) Build(_target);
            ValueChanged?.Invoke();
        };
        panel.Children.Add(reset);
        return panel;
    }

    private FrameworkElement BuildSolutionHeights(SolutionModule sol)
    {
        var panel = new StackPanel();
        var rows = new StackPanel();
        int count = Math.Clamp(sol.QuestionCount, 1, 200);
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"第 {sol.StartNumber + i} 题" + (sol.HasCustomHeight(i) ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                Foreground = sol.HasCustomHeight(i) ? Brushes.DarkBlue : Brushes.Gray
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var tb = new TextBox
            {
                Text = sol.HeightFor(i).ToString("0.#", CultureInfo.InvariantCulture),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "本题答题区高度 (mm)；填 0 = 恢复为统一高度"
            };
            void CommitHeight()
            {
                if (!double.TryParse(tb.Text, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double v)) v = 0;
                sol.SetQuestionHeight(idx, v);
                tb.Text = sol.HeightFor(idx).ToString("0.#", CultureInfo.InvariantCulture);
                label.Text = $"第 {sol.StartNumber + idx} 题" + (sol.HasCustomHeight(idx) ? " *" : "");
                label.Foreground = sol.HasCustomHeight(idx) ? Brushes.DarkBlue : Brushes.Gray;
                ValueChanged?.Invoke();
            }
            tb.LostFocus += (_, _) => CommitHeight();
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) CommitHeight();
            };
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
            rows.Children.Add(grid);
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = rows, MaxHeight = 230,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var reset = new Button
        {
            Content = "全部恢复默认高度", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 2, 6, 2), FontSize = 11
        };
        reset.Click += (_, _) =>
        {
            sol.ClearAllQuestionHeights();
            if (_target != null) Build(_target);
            ValueChanged?.Invoke();
        };
        panel.Children.Add(reset);
        return panel;
    }

    /// <summary>逐题「小题号」：按题目 / 空数量逐行生成输入框，留空 = 使用默认「起始题号 + 序号」编号。选择题 / 填空题 / 解答题共用。</summary>
    private FrameworkElement BuildNumberListEditor(
        int count, int startNumber,
        Func<int, string> custom, Func<int, bool> hasCustom,
        Action<int, string> set, Action clearAll)
    {
        var panel = new StackPanel();
        var rows = new StackPanel();
        int n = Math.Clamp(count, 1, 2000);
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"第 {startNumber + i} 题" + (hasCustom(i) ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                Foreground = hasCustom(i) ? Brushes.DarkBlue : Brushes.Gray
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var tb = new TextBox
            {
                Text = custom(i),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "本题的小题号；留空 = 使用默认编号（起始题号 + 序号）。可填任意文字，例如改成不连续的题号或 1-1"
            };
            void CommitNum()
            {
                set(idx, tb.Text);
                tb.Text = custom(idx);
                label.Text = $"第 {startNumber + idx} 题" + (hasCustom(idx) ? " *" : "");
                label.Foreground = hasCustom(idx) ? Brushes.DarkBlue : Brushes.Gray;
                ValueChanged?.Invoke();
            }
            tb.LostFocus += (_, _) => CommitNum();
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) CommitNum();
            };
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
            rows.Children.Add(grid);
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = rows, MaxHeight = 230,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var reset = new Button
        {
            Content = "全部恢复默认编号", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 2, 6, 2), FontSize = 11
        };
        reset.Click += (_, _) =>
        {
            clearAll();
            if (_target != null) Build(_target);
            ValueChanged?.Invoke();
        };
        panel.Children.Add(reset);
        return panel;
    }

    /// <summary>逐题「选项个数」：按题号顺序逐行生成数字输入框，留空 = 使用「选项数量」默认。</summary>
    private FrameworkElement BuildIntListEditor(
        int count, int startNumber,
        Func<int, int> custom, Func<int, bool> hasCustom,
        Action<int, int> set, Action clearAll)
    {
        var panel = new StackPanel();
        var rows = new StackPanel();
        int n = Math.Clamp(count, 1, 2000);
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"第 {startNumber + i} 题" + (hasCustom(i) ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                Foreground = hasCustom(i) ? Brushes.DarkBlue : Brushes.Gray
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            int cur = custom(i);
            var tb = new TextBox
            {
                Text = cur > 0 ? cur.ToString() : "",
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "本题的选项个数；留空 = 使用「选项数量」默认值。范围 1~200；判断题的逐题选项数不生效"
            };
            void CommitOpt()
            {
                string s = tb.Text.Trim();
                int v = 0;
                if (!string.IsNullOrEmpty(s))
                {
                    if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) v = 0;
                    if (v < 0) v = 0;
                    if (v > 200) v = 200;
                }
                set(idx, v);
                int nv = custom(idx);
                tb.Text = nv > 0 ? nv.ToString() : "";
                label.Text = $"第 {startNumber + idx} 题" + (hasCustom(idx) ? " *" : "");
                label.Foreground = hasCustom(idx) ? Brushes.DarkBlue : Brushes.Gray;
                ValueChanged?.Invoke();
            }
            tb.LostFocus += (_, _) => CommitOpt();
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) CommitOpt();
            };
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
            rows.Children.Add(grid);
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = rows, MaxHeight = 230,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var reset = new Button
        {
            Content = "全部恢复默认选项数", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 2, 6, 2), FontSize = 11
        };
        reset.Click += (_, _) =>
        {
            clearAll();
            if (_target != null) Build(_target);
            ValueChanged?.Invoke();
        };
        panel.Children.Add(reset);
        return panel;
    }

    private void CommitNumber(PropertyInfo prop, object target, PropAttribute attr, TextBox tb)
    {
        if (!double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            tb.Text = Convert.ToString(prop.GetValue(target), CultureInfo.InvariantCulture);
            return;
        }
        if (v < attr.Min) v = attr.Min;
        if (v > attr.Max) v = attr.Max;
        tb.Text = v.ToString("0.###", CultureInfo.InvariantCulture);
        object final = prop.PropertyType == typeof(int) ? (object)(int)Math.Round(v) : v;
        Commit(prop, target, final);
    }

    private void Commit(PropertyInfo prop, object target, object value)
    {
        try { prop.SetValue(target, value); }
        catch { return; }
        ValueChanged?.Invoke();
        // 题目数量 / 统一高度变化后，逐题高度列表的行数与默认值要同步刷新
        if (target is SolutionModule &&
            (prop.Name == nameof(SolutionModule.QuestionCount) ||
             prop.Name == nameof(SolutionModule.AreaHeightMm) ||
             prop.Name == nameof(SolutionModule.StartNumber)))
            Build(target);
        // 选择题 / 填空题 / 解答题 的题目数量 / 起始题号变化后，逐题小题号列表的行数与左侧标签要同步刷新
        if (target is ChoiceModule &&
            (prop.Name == nameof(ChoiceModule.QuestionCount) ||
             prop.Name == nameof(ChoiceModule.StartNumber) ||
             prop.Name == nameof(ChoiceModule.OptionCount)))
            Build(target);
        if (target is BlankModule bm2 &&
            (prop.Name == nameof(BlankModule.BlankCount) ||
             prop.Name == nameof(BlankModule.StartNumber)))
            Build(target);
    }
}
