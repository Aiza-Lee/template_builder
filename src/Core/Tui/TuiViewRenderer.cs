using System.Text;
using Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.Tui {
    /// <summary>
    /// TUI 视图渲染器：解耦纯视觉呈现与终端键盘交互。
    /// 统一驱动交互式会话（<see cref="TuiRunner"/>）与无头开发脚手架（<see cref="TuiScaffolding"/>）。
    /// </summary>
    internal static class TuiViewRenderer {
        private static readonly string[] s_tightBannerLines = [
            @" _____                _       _          ___        _ _     _           ",
            @"|_   _|__ _ __  _ __ | | __ _| |_ ___   | _ )_  _  (_) | __| |___ _ _  ",
            @"  | |/ -_) '  \| '_ \| |/ _` |  _/ -_)  | _ \ || | | | |/ _` / -_) '_| ",
            @"  |_|\___|_|_|_| .__/|_|\__,_|\__\___|  |___/\_,_| |_|_|\__,_\___|_|   ",
            @"               |_|                                                     "
        ];

        private static readonly string[] s_compactBannerLines = [
            @"╔╦╗┌─┐┌┬┐┌─┐┬  ┌─┐┌┬┐┌─┐  ╔╗ ┬ ┬┬┬  ┌┬┐┌─┐┬─┐",
            @" ║ ├┤ │││├─┘│  ├─┤ │ ├┤   ╠╩╗│ │││   ││├┤ ├┬┘",
            @" ╩ └─┘┴ ┴┴  ┴─┘┴ ┴ ┴ └─┘  ╚═╝└─┘┴┴─┘─┴┘└─┘┴└─"
        ];

        private static readonly (int R, int G, int B)[] s_rainbowSpectrum = [
            (255, 0, 85),    // 霓虹桃红 / 珊瑚红
            (255, 120, 0),   // 亮丽电光橙
            (255, 215, 0),   // 明亮金黄
            (0, 230, 118),   // 翠绿 / 霓虹绿
            (0, 229, 255),   // 天青 / 霓虹青
            (41, 121, 255),  // 亮蓝 / 皇家蓝
            (170, 0, 255),   // 极光紫
            (255, 0, 85)     // 闭环渐变
        ];

        /// <summary>
        /// 根据插值系数 t (0.0 ~ 1.0) 计算彩虹渐变色 RGB。
        /// </summary>
        public static (int R, int G, int B) GetRainbowColor(double t) {
            t = ((t % 1.0) + 1.0) % 1.0;
            var stops = s_rainbowSpectrum;
            var scaled = t * (stops.Length - 1);
            var idx = (int)scaled;
            var frac = scaled - idx;
            if (idx >= stops.Length - 1) return stops[^1];
            var c1 = stops[idx];
            var c2 = stops[idx + 1];
            var r = (int)(c1.R + (c2.R - c1.R) * frac);
            var g = (int)(c1.G + (c2.G - c1.G) * frac);
            var b = (int)(c1.B + (c2.B - c1.B) * frac);
            return (r, g, b);
        }

        /// <summary>
        /// 渲染紧凑型艺术字 Banner 与品牌副标题。
        /// 支持真彩彩虹动态渐变（波长横向/对角线平滑过渡），自适应终端宽度。
        /// </summary>
        public static void RenderBanner(IAnsiConsole console, int width = 80, double waveOffset = 0.0) {
            if (width >= 75) {
                // 宽屏：紧凑高密度艺术字 + 绚丽连续彩虹光谱渐变
                var maxLen = s_tightBannerLines.Max(l => l.Length);
                for (int row = 0; row < s_tightBannerLines.Length; row++) {
                    var line = s_tightBannerLines[row];
                    var sb = new StringBuilder();
                    string? currentColor = null;

                    for (int col = 0; col < line.Length; col++) {
                        var ch = line[col];
                        if (ch == ' ') {
                            if (currentColor != null) {
                                sb.Append("[/]");
                                currentColor = null;
                            }
                            sb.Append(' ');
                            continue;
                        }

                        // 对角线流光渐变相位
                        var t = (col / (double)maxLen * 0.85 + row * 0.04 + waveOffset) % 1.0;
                        var (r, g, b) = GetRainbowColor(t);
                        var hex = $"#{r:x2}{g:x2}{b:x2}";

                        if (currentColor != hex) {
                            if (currentColor != null) sb.Append("[/]");
                            sb.Append($"[{hex}]");
                            currentColor = hex;
                        }

                        sb.Append(Markup.Escape(ch.ToString()));
                    }

                    if (currentColor != null) sb.Append("[/]");
                    console.MarkupLine(sb.ToString());
                }
            } else if (width >= 48) {
                // 中屏：精致 3 行 Unicode 艺术字 + 彩虹渐变
                var maxLen = s_compactBannerLines.Max(l => l.Length);
                for (int row = 0; row < s_compactBannerLines.Length; row++) {
                    var line = s_compactBannerLines[row];
                    var sb = new StringBuilder();
                    string? currentColor = null;

                    for (int col = 0; col < line.Length; col++) {
                        var ch = line[col];
                        if (ch == ' ') {
                            if (currentColor != null) {
                                sb.Append("[/]");
                                currentColor = null;
                            }
                            sb.Append(' ');
                            continue;
                        }

                        var t = (col / (double)maxLen * 0.85 + row * 0.08 + waveOffset) % 1.0;
                        var (r, g, b) = GetRainbowColor(t);
                        var hex = $"#{r:x2}{g:x2}{b:x2}";

                        if (currentColor != hex) {
                            if (currentColor != null) sb.Append("[/]");
                            sb.Append($"[{hex}]");
                            currentColor = hex;
                        }

                        sb.Append(Markup.Escape(ch.ToString()));
                    }

                    if (currentColor != null) sb.Append("[/]");
                    console.MarkupLine(sb.ToString());
                }
            } else {
                // 极窄屏幕：单行醒目标题
                console.MarkupLine("[bold #00f5d4]=== Template Builder ===[/]");
            }

            console.MarkupLine("[bold #00f5d4]ACM/ICPC 代码模板生成工具[/] [grey]| 全屏交互模式 v2.0.0[/]");
            console.Write(new Rule().RuleStyle("#4361ee"));
            console.WriteLine();
        }

        /// <summary>
        /// 在交互式控制台中执行轻量级动态彩虹波纹微动效（约 120ms），随后进入主流程。
        /// </summary>
        public static void RenderDynamicBanner(IAnsiConsole console, int width = 80) {
            if (Console.IsOutputRedirected || !console.Profile.Capabilities.Ansi) {
                RenderBanner(console, width, 0.0);
                return;
            }

            try {
                // 3 帧平滑流光位移过渡
                double[] offsets = [0.0, 0.15, 0.30];
                foreach (var offset in offsets) {
                    console.Clear();
                    RenderBanner(console, width, offset);
                    Thread.Sleep(35);
                }
            } catch {
                console.Clear();
                RenderBanner(console, width, 0.0);
            }
        }

        /// <summary>
        /// 渲染向导步骤指示条（面包屑导航）。
        /// </summary>
        public static void RenderBreadcrumb(IAnsiConsole console, int step, string title) {
            string s1 = step switch {
                1 => "[bold #00f5d4][[步骤 1/3: 路径参数]][/]",
                > 1 => "[#00bbf9][[✔ 步骤 1: 路径参数]][/]",
                _ => "[grey][[步骤 1/3: 路径参数]][/]"
            };

            string s2 = step switch {
                2 => "[bold #00f5d4][[步骤 2/3: 配置调优]][/]",
                > 2 => "[#00bbf9][[✔ 步骤 2: 配置调优]][/]",
                _ => "[grey][[步骤 2/3: 配置调优]][/]"
            };

            string s3 = step switch {
                3 => "[bold #00f5d4][[步骤 3/3: 执行构建]][/]",
                _ => "[grey][[步骤 3/3: 执行构建]][/]"
            };

            console.MarkupLine($"{s1} ── {s2} ── {s3}");
            console.MarkupLine($"[bold underline]{Markup.Escape(title)}[/]");
            console.WriteLine();
        }

        /// <summary>
        /// 渲染配置字段详情卡片（基于精确的 ConfigItem 模型）。
        /// </summary>
        public static void RenderFieldCard(
            IAnsiConsole console,
            TuiConfigField field,
            ConfigItem? item
        ) {
            if (item == null) {
                RenderFieldCard(console, field, string.Empty, string.Empty);
                return;
            }

            console.WriteLine();
            console.Write(new Rule($"[bold cyan]{Markup.Escape(field.Label)}[/]") { Justification = Justify.Left });
            console.MarkupLine($"[grey]配置键名：[/]{Markup.Escape(field.Key)} [grey]({Markup.Escape(field.Section)})[/]");
            if (!string.IsNullOrWhiteSpace(field.Category)) {
                console.MarkupLine($"[grey]所属分类：[/]{Markup.Escape(field.Category)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Description)) {
                console.MarkupLine($"[grey]功能解释：[/]{Markup.Escape(field.Description)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Constraints)) {
                console.MarkupLine($"[grey]内容约束：[/][yellow]{Markup.Escape(field.Constraints)}[/]");
            }

            var dispBuiltin = string.IsNullOrEmpty(item.BuiltinValue) ? "<空>" : item.BuiltinValue;
            var dispProject = item.ProjectValue != null
                ? (string.IsNullOrEmpty(item.ProjectValue) ? "<空>" : item.ProjectValue)
                : "(未显式指定)";
            var dispEffective = string.IsNullOrEmpty(item.EffectiveValue) ? "<空>" : item.EffectiveValue;

            console.MarkupLine($"[grey]内置默认：[/][grey]{Markup.Escape(dispBuiltin)}[/]");
            if (item.HasProjectValue) {
                console.MarkupLine($"[grey]项目配置：[/][cyan]{Markup.Escape(dispProject)}[/]");
            }

            var sourceDesc = item.Source switch {
                ConfigSource.Override => "[bold #ffd600][运行时覆盖][/]",
                ConfigSource.Project => "[cyan][项目配置文件][/]",
                _ => "[grey][内置默认][/]"
            };

            if (item.IsOverridden) {
                console.MarkupLine($"[grey]当前生效：[/][bold #ffd600]{Markup.Escape(dispEffective)}[/] {sourceDesc}");
            } else {
                console.MarkupLine($"[grey]当前生效：[/][green]{Markup.Escape(dispEffective)}[/] {sourceDesc}");
            }
            console.WriteLine();
        }

        /// <summary>
        /// 渲染配置字段详情卡片（兼容旧调用签名）。
        /// </summary>
        public static void RenderFieldCard(
            IAnsiConsole console,
            TuiConfigField field,
            string? currentValue,
            string defaultVal
        ) {
            console.WriteLine();
            console.Write(new Rule($"[bold cyan]{Markup.Escape(field.Label)}[/]") { Justification = Justify.Left });
            console.MarkupLine($"[grey]配置键名：[/]{Markup.Escape(field.Key)} [grey]({Markup.Escape(field.Section)})[/]");
            if (!string.IsNullOrWhiteSpace(field.Category)) {
                console.MarkupLine($"[grey]所属分类：[/]{Markup.Escape(field.Category)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Description)) {
                console.MarkupLine($"[grey]功能解释：[/]{Markup.Escape(field.Description)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Constraints)) {
                console.MarkupLine($"[grey]内容约束：[/][yellow]{Markup.Escape(field.Constraints)}[/]");
            }
            var dispCur = string.IsNullOrEmpty(currentValue) ? "<空>" : currentValue;
            var dispDef = string.IsNullOrEmpty(defaultVal) ? "<空>" : defaultVal;
            var isModified = !string.Equals(currentValue, defaultVal, StringComparison.Ordinal);

            console.MarkupLine($"[grey]默认取值：[/][grey]{Markup.Escape(dispDef)}[/]");
            if (isModified) {
                console.MarkupLine($"[grey]当前取值：[/][bold #ffd600]{Markup.Escape(dispCur)}[/] [bold #ffd600](已修改)[/]");
            } else {
                console.MarkupLine($"[grey]当前取值：[/][green]{Markup.Escape(dispCur)}[/]");
            }
            console.WriteLine();
        }

        /// <summary>
        /// 渲染当前运行时覆盖概览表格（支持传入 ProjectConfiguration 展示内置与项目来源对比）。
        /// </summary>
        public static void RenderOverridesTable(
            IAnsiConsole console,
            IReadOnlyDictionary<string, string> overrides,
            ProjectConfiguration? configuration = null
        ) {
            var table = new Table().Border(TableBorder.Rounded).Title("[bold #ffd600]当前已配置的运行时覆盖项[/]");
            table.AddColumn(new TableColumn("[bold]配置项名称[/]"));
            table.AddColumn(new TableColumn("[bold]配置键名[/]"));
            if (configuration != null) {
                table.AddColumn(new TableColumn("[bold]内置默认[/]"));
                table.AddColumn(new TableColumn("[bold]项目配置[/]"));
            }
            table.AddColumn(new TableColumn("[bold]本次覆盖值[/]"));

            if (overrides.Count == 0) {
                if (configuration != null) {
                    table.AddRow("[grey](未设置任何覆盖)[/]", "-", "-", "-", "[grey]<跟随项目/默认配置>[/]");
                } else {
                    table.AddRow("[grey](未设置任何覆盖)[/]", "-", "[grey]<跟随配置文件>[/]");
                }
            } else {
                foreach (var (k, v) in overrides) {
                    var label = TuiSchemaCatalog.FieldsByKey.TryGetValue(k, out var f) ? f.Label : k;
                    var dispV = string.IsNullOrEmpty(v) ? "<空>" : v;
                    if (configuration != null) {
                        var item = configuration.GetItem(k);
                        var dispDef = item != null && !string.IsNullOrEmpty(item.BuiltinValue) ? item.BuiltinValue : "<空>";
                        var dispProj = item != null && item.ProjectValue != null
                            ? (string.IsNullOrEmpty(item.ProjectValue) ? "<空>" : item.ProjectValue)
                            : "-";
                        table.AddRow(
                            $"[bold #ffd600]{Markup.Escape(label)}[/]",
                            $"[grey]{Markup.Escape(k)}[/]",
                            $"[grey]{Markup.Escape(dispDef)}[/]",
                            $"[cyan]{Markup.Escape(dispProj)}[/]",
                            $"[bold green]{Markup.Escape(dispV)}[/]"
                        );
                    } else {
                        table.AddRow(
                            $"[bold #ffd600]{Markup.Escape(label)}[/]",
                            $"[grey]{Markup.Escape(k)}[/]",
                            $"[bold green]{Markup.Escape(dispV)}[/]"
                        );
                    }
                }
            }

            console.Write(table);
        }

        /// <summary>
        /// 渲染分类菜单视图。
        /// </summary>
        public static void RenderCategoryMenuPreview(
            IAnsiConsole console,
            IReadOnlyDictionary<string, string> overrides
        ) {
            var table = new Table().Border(TableBorder.Simple).Title("[bold]配置调优分类导航预览[/]");
            table.AddColumn("分类标识");
            table.AddColumn("分类名称");
            table.AddColumn("包含字段数");
            table.AddColumn("功能简述");
            table.AddColumn("已修改项数");

            foreach (var cat in TuiSchemaCatalog.Categories) {
                var modCount = cat.Fields.Count(f => overrides.ContainsKey(f.Key));
                var badge = modCount > 0 ? $"[bold #ffd600]{modCount} 项已改[/]" : "[grey]0[/]";
                var catTitle = modCount > 0 ? $"[bold #ffd600]{Markup.Escape(cat.Title)}[/]" : Markup.Escape(cat.Title);
                table.AddRow(
                    $"[grey]{cat.Id}[/]",
                    catTitle,
                    cat.Fields.Count.ToString(),
                    $"[grey]{Markup.Escape(cat.Description)}[/]",
                    badge
                );
            }

            console.Write(table);
        }

        /// <summary>
        /// 渲染构建监控与结果仪表板。
        /// </summary>
        public static void RenderDashboard(
            IAnsiConsole console,
            IReadOnlyList<string> sampleLogs,
            TimeSpan elapsed,
            int exitCode,
            string pdfPath = "preview/template.pdf"
        ) {
            var isSuccess = exitCode == ExitCodes.Success;
            var statusBadge = isSuccess
                ? "[bold black on green]  ✔ 构建成功  [/]"
                : $"[bold white on red]  ✖ 构建失败 (退出码 {exitCode})  [/]";

            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(2));
            grid.AddColumn(new GridColumn());

            grid.AddRow(statusBadge, $"[grey]总耗时：[/][bold cyan]{elapsed.TotalSeconds:F2}s[/]");
            if (isSuccess) {
                grid.AddRow("[grey]产物输出：[/]", $"[underline green]{Markup.Escape(pdfPath)}[/]");
            }
            console.Write(new Panel(grid).Header("[bold]构建执行状态[/]").Border(BoxBorder.Rounded));

            var logTable = new Table().Border(TableBorder.None).HideHeaders();
            logTable.AddColumn("LogLine");
            foreach (var line in sampleLogs) {
                logTable.AddRow(line);
            }

            console.Write(new Panel(logTable).Header("[bold]实时编译日志[/]").Border(BoxBorder.Square));
        }
    }
}
