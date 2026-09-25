using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.Tui {
    /// <summary>
    /// 菜单项动作类型。
    /// </summary>
    public enum TuiMenuAction {
        Select,
        Back,
        Build,
        Clear,
        Inspect,
        Reload,
        Save,
        Search,
        Preview,
        Preset,
        Sample
    }

    /// <summary>
    /// TUI 菜单项定义，支持显式覆写标记、独立返回功能与自适应富文本渲染。
    /// </summary>
    public sealed class TuiMenuItem {
        public static readonly TuiMenuItem BackSentinel = new(
            "__BACK__",
            null!,
            action: TuiMenuAction.Back
        );

        public static readonly TuiMenuItem ConfirmSentinel = new(
            "__CONFIRM__",
            null!,
            action: TuiMenuAction.Build
        );

        public static readonly TuiMenuItem DeleteSentinel = new(
            "__DELETE__",
            null!,
            action: TuiMenuAction.Clear
        );

        private readonly string? _fixedTitle;

        public string Id { get; }
        public string Title => _fixedTitle ?? (Id switch {
            "__BACK__" => Core.Localization.I18n.T("Menu.Back", "返回上级"),
            "__CONFIRM__" => Core.Localization.I18n.T("Menu.ConfirmBuild", "确认配置并开始构建"),
            "__DELETE__" => Core.Localization.I18n.T("Menu.ClearCurrent", "清空当前项"),
            _ => string.Empty
        });
        public string? Details { get; }
        public bool IsOverridden { get; }
        public TuiMenuAction Action { get; }
        public object? Tag { get; }

        public bool IsBack => Action == TuiMenuAction.Back;
        public bool IsBuild => Action == TuiMenuAction.Build;
        public bool IsClear => Action == TuiMenuAction.Clear;
        public bool IsReload => Action == TuiMenuAction.Reload;
        public bool IsSave => Action == TuiMenuAction.Save;
        public bool IsSearch => Action == TuiMenuAction.Search;
        public bool IsPreview => Action == TuiMenuAction.Preview;
        public bool IsPreset => Action == TuiMenuAction.Preset;
        public bool IsSample => Action == TuiMenuAction.Sample;

        public TuiMenuItem(
            string id,
            string title,
            string? details = null,
            bool isOverridden = false,
            TuiMenuAction action = TuiMenuAction.Select,
            object? tag = null
        ) {
            Id = id;
            _fixedTitle = title;
            Details = details;
            IsOverridden = isOverridden;
            Action = action;
            Tag = tag;
        }
    }

    /// <summary>
    /// TUI 交互式选择菜单：
    /// 1. 支持上下箭头、PageUp/PageDown、Home/End 滚动选择；
    /// 2. 支持 Spacebar 与 RightArrow (→) 进入/选择当前高亮项；
    /// 3. 支持 Enter 触发整体全局确认（如确认配置并直接开始构建）；
    /// 4. 支持 Delete 快捷键清空配置项；
    /// 5. 支持 LeftArrow (←) 与 Escape 替代返回；
    /// 6. 有覆盖的配置选项使用柔和暖琥珀色标记；
    /// 7. 底部呈现人性化操作引导提示。
    /// </summary>
    public static class TuiMenu {
        static TuiMenu() {
            TerminalWidthRegistry.Register("Menu", 60);
        }

        public static TuiMenuItem Prompt(
            string titleMarkup,
            IReadOnlyList<TuiMenuItem> items,
            int pageSize = 14,
            int defaultIndex = 0,
            string? initialSelectedId = null,
            TuiMenuItem? onEnterAction = null,
            TuiMenuItem? onDeleteAction = null,
            string? customHelp = null
        ) {
            if (items == null || items.Count == 0) {
                return TuiMenuItem.BackSentinel;
            }

            if (!string.IsNullOrEmpty(initialSelectedId)) {
                for (int i = 0; i < items.Count; i++) {
                    if (string.Equals(items[i].Id, initialSelectedId, StringComparison.OrdinalIgnoreCase)) {
                        defaultIndex = i;
                        break;
                    }
                }
            }

            if (Console.IsInputRedirected || !TuiEnvironment.SupportsInteractiveUi()) {
                return items[Math.Clamp(defaultIndex, 0, items.Count - 1)];
            }

            var selectedIndex = Math.Clamp(defaultIndex, 0, items.Count - 1);
            TuiMenuItem? chosen = null;

            var defaultHelp = onEnterAction != null
                ? (onDeleteAction != null
                    ? Core.Localization.I18n.T("Controls.Full", "[grey]操作：[[↑/↓, jk]] 移动  [[Space / → / l]] 编辑  [[Del]] 清空  [[Enter]] 确认构建  [[← / Esc / h]] 返回[/]")
                    : Core.Localization.I18n.T("Controls.Navigate", "[grey]操作：[[↑/↓, jk]] 移动  [[Space / → / l]] 进入  [[Enter]] 确认构建  [[← / Esc / h]] 返回[/]"))
                : (onDeleteAction != null
                    ? Core.Localization.I18n.T("Controls.SelectWithDel", "[grey]操作：[[↑/↓, jk]] 移动  [[Space / Enter / l]] 确认  [[Del]] 清空  [[← / Esc / h]] 返回[/]")
                    : Core.Localization.I18n.T("Controls.SelectSimple", "[grey]操作：[[↑/↓, jk]] 移动  [[Space / Enter / l]] 确认  [[← / Esc / h]] 返回[/]"));
            var effectiveHelp = customHelp ?? defaultHelp;

            AnsiConsole.Live(RenderMenu(titleMarkup, items, selectedIndex, 0, int.MaxValue, effectiveHelp))
                .AutoClear(true)
                .Start(ctx => {
                    while (chosen == null) {
                        var requiredWidth = TerminalWidthRegistry.GetRequiredWidth();
                        var currentWidth = TuiEnvironment.SafeWindowWidth();

                        if (currentWidth < requiredWidth) {
                            // 终端宽度过小：直接显示窗口过小，不显示内容，并实时检测并提示当前终端宽度和要求的宽度
                            ctx.UpdateTarget(TerminalWidthRegistry.RenderTooSmallWarning(currentWidth, requiredWidth));
                            ctx.Refresh();

                            while (!Console.KeyAvailable) {
                                var newWidth = TuiEnvironment.SafeWindowWidth();
                                if (newWidth != currentWidth) {
                                    break;
                                }
                                Thread.Sleep(50);
                            }

                            if (Console.KeyAvailable) {
                                try { Console.ReadKey(true); } catch { }
                            }
                            continue;
                        }

                        ctx.UpdateTarget(RenderMenu(titleMarkup, items, selectedIndex, 0, int.MaxValue, effectiveHelp));
                        ctx.Refresh();

                        while (!Console.KeyAvailable && chosen == null) {
                            var checkWidth = TuiEnvironment.SafeWindowWidth();
                            if (checkWidth != currentWidth) {
                                break;
                            }
                            Thread.Sleep(30);
                        }

                        if (!Console.KeyAvailable) {
                            continue;
                        }

                        ConsoleKeyInfo key;
                        try {
                            key = Console.ReadKey(true);
                        } catch {
                            chosen = items[selectedIndex];
                            break;
                        }

                        var (newIndex, actionItem) = ProcessKey(key, items, selectedIndex, onEnterAction, onDeleteAction);
                        selectedIndex = newIndex;
                        if (actionItem != null) {
                            chosen = actionItem;
                            break;
                        }
                    }
                });

            var result = chosen ?? items[selectedIndex];

            // 渲染简洁选中结果，保留操作轨迹
            var actionTag = result.IsBack
                ? $"[grey]{Core.Localization.I18n.T("Badge.Back", "返回")}[/]"
                : (result.IsOverridden ? $"[bold {TuiTheme.Warning}]{Core.Localization.I18n.T("Badge.Modified", "已修改项")}[/]" : $"[grey]{Core.Localization.I18n.T("Badge.Select", "选择")}[/]");
            AnsiConsole.MarkupLine($"[grey]✔ {actionTag}：[/]{Markup.Escape(result.Title)}");

            return result;
        }

        internal static (int NewIndex, TuiMenuItem? Chosen) ProcessKey(
            ConsoleKeyInfo key,
            IReadOnlyList<TuiMenuItem> items,
            int selectedIndex,
            TuiMenuItem? onEnterAction = null,
            TuiMenuItem? onDeleteAction = null
        ) {
            bool noModifiers = (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0;

            // 1. 向上移动：UpArrow 或 Vim k
            if (key.Key == ConsoleKey.UpArrow || (noModifiers && (key.Key == ConsoleKey.K || key.KeyChar is 'k' or 'K'))) {
                return ((selectedIndex - 1 + items.Count) % items.Count, null);
            }

            // 2. 向下移动：DownArrow 或 Vim j
            if (key.Key == ConsoleKey.DownArrow || (noModifiers && (key.Key == ConsoleKey.J || key.KeyChar is 'j' or 'J'))) {
                return ((selectedIndex + 1) % items.Count, null);
            }

            // 3. 翻页与首尾快速跳转
            if (key.Key == ConsoleKey.PageUp) {
                return (Math.Max(0, selectedIndex - 10), null);
            }
            if (key.Key == ConsoleKey.PageDown) {
                return (Math.Min(items.Count - 1, selectedIndex + 10), null);
            }
            if (key.Key == ConsoleKey.Home) {
                return (0, null);
            }
            if (key.Key == ConsoleKey.End) {
                return (items.Count - 1, null);
            }

            // 4. Enter 键：整体确认或进入当前项
            if (key.Key == ConsoleKey.Enter) {
                return (selectedIndex, onEnterAction ?? items[selectedIndex]);
            }

            // 5. Delete 快捷键：清空当前选中的配置项
            if (key.Key == ConsoleKey.Delete && onDeleteAction != null) {
                var curItem = items[selectedIndex];
                var clearItem = new TuiMenuItem(
                    curItem.Id,
                    curItem.Title,
                    curItem.Details,
                    action: TuiMenuAction.Clear,
                    tag: curItem.Tag
                );
                return (selectedIndex, clearItem);
            }

            // 6. 向右移动 / 选择确认：Space、RightArrow 或 Vim l
            if (key.Key is ConsoleKey.Spacebar or ConsoleKey.RightArrow || (noModifiers && (key.Key == ConsoleKey.L || key.KeyChar is 'l' or 'L'))) {
                return (selectedIndex, items[selectedIndex]);
            }

            // 7. 向左移动 / 返回上级：LeftArrow、Escape 或 Vim h
            if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.Escape || (noModifiers && (key.Key == ConsoleKey.H || key.KeyChar is 'h' or 'H'))) {
                var backItem = items.FirstOrDefault(i => i.IsBack);
                return (selectedIndex, backItem ?? TuiMenuItem.BackSentinel);
            }

            return (selectedIndex, null);
        }

        public static IRenderable RenderMenu(
            string titleMarkup,
            IReadOnlyList<TuiMenuItem> items,
            int selectedIndex,
            int scrollOffset = 0,
            int pageSize = int.MaxValue,
            string? customHelp = null
        ) {
            var rows = new List<IRenderable>();

            // 1. 标题
            rows.Add(new Markup(titleMarkup));
            rows.Add(new Text(string.Empty));

            // 2. 全部平铺渲染选项（不折叠、不显示“向上/向下还有xx项”）
            for (int i = 0; i < items.Count; i++) {
                var item = items[i];
                var isSelected = i == selectedIndex;

                var line = FormatItemLine(item, isSelected);
                rows.Add(new Markup(line));
            }

            // 3. 底部操作提示栏
            rows.Add(new Text(string.Empty));
            var help = customHelp ?? Core.Localization.I18n.T("Controls.Navigate", "[grey]操作：[[↑/↓, jk]] 移动  [[Space / → / l]] 进入  [[Enter]] 确认构建  [[← / Esc / h]] 返回[/]");
            rows.Add(new Markup(help));

            return new Rows(rows);
        }

        private static string FormatItemLine(TuiMenuItem item, bool isSelected) {
            var prefix = isSelected ? $"[bold {TuiTheme.Primary}] > [/]" : "   ";

            // 独立返回动作：低调灰色风格
            if (item.IsBack) {
                return isSelected
                    ? $"{prefix}[bold {TuiTheme.TextLight} on {TuiTheme.Surface}]<< {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[grey]<< {Markup.Escape(item.Title)}[/]";
            }

            // 立即构建动作：低饱和度柔和绿风格
            if (item.IsBuild) {
                return isSelected
                    ? $"{prefix}[bold #1e1e2e on {TuiTheme.Success}]>> {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[bold {TuiTheme.Success}]>> {Markup.Escape(item.Title)}[/]";
            }

            // 清空所有覆盖项：低饱和度柔和红风格
            if (item.IsClear) {
                return isSelected
                    ? $"{prefix}[bold #1e1e2e on {TuiTheme.Danger}]x  {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[{TuiTheme.Danger}]x  {Markup.Escape(item.Title)}[/]";
            }

            // 查看全部覆盖：柔和青色风格
            if (item.Action == TuiMenuAction.Inspect) {
                return isSelected
                    ? $"{prefix}[bold #1e1e2e on {TuiTheme.Secondary}]*  {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[{TuiTheme.Secondary}]*  {Markup.Escape(item.Title)}[/]";
            }

            // 普通配置项或分类：
            // 【核心】有覆盖的配置选项通过柔和暖琥珀色标记
            if (item.IsOverridden) {
                var details = string.IsNullOrEmpty(item.Details) ? string.Empty : $" [{TuiTheme.Warning}]  {Markup.Escape(item.Details)}[/]";
                var badge = $" [bold {TuiTheme.Warning}]*[/]";
                return isSelected
                    ? $"{prefix}[bold {TuiTheme.Warning} on {TuiTheme.Surface}]{Markup.Escape(item.Title)}[/]{details}{badge}"
                    : $"{prefix}[bold {TuiTheme.Warning}]{Markup.Escape(item.Title)}[/]{details}{badge}";
            }

            // 未覆盖的默认配置项：干净柔和文字
            var normDetails = string.IsNullOrEmpty(item.Details) ? string.Empty : $" [grey]  {Markup.Escape(item.Details)}[/]";
            return isSelected
                ? $"{prefix}[bold {TuiTheme.Secondary} on {TuiTheme.Surface}]{Markup.Escape(item.Title)}[/]{normDetails}"
                : $"{prefix}[{TuiTheme.TextLight}]{Markup.Escape(item.Title)}[/]{normDetails}";
        }
    }
}
