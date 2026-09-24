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
        Preview
    }

    /// <summary>
    /// TUI 菜单项定义，支持显式覆写标记、独立返回功能与自适应富文本渲染。
    /// </summary>
    public sealed class TuiMenuItem {
        public static readonly TuiMenuItem BackSentinel = new(
            "__BACK__",
            "返回上级",
            action: TuiMenuAction.Back
        );

        public string Id { get; }
        public string Title { get; }
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

        public TuiMenuItem(
            string id,
            string title,
            string? details = null,
            bool isOverridden = false,
            TuiMenuAction action = TuiMenuAction.Select,
            object? tag = null
        ) {
            Id = id;
            Title = title;
            Details = details;
            IsOverridden = isOverridden;
            Action = action;
            Tag = tag;
        }
    }

    /// <summary>
    /// TUI 交互式选择菜单：
    /// 1. 支持上下箭头、PageUp/PageDown、Home/End 滚动选择；
    /// 2. 【核心】支持 RightArrow (→) 替代 Enter 确认进入；
    /// 3. 【核心】支持 LeftArrow (←) 与 Escape 替代返回，配合独立的返回项实现双重返回保护；
    /// 4. 【核心】有覆盖的配置选项使用醒目的高亮黄色/琥珀色标记，未覆盖项采用灰白低对比度；
    /// 5. 底部呈现人性化操作引导提示。
    /// </summary>
    public static class TuiMenu {
        public static TuiMenuItem Prompt(
            string titleMarkup,
            IReadOnlyList<TuiMenuItem> items,
            int pageSize = 14,
            int defaultIndex = 0,
            string? initialSelectedId = null
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
            var scrollOffset = 0;
            EnsureVisible(selectedIndex, ref scrollOffset, items.Count, pageSize);

            TuiMenuItem? chosen = null;

            AnsiConsole.Live(RenderMenu(titleMarkup, items, selectedIndex, scrollOffset, pageSize))
                .AutoClear(true)
                .Start(ctx => {
                    while (chosen == null) {
                        EnsureVisible(selectedIndex, ref scrollOffset, items.Count, pageSize);
                        ctx.UpdateTarget(RenderMenu(titleMarkup, items, selectedIndex, scrollOffset, pageSize));
                        ctx.Refresh();

                        ConsoleKeyInfo key;
                        try {
                            key = Console.ReadKey(true);
                        } catch {
                            chosen = items[selectedIndex];
                            break;
                        }

                        if (key.Key == ConsoleKey.UpArrow) {
                            selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                        } else if (key.Key == ConsoleKey.DownArrow) {
                            selectedIndex = (selectedIndex + 1) % items.Count;
                        } else if (key.Key == ConsoleKey.PageUp) {
                            selectedIndex = Math.Max(0, selectedIndex - pageSize);
                        } else if (key.Key == ConsoleKey.PageDown) {
                            selectedIndex = Math.Min(items.Count - 1, selectedIndex + pageSize);
                        } else if (key.Key == ConsoleKey.Home) {
                            selectedIndex = 0;
                        } else if (key.Key == ConsoleKey.End) {
                            selectedIndex = items.Count - 1;
                        } else if (key.Key is ConsoleKey.Enter or ConsoleKey.RightArrow) {
                            // Enter 或 右箭头 (→) 确认进入当前选中的项目
                            chosen = items[selectedIndex];
                            break;
                        } else if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.Escape) {
                            // 左箭头 (←) 或 Escape：直接触发返回上级
                            var backItem = items.FirstOrDefault(i => i.IsBack);
                            chosen = backItem ?? TuiMenuItem.BackSentinel;
                            break;
                        }
                    }
                });

            var result = chosen ?? items[selectedIndex];

            // 渲染简洁选中结果，保留操作轨迹
            var actionTag = result.IsBack
                ? "[grey]返回[/]"
                : (result.IsOverridden ? "[bold #ffd600]已修改项[/]" : "[grey]选择[/]");
            AnsiConsole.MarkupLine($"[grey]✔ {actionTag}：[/]{Markup.Escape(result.Title)}");

            return result;
        }

        private static void EnsureVisible(int selectedIndex, ref int scrollOffset, int totalCount, int pageSize) {
            if (totalCount <= pageSize) {
                scrollOffset = 0;
                return;
            }
            if (selectedIndex < scrollOffset) {
                scrollOffset = selectedIndex;
            } else if (selectedIndex >= scrollOffset + pageSize) {
                scrollOffset = selectedIndex - pageSize + 1;
            }
            scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, totalCount - pageSize));
        }

        public static IRenderable RenderMenu(
            string titleMarkup,
            IReadOnlyList<TuiMenuItem> items,
            int selectedIndex,
            int scrollOffset,
            int pageSize
        ) {
            var visibleCount = Math.Min(pageSize, items.Count - scrollOffset);
            var rows = new List<IRenderable>();

            // 1. 标题
            rows.Add(new Markup(titleMarkup));
            rows.Add(new Text(string.Empty));

            // 2. 向上滚动提示
            if (scrollOffset > 0) {
                rows.Add(new Markup($"[grey]   ▲ 向上还有 {scrollOffset} 项...[/]"));
            }

            // 3. 渲染当前页选项
            for (int i = 0; i < visibleCount; i++) {
                var actualIndex = scrollOffset + i;
                var item = items[actualIndex];
                var isSelected = actualIndex == selectedIndex;

                var line = FormatItemLine(item, isSelected);
                rows.Add(new Markup(line));
            }

            // 4. 向下滚动提示
            var remainingBelow = items.Count - (scrollOffset + visibleCount);
            if (remainingBelow > 0) {
                rows.Add(new Markup($"[grey]   ▼ 向下还有 {remainingBelow} 项...[/]"));
            }

            // 5. 底部操作提示栏
            rows.Add(new Text(string.Empty));
            rows.Add(new Markup("[grey]操作：[[↑/↓]] 移动  [[Enter / →]] 确认进入  [[← / Esc]] 返回上级[/]"));

            return new Rows(rows);
        }

        private static string FormatItemLine(TuiMenuItem item, bool isSelected) {
            var prefix = isSelected ? "[bold #00f5d4] > [/]" : "   ";

            // 独立返回动作：低调灰色风格
            if (item.IsBack) {
                return isSelected
                    ? $"{prefix}[bold white on grey23]<< {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[grey]<< {Markup.Escape(item.Title)}[/]";
            }

            // 立即构建动作：醒目翠绿风格
            if (item.IsBuild) {
                return isSelected
                    ? $"{prefix}[bold black on green]>> {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[bold green]>> {Markup.Escape(item.Title)}[/]";
            }

            // 清空所有覆盖项：淡红告警风格
            if (item.IsClear) {
                return isSelected
                    ? $"{prefix}[bold white on red3]x  {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[red]x  {Markup.Escape(item.Title)}[/]";
            }

            // 查看全部覆盖：青色查看风格
            if (item.Action == TuiMenuAction.Inspect) {
                return isSelected
                    ? $"{prefix}[bold black on cyan]*  {Markup.Escape(item.Title)}[/]"
                    : $"{prefix}[cyan]*  {Markup.Escape(item.Title)}[/]";
            }

            // 普通配置项或分类：
            // 【核心】有覆盖的配置选项通过高饱和度金黄/琥珀色标记！
            if (item.IsOverridden) {
                var details = string.IsNullOrEmpty(item.Details) ? string.Empty : $" [yellow]({Markup.Escape(item.Details)})[/]";
                var badge = " [bold #ffd600](已修改)[/]";
                return isSelected
                    ? $"{prefix}[bold #ffd600 on #332b00]{Markup.Escape(item.Title)}[/]{details}{badge}"
                    : $"{prefix}[bold #ffd600]{Markup.Escape(item.Title)}[/]{details}{badge}";
            }

            // 未覆盖的默认配置项：干净灰白
            var normDetails = string.IsNullOrEmpty(item.Details) ? string.Empty : $" [grey]({Markup.Escape(item.Details)})[/]";
            return isSelected
                ? $"{prefix}[bold cyan on grey23]{Markup.Escape(item.Title)}[/]{normDetails}"
                : $"{prefix}[white]{Markup.Escape(item.Title)}[/]{normDetails}";
        }
    }
}
