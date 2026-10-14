using System.Text;
using Core.Models;
using Core.SourceTree;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.Tui {
    /// <summary>
    /// TUI 视图渲染器：解耦纯视觉呈现与终端键盘交互。
    /// 统一驱动交互式会话（<see cref="TuiRunner"/>）与无头开发脚手架（<see cref="TuiScaffolding"/>）。
    /// </summary>
    internal static class TuiViewRenderer {
        static TuiViewRenderer() {
            TerminalWidthRegistry.Register("Banner", 48);
            TerminalWidthRegistry.Register("Breadcrumb", 45);
            TerminalWidthRegistry.Register("FieldCard", 50);
            TerminalWidthRegistry.Register("OverridesTable", 55);
            TerminalWidthRegistry.Register("Dashboard", 60);
            TerminalWidthRegistry.Register("SourceTreePreview", 60);
        }

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

        /// <summary>
        /// 根据插值系数 t (0.0 ~ 1.0) 计算低饱和度优雅渐变色 RGB。
        /// </summary>
        public static (int R, int G, int B) GetRainbowColor(double t) => TuiTheme.GetSpectrumColor(t);

        /// <summary>
        /// 构建支持自适应宽度的 Banner 渲染对象（IRenderable）。
        /// 针对宽屏 (>=75)、中屏 (>=48) 与极窄屏三种形态无缝切换，支持全屏实时 Live 重绘与单元测试无头渲染。
        /// </summary>
        public static IRenderable BuildBanner(int width = 80, double waveOffset = 0.0) {
            var items = new List<IRenderable>();

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
                    items.Add(new Markup(sb.ToString()));
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
                    items.Add(new Markup(sb.ToString()));
                }
            } else {
                // 极窄屏幕：单行醒目标题
                items.Add(new Markup($"[bold {TuiTheme.Primary}]=== Template Builder ===[/]"));
            }

            var subtitle = Core.Localization.I18n.T("Banner.Subtitle", $"ACM/ICPC 代码模板生成工具 | 全屏交互模式 v{AppVersion.DisplayVersion}");
            items.Add(new Markup($"[bold {TuiTheme.Primary}]{Markup.Escape(subtitle)}[/]"));
            items.Add(new Rule().RuleStyle(TuiTheme.PrimaryDim));

            return new Rows(items);
        }

        /// <summary>
        /// 渲染紧凑型艺术字 Banner 与品牌副标题。
        /// 支持真彩彩虹动态渐变（波长横向/对角线平滑过渡），自适应终端宽度。
        /// </summary>
        public static void RenderBanner(IAnsiConsole console, int width = 80, double waveOffset = 0.0) {
            console.Write(BuildBanner(width, waveOffset));
            console.WriteLine();
        }

        /// <summary>
        /// 在交互式控制台中执行轻量级动态彩虹波纹微动效（约 120ms），随后进入主流程。
        /// 实时监测终端窗口变动并自适应重新计算宽度。
        /// </summary>
        public static void RenderDynamicBanner(IAnsiConsole console, int? width = null) {
            var targetWidth = width ?? TuiEnvironment.SafeWindowWidth();
            if (Console.IsOutputRedirected || !console.Profile.Capabilities.Ansi) {
                RenderBanner(console, targetWidth, 0.0);
                return;
            }

            try {
                // 3 帧平滑流光位移过渡，实时响应终端宽度变动
                double[] offsets = [0.0, 0.15, 0.30];
                foreach (var offset in offsets) {
                    var currentWidth = width ?? TuiEnvironment.SafeWindowWidth();
                    console.Clear();
                    RenderBanner(console, currentWidth, offset);
                    Thread.Sleep(35);
                }
            } catch {
                console.Clear();
                var fallbackWidth = width ?? TuiEnvironment.SafeWindowWidth();
                RenderBanner(console, fallbackWidth, 0.0);
            }
        }

        /// <summary>
        /// 渲染向导步骤指示条（面包屑导航）。
        /// </summary>
        public static void RenderBreadcrumb(IAnsiConsole console, int step, string title) {
            var step1Tag = Core.Localization.I18n.T("Breadcrumb.Step1.Tag", "[[步骤 1/3: 路径参数]]");
            var step1Done = Core.Localization.I18n.T("Breadcrumb.Step1.Done", "[[✔ 步骤 1: 路径参数]]");
            var step2Tag = Core.Localization.I18n.T("Breadcrumb.Step2.Tag", "[[步骤 2/3: 配置调优]]");
            var step2Done = Core.Localization.I18n.T("Breadcrumb.Step2.Done", "[[✔ 步骤 2: 配置调优]]");
            var step3Tag = Core.Localization.I18n.T("Breadcrumb.Step3.Tag", "[[步骤 3/3: 执行构建]]");

            string s1 = step switch {
                1 => $"[bold {TuiTheme.Primary}]{step1Tag}[/]",
                > 1 => $"[{TuiTheme.Secondary}]{step1Done}[/]",
                _ => $"[grey]{step1Tag}[/]"
            };

            string s2 = step switch {
                2 => $"[bold {TuiTheme.Primary}]{step2Tag}[/]",
                > 2 => $"[{TuiTheme.Secondary}]{step2Done}[/]",
                _ => $"[grey]{step2Tag}[/]"
            };

            string s3 = step switch {
                3 => $"[bold {TuiTheme.Primary}]{step3Tag}[/]",
                _ => $"[grey]{step3Tag}[/]"
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
            console.Write(new Rule($"[bold cyan]{Markup.Escape(field.DisplayLabel)}[/]") { Justification = Justify.Left });
            console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Key", "配置键名：")}[/]{Markup.Escape(field.Key)} [grey]({Markup.Escape(field.Section)})[/]");
            if (!string.IsNullOrWhiteSpace(field.Category)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Category", "所属分类：")}[/]{Markup.Escape(field.Category)}");
            }
            if (!string.IsNullOrWhiteSpace(field.DisplayDescription)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Desc", "功能解释：")}[/]{Markup.Escape(field.DisplayDescription)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Constraints)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Constraints", "内容约束：")}[/][yellow]{Markup.Escape(field.Constraints)}[/]");
            }

            var emptyText = Core.Localization.I18n.T("Field.Empty", "<空>");
            var dispBuiltin = string.IsNullOrEmpty(item.BuiltinValue) ? emptyText : item.BuiltinValue;
            var dispProject = item.ProjectValue != null
                ? (string.IsNullOrEmpty(item.ProjectValue) ? emptyText : item.ProjectValue)
                : Core.Localization.I18n.T("Field.Unspecified", "(未显式指定)");
            var dispEffective = string.IsNullOrEmpty(item.EffectiveValue) ? emptyText : item.EffectiveValue;

            console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Builtin", "内置默认：")}[/][grey]{Markup.Escape(dispBuiltin)}[/]");
            if (item.HasProjectValue) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Project", "项目配置：")}[/][{TuiTheme.Secondary}]{Markup.Escape(dispProject)}[/]");
            }

            var sourceDesc = item.Source switch {
                ConfigSource.Override => $"[bold {TuiTheme.Warning}]{Core.Localization.I18n.T("Field.SourceOverride", "[[运行时覆盖]]")}[/]",
                ConfigSource.Project => $"[{TuiTheme.Secondary}]{Core.Localization.I18n.T("Field.SourceProject", "[[项目配置文件]]")}[/]",
                _ => $"[grey]{Core.Localization.I18n.T("Field.SourceBuiltin", "[[内置默认]]")}[/]"
            };

            if (item.IsOverridden) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Effective", "当前生效：")}[/][bold {TuiTheme.Warning}]{Markup.Escape(dispEffective)}[/] {sourceDesc}");
            } else {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Effective", "当前生效：")}[/][{TuiTheme.Success}]{Markup.Escape(dispEffective)}[/] {sourceDesc}");
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
            console.Write(new Rule($"[bold {TuiTheme.Secondary}]{Markup.Escape(field.DisplayLabel)}[/]") { Justification = Justify.Left });
            console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Key", "配置键名：")}[/]{Markup.Escape(field.Key)} [grey]({Markup.Escape(field.Section)})[/]");
            if (!string.IsNullOrWhiteSpace(field.Category)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Category", "所属分类：")}[/]{Markup.Escape(field.Category)}");
            }
            if (!string.IsNullOrWhiteSpace(field.DisplayDescription)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Desc", "功能解释：")}[/]{Markup.Escape(field.DisplayDescription)}");
            }
            if (!string.IsNullOrWhiteSpace(field.Constraints)) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.Constraints", "内容约束：")}[/][{TuiTheme.Warning}]{Markup.Escape(field.Constraints)}[/]");
            }
            var dispCur = FormatFieldValueDisplay(currentValue, field.Key);
            var dispDef = FormatFieldValueDisplay(defaultVal, field.Key);
            var isModified = !string.Equals(currentValue, defaultVal, StringComparison.Ordinal);

            console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.DefaultVal", "默认取值：")}[/][grey]{Markup.Escape(dispDef)}[/]");
            if (isModified) {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.CurrentVal", "当前取值：")}[/][bold {TuiTheme.Warning}]{Markup.Escape(dispCur)}[/] [bold {TuiTheme.Warning}]{Core.Localization.I18n.T("Field.TagModified", "[[已改]]")}[/]");
            } else {
                console.MarkupLine($"[grey]{Core.Localization.I18n.T("Field.CurrentVal", "当前取值：")}[/][{TuiTheme.Success}]{Markup.Escape(dispCur)}[/]");
            }
            console.WriteLine();
        }

        /// <summary>
        /// 格式化字段取值展示（对特定键如目录排版布局提供直观中文别名）。
        /// </summary>
        public static string FormatFieldValueDisplay(string? value, string key) {
            if (string.IsNullOrEmpty(value)) return Core.Localization.I18n.T("Field.Empty", "<空>");
            if (key == "LAYOUT_TOC_IN_COLUMNS") {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return Core.Localization.I18n.T("Field.IndependentColumn", "独立单栏");
                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return Core.Localization.I18n.T("Field.FollowBody", "跟随正文");
            }
            return value;
        }

        /// <summary>
        /// 渲染当前运行时覆盖概览表格（支持传入 ProjectConfiguration 展示内置与项目来源对比）。
        /// </summary>
        public static void RenderOverridesTable(
            IAnsiConsole console,
            IReadOnlyDictionary<string, string> overrides,
            ProjectConfiguration? configuration = null
        ) {
            var table = new Table().Border(TableBorder.Rounded).Title($"[bold {TuiTheme.Warning}]{Core.Localization.I18n.T("Overrides.Title", "当前已配置的运行时覆盖项")}[/]");
            table.AddColumn(new TableColumn($"[bold]{Core.Localization.I18n.T("Overrides.ColName", "配置项名称")}[/]"));
            table.AddColumn(new TableColumn($"[bold]{Core.Localization.I18n.T("Overrides.ColKey", "配置键名")}[/]"));
            if (configuration != null) {
                table.AddColumn(new TableColumn($"[bold]{Core.Localization.I18n.T("Overrides.ColBuiltin", "内置默认")}[/]"));
                table.AddColumn(new TableColumn($"[bold]{Core.Localization.I18n.T("Overrides.ColProject", "项目配置")}[/]"));
            }
            table.AddColumn(new TableColumn($"[bold]{Core.Localization.I18n.T("Overrides.ColValue", "本次覆盖值")}[/]"));

            if (overrides.Count == 0) {
                var noOverrides = $"[grey]{Core.Localization.I18n.T("Overrides.None", "(未设置任何覆盖)")}[/]";
                if (configuration != null) {
                    table.AddRow(noOverrides, "-", "-", "-", $"[grey]{Core.Localization.I18n.T("Overrides.FollowProjectDefault", "<跟随项目/默认配置>")}[/]");
                } else {
                    table.AddRow(noOverrides, "-", $"[grey]{Core.Localization.I18n.T("Overrides.FollowProject", "<跟随配置文件>")}[/]");
                }
            } else {
                var emptyText = Core.Localization.I18n.T("Field.Empty", "<空>");
                foreach (var (k, v) in overrides) {
                    var label = TuiSchemaCatalog.FieldsByKey.TryGetValue(k, out var f) ? f.DisplayLabel : k;
                    var dispV = FormatFieldValueDisplay(v, k);
                    if (configuration != null) {
                        var item = configuration.GetItem(k);
                        var dispDef = item != null && !string.IsNullOrEmpty(item.BuiltinValue) ? FormatFieldValueDisplay(item.BuiltinValue, k) : emptyText;
                        var dispProj = item != null && item.ProjectValue != null
                            ? (string.IsNullOrEmpty(item.ProjectValue) ? emptyText : FormatFieldValueDisplay(item.ProjectValue, k))
                            : "-";
                        table.AddRow(
                            $"[bold {TuiTheme.Warning}]{Markup.Escape(label)}[/]",
                            $"[grey]{Markup.Escape(k)}[/]",
                            $"[grey]{Markup.Escape(dispDef)}[/]",
                            $"[{TuiTheme.Secondary}]{Markup.Escape(dispProj)}[/]",
                            $"[bold {TuiTheme.Success}]{Markup.Escape(dispV)}[/]"
                        );
                    } else {
                        table.AddRow(
                            $"[bold {TuiTheme.Warning}]{Markup.Escape(label)}[/]",
                            $"[grey]{Markup.Escape(k)}[/]",
                            $"[bold {TuiTheme.Success}]{Markup.Escape(dispV)}[/]"
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
            var table = new Table().Border(TableBorder.Simple).Title($"[bold]{Core.Localization.I18n.T("CategoryPreview.Title", "配置调优分类导航预览")}[/]");
            table.AddColumn(Core.Localization.I18n.T("CategoryPreview.ColId", "分类标识"));
            table.AddColumn(Core.Localization.I18n.T("CategoryPreview.ColName", "分类名称"));
            table.AddColumn(Core.Localization.I18n.T("CategoryPreview.ColCount", "包含字段数"));
            table.AddColumn(Core.Localization.I18n.T("CategoryPreview.ColDesc", "功能简述"));
            table.AddColumn(Core.Localization.I18n.T("CategoryPreview.ColModified", "已修改项数"));

            foreach (var cat in TuiSchemaCatalog.Categories) {
                var modCount = cat.Fields.Count(f => overrides.ContainsKey(f.Key));
                var modFmt = Core.Localization.I18n.T("CategoryPreview.ModifiedFmt", "{0} 项已改");
                var badge = modCount > 0 ? $"[bold {TuiTheme.Warning}]{string.Format(modFmt, modCount)}[/]" : "[grey]0[/]";
                var catTitle = modCount > 0 ? $"[bold {TuiTheme.Warning}]{Markup.Escape(cat.DisplayTitle)}[/]" : Markup.Escape(cat.DisplayTitle);
                table.AddRow(
                    $"[grey]{cat.Id}[/]",
                    catTitle,
                    cat.Fields.Count.ToString(),
                    $"[grey]{Markup.Escape(cat.DisplayDescription)}[/]",
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
            var successText = Core.Localization.I18n.T("Dashboard.Success", "✔ 构建成功");
            var failedFmt = Core.Localization.I18n.T("Dashboard.FailedFmt", "✖ 构建失败 (退出码 {0})");
            var statusBadge = isSuccess
                ? $"[bold #1e1e2e on {TuiTheme.Success}]  {successText}  [/]"
                : $"[bold #1e1e2e on {TuiTheme.Danger}]  {string.Format(failedFmt, exitCode)}  [/]";

            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(2));
            grid.AddColumn(new GridColumn());

            grid.AddRow(statusBadge, $"[grey]{Core.Localization.I18n.T("Dashboard.TotalElapsed", "总耗时：")}[/][bold {TuiTheme.Secondary}]{elapsed.TotalSeconds:F2}s[/]");
            if (isSuccess) {
                grid.AddRow($"[grey]{Core.Localization.I18n.T("Dashboard.Output", "产物输出：")}[/]", $"[underline {TuiTheme.Success}]{Markup.Escape(pdfPath)}[/]");
            }
            console.Write(new Panel(grid).Header($"[bold]{Core.Localization.I18n.T("Dashboard.Status", "构建执行状态")}[/]").Border(BoxBorder.Rounded));

            var logTable = new Table().Border(TableBorder.None).HideHeaders();
            logTable.AddColumn("LogLine");
            foreach (var line in sampleLogs) {
                try {
                    logTable.AddRow(new Markup(line));
                } catch {
                    logTable.AddRow(new Text(line));
                }
            }

            console.Write(new Panel(logTable).Header($"[bold]{Core.Localization.I18n.T("Dashboard.LiveLogs", "实时编译日志")}[/]").Border(BoxBorder.Square));
        }

        /// <summary>
        /// 渲染构建前内容预览、收录解释及章节组织视图。
        /// </summary>
        public static void RenderSourceTreePreview(
            IAnsiConsole console,
            SourceTreePlan plan,
            SourceTreeFilterMode filterMode = SourceTreeFilterMode.All
        ) {
            console.WriteLine();

            // 1. 统计卡片与预检指标
            var statsGrid = new Grid();
            statsGrid.AddColumn(new GridColumn().PadRight(2));
            statsGrid.AddColumn(new GridColumn());

            var maxDepthStyle = plan.MaxDepth <= SourceTreePlan.MaxSupportedDepth ? "green" : "red";
            var maxDepthFmt = Core.Localization.I18n.T("SourceTree.MaxDepthFmt", "最大允许 {0} 层");
            var maxDepthText = $"[{maxDepthStyle}]{plan.MaxDepth}[/] [grey]({string.Format(maxDepthFmt, SourceTreePlan.MaxSupportedDepth)})[/]";

            statsGrid.AddRow($"[bold]{Core.Localization.I18n.T("SourceTree.SourceDir", "源目录路径")}[/]：", $"[underline cyan]{Markup.Escape(plan.SourceDir.FullName)}[/]");
            var statsFmt = Core.Localization.I18n.T("SourceTree.StatsFmt", "物理文件 [bold]{0}[/] 个 | 实际收录 [bold green]{1}[/] 个 | 排除文件 [yellow]{2}[/] 个 | 排除目录 [grey]{3}[/] 个");
            statsGrid.AddRow(
                $"[bold]{Core.Localization.I18n.T("SourceTree.Stats", "收录统计")}[/]：",
                string.Format(statsFmt, plan.TotalPhysicalFiles, plan.IncludedFiles.Count, plan.ExcludedFiles.Count, plan.ExcludedDirectories.Count)
            );
            statsGrid.AddRow($"[bold]{Core.Localization.I18n.T("SourceTree.Depth", "章节深度")}[/]：", maxDepthText);

            var panel = new Panel(statsGrid)
                .Header($"[bold cyan]{Core.Localization.I18n.T("SourceTree.PanelTitle", "📂 源码收录与章节结构预检面板")}[/]")
                .Border(BoxBorder.Rounded);
            console.Write(panel);

            // 2. 全部过滤警报
            if (plan.IncludedFiles.Count == 0) {
                var alert = new Panel(
                    new Markup($"[bold red]{Core.Localization.I18n.T("SourceTree.NoFiles", "✖ 源目录未纳入任何有效源码文件！")}[/]\n" +
                               $"[yellow]{Core.Localization.I18n.T("SourceTree.NoFilesReason", "原因：根据当前的白名单 (PROGRAM.include_file_types) 及排除模式 (PROGRAM.ignore_patterns)，全部文件均已被过滤。")}[/]\n" +
                               $"[grey]{Core.Localization.I18n.T("SourceTree.NoFilesTip", "请检查下方排除详情，或在主菜单中调整过滤配置。")}[/]")
                ).Border(BoxBorder.Heavy).BorderColor(Color.Red);
                console.Write(alert);
            }

            console.WriteLine();

            // 3. 视图切换展示
            switch (filterMode) {
                case SourceTreeFilterMode.IncludedOnly:
                    RenderIncludedTable(console, plan);
                    break;
                case SourceTreeFilterMode.ExcludedOnly:
                    RenderExcludedTable(console, plan);
                    break;
                default:
                    RenderFullTree(console, plan);
                    break;
            }
        }

        private static void RenderFullTree(IAnsiConsole console, SourceTreePlan plan) {
            var tree = new Tree($"[bold {TuiTheme.Secondary}]📁 {Markup.Escape(plan.SourceDir.Name)}[/] [grey]({Markup.Escape(plan.SourceDir.FullName)})[/]");
            foreach (var node in plan.RootNodes) {
                AddTreeNode(tree, node);
            }
            console.Write(tree);
        }

        private static void AddTreeNode(IHasTreeNodes parent, SourceNodePlan node) {
            var notIncludedText = Core.Localization.I18n.T("SourceTree.NotIncluded", "未收录");
            if (node.IsDirectory) {
                TreeNode dirNode;
                if (node.Status == SourceInclusionStatus.Included) {
                    dirNode = parent.AddNode($"[bold {TuiTheme.Warning}]📁 {Markup.Escape(node.DisplayTitle)}[/] [grey](\\{node.SectionCommand})[/]");
                } else {
                    dirNode = parent.AddNode($"[grey]📁 {Markup.Escape(node.DisplayTitle)}[/] [{TuiTheme.Warning}]({Markup.Escape(node.ExclusionExplanation ?? notIncludedText)})[/]");
                }
                foreach (var child in node.Children) {
                    AddTreeNode(dirNode, child);
                }
            } else {
                if (node.Status == SourceInclusionStatus.Included) {
                    var titleBadge = !string.Equals(node.Info.Name, node.DisplayTitle, StringComparison.Ordinal)
                        ? $" [grey]→[/] [bold {TuiTheme.Secondary}]{Markup.Escape(node.DisplayTitle)}[/]"
                        : "";
                    var langBadge = node.IsRawLatex ? $"[bold {TuiTheme.Accent}]raw LaTeX[/]" : $"[{TuiTheme.Secondary}]{Markup.Escape(node.EffectiveLanguage ?? "text")}[/]";
                    parent.AddNode($"[bold {TuiTheme.Success}]✔[/] [bold]{Markup.Escape(node.Info.Name)}[/]{titleBadge} [grey]({langBadge}, \\{node.SectionCommand})[/]");
                } else {
                    parent.AddNode($"[grey strike]✖ {Markup.Escape(node.Info.Name)}[/] [{TuiTheme.Warning}]({Markup.Escape(node.ExclusionExplanation ?? notIncludedText)})[/]");
                }
            }
        }

        private static void RenderIncludedTable(IAnsiConsole console, SourceTreePlan plan) {
            var incTitle = Core.Localization.I18n.T("SourceTree.IncludedTitle", "已收录源码文件清单 (共 {0} 项，按最终排版编译顺序)");
            var table = new Table().Border(TableBorder.Rounded).Title($"[bold {TuiTheme.Success}]{string.Format(incTitle, plan.IncludedFiles.Count)}[/]");
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColIndex", "序号"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColPath", "相对路径"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColTitle", "章节显示标题"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColCommand", "章节命令"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColLang", "代码语言"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColWeight", "排序权重"));

            if (plan.IncludedFiles.Count == 0) {
                table.AddRow("-", $"[grey]{Core.Localization.I18n.T("SourceTree.NoIncludedFiles", "(无有效收录文件)")}[/]", "-", "-", "-", "-");
            } else {
                int index = 1;
                var defWeightText = $"[grey]{Core.Localization.I18n.T("SourceTree.DefaultWeight", "默认(自然序)")}[/]";
                foreach (var file in plan.IncludedFiles) {
                    var weightStr = file.OrderWeight == int.MaxValue ? defWeightText : $"[{TuiTheme.Warning}]{file.OrderWeight}[/]";
                    table.AddRow(
                        $"[grey]{index++}[/]",
                        $"[bold]{Markup.Escape(file.RelativePath)}[/]",
                        $"[{TuiTheme.Secondary}]{Markup.Escape(file.DisplayTitle)}[/]",
                        $"[grey]\\{file.SectionCommand}[/]",
                        file.IsRawLatex ? $"[{TuiTheme.Accent}]raw LaTeX[/]" : $"[{TuiTheme.Success}]{Markup.Escape(file.EffectiveLanguage)}[/]",
                        weightStr
                    );
                }
            }
            console.Write(table);
        }

        private static void RenderExcludedTable(IAnsiConsole console, SourceTreePlan plan) {
            var excTitle = Core.Localization.I18n.T("SourceTree.ExcludedTitle", "被排除项清单与原因排查 (文件: {0} 项，目录: {1} 项)");
            var table = new Table().Border(TableBorder.Rounded).Title($"[bold {TuiTheme.Warning}]{string.Format(excTitle, plan.ExcludedFiles.Count, plan.ExcludedDirectories.Count)}[/]");
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColType", "类型"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColPath", "相对路径"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColReason", "排除原因分类"));
            table.AddColumn(Core.Localization.I18n.T("SourceTree.ColAdvice", "详细说明与调优建议"));

            if (plan.ExcludedFiles.Count == 0 && plan.ExcludedDirectories.Count == 0) {
                table.AddRow("-", $"[grey]{Core.Localization.I18n.T("SourceTree.NoExcludedItems", "(无任何被排除项)")}[/]", "-", "-");
            } else {
                var typeFile = $"[grey]{Core.Localization.I18n.T("SourceTree.TypeFile", "文件")}[/]";
                var typeDir = $"[grey]{Core.Localization.I18n.T("SourceTree.TypeDir", "目录")}[/]";

                foreach (var f in plan.ExcludedFiles) {
                    var reasonName = f.Reason switch {
                        SourceExclusionReason.ExtensionNotIncluded => Core.Localization.I18n.T("SourceTree.ReasonExt", "扩展名未收录"),
                        SourceExclusionReason.IgnorePattern => Core.Localization.I18n.T("SourceTree.ReasonIgnore", "命中排除模式"),
                        SourceExclusionReason.Hidden => Core.Localization.I18n.T("SourceTree.ReasonHidden", "隐藏文件"),
                        SourceExclusionReason.DepthExceeded => Core.Localization.I18n.T("SourceTree.ReasonDepth", "深度超限"),
                        SourceExclusionReason.SymlinkLoop => Core.Localization.I18n.T("SourceTree.ReasonSymlinkLoop", "符号链接循环"),
                        SourceExclusionReason.ExternalSymlink => Core.Localization.I18n.T("SourceTree.ReasonExternalSymlink", "外部符号链接"),
                        _ => f.Reason.ToString()
                    };
                    table.AddRow(
                        typeFile,
                        $"[bold]{Markup.Escape(f.RelativePath)}[/]",
                        $"[yellow]{reasonName}[/]",
                        Markup.Escape(f.Explanation)
                    );
                }
                foreach (var d in plan.ExcludedDirectories) {
                    var reasonName = d.Reason switch {
                        SourceExclusionReason.EmptyDirectory => Core.Localization.I18n.T("SourceTree.ReasonEmptyDir", "空目录/无有效代码"),
                        SourceExclusionReason.IgnorePattern => Core.Localization.I18n.T("SourceTree.ReasonIgnore", "命中排除模式"),
                        SourceExclusionReason.Hidden => Core.Localization.I18n.T("SourceTree.ReasonHiddenDir", "隐藏目录"),
                        SourceExclusionReason.DepthExceeded => Core.Localization.I18n.T("SourceTree.ReasonDepth", "深度超限"),
                        SourceExclusionReason.SymlinkLoop => Core.Localization.I18n.T("SourceTree.ReasonSymlinkLoop", "符号链接循环"),
                        SourceExclusionReason.ExternalSymlink => Core.Localization.I18n.T("SourceTree.ReasonExternalSymlink", "外部符号链接"),
                        _ => d.Reason.ToString()
                    };
                    table.AddRow(
                        typeDir,
                        $"[grey]{Markup.Escape(d.RelativePath)}[/]",
                        $"[grey]{reasonName}[/]",
                        Markup.Escape(d.Explanation)
                    );
                }
            }
            console.Write(table);
        }
    }
}
