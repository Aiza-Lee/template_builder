using System.Text;
using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// 专门的 UI 开发与视觉感知脚手架（TUI Development &amp; Preview Scaffolding）。
    /// <para>
    /// 解决痛点：在纯文本环境与非交互模式下，开发与调试人员对 TUI 最终呈现效果的感知较弱。
    /// 本脚手架通过无头虚拟终端引擎，可将所有 TUI 视图（Banner、步骤条、分类导航、字段卡片、覆盖表、仪表板）
    /// 批量渲染为纯文本排版快照、带真彩 ANSI 终端代码的彩色文件以及包含视觉效果解释的 Markdown 汇报。
    /// </para>
    /// </summary>
    internal static class TuiScaffolding {
        /// <summary>
        /// 创建一个绑定到指定 TextWriter 的无头虚拟 IAnsiConsole。
        /// </summary>
        public static IAnsiConsole CreateVirtualConsole(TextWriter writer, bool enableAnsi = false, int width = 80) {
            var settings = new AnsiConsoleSettings {
                Out = new AnsiConsoleOutput(writer),
                Ansi = enableAnsi ? AnsiSupport.Yes : AnsiSupport.No,
                ColorSystem = enableAnsi ? ColorSystemSupport.TrueColor : ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No
            };
            var console = AnsiConsole.Create(settings);
            console.Profile.Width = width;
            return console;
        }

        private static readonly System.Text.RegularExpressions.Regex s_ansiRegex =
            new(@"\x1B\[[0-9;?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*[\x07\x1B]|\x1B.", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// 去除字符串中的全部 ANSI 转义控制字符。
        /// </summary>
        public static string StripAnsi(string text) => s_ansiRegex.Replace(text, string.Empty);

        /// <summary>
        /// 把指定渲染动作输出为纯文本字符串（去除 ANSI 控制字符，适合单测与纯文本阅读）。
        /// </summary>
        public static string RenderToPlainText(Action<IAnsiConsole> renderAction, int width = 80) {
            using var writer = new StringWriter();
            var console = CreateVirtualConsole(writer, enableAnsi: false, width: width);
            console.Profile.Capabilities.Ansi = false;
            renderAction(console);
            return StripAnsi(writer.ToString());
        }

        /// <summary>
        /// 把指定渲染动作输出为带真彩 ANSI 转义序列的字符串（适合终端重放与色彩分析）。
        /// </summary>
        public static string RenderToAnsi(Action<IAnsiConsole> renderAction, int width = 80) {
            using var writer = new StringWriter();
            var console = CreateVirtualConsole(writer, enableAnsi: true, width: width);
            renderAction(console);
            return writer.ToString();
        }

        /// <summary>
        /// 批量生成全套 TUI 视图的快照文件与 Markdown 汇总概览。
        /// </summary>
        /// <param name="outputDir">输出目标目录（例如 preview/tui-scaffold）</param>
        /// <param name="width">虚拟终端字符宽度，默认 80</param>
        /// <returns>生成的快照文件路径清单</returns>
        public static IReadOnlyList<string> GenerateAllSnapshots(string outputDir, int width = 80) {
            Directory.CreateDirectory(outputDir);
            var generatedFiles = new List<string>();

            var mockOverrides = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["TITLE_CONTENT"] = "ACM/ICPC 高级算法模板集",
                ["GLOBAL_MAIN_FONT"] = "JetBrains Mono",
                ["LAYOUT_COLUMNS"] = "2",
                ["CODE_MINTED_STYLE"] = "vs"
            };

            var mockCurrent = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["TITLE_CONTENT"] = "ACM/ICPC 高级算法模板集",
                ["GLOBAL_MAIN_FONT"] = "JetBrains Mono",
                ["GLOBAL_CJK_MAIN_FONT"] = "Source Han Serif SC",
                ["LAYOUT_COLUMNS"] = "2",
                ["CODE_MINTED_STYLE"] = "vs",
                ["DOCCLASS_BASE_FONT_SIZE"] = "10pt"
            };

            var mockDefaults = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["TITLE_CONTENT"] = "文档标题",
                ["GLOBAL_MAIN_FONT"] = "Times New Roman",
                ["GLOBAL_CJK_MAIN_FONT"] = "Source Han Serif SC",
                ["LAYOUT_COLUMNS"] = "1",
                ["CODE_MINTED_STYLE"] = "bw",
                ["DOCCLASS_BASE_FONT_SIZE"] = "10pt"
            };

            var sampleLogs = new List<string> {
                "[grey]01:25:30[/] [blue][[INFO]][/] 开始构建流水线...",
                "[grey]01:25:31[/] [blue][[INFO]][/] 加载配置文件，应用 4 项运行时覆盖",
                "[grey]01:25:32[/] [blue][[INFO]][/] 扫描源文件完成：共纳入 42 个源码文件",
                "[grey]01:25:33[/] [blue][[INFO]][/] 第 1 轮 XeLaTeX 编译完成",
                "[grey]01:25:35[/] [blue][[INFO]][/] 第 2 轮 XeLaTeX 编译完成，生成目录与交叉引用",
                "[grey]01:25:36[/] [green][[SUCCESS]][/] PDF 构建成功，写入 preview/template.pdf"
            };

            var screens = new (string Name, string Title, Action<IAnsiConsole> Action)[] {
                ("01_banner", "品牌霓虹艺术字 Banner", c => TuiViewRenderer.RenderBanner(c, width)),
                ("02_step1_paths", "步骤 1: 路径与运行参数向导", c => {
                    TuiViewRenderer.RenderBreadcrumb(c, 1, "基础路径与运行参数");
                    c.MarkupLine("[bold]源文件目录[/]：[green]/home/user/template_builder/src[/]");
                    c.MarkupLine("[bold]输出 PDF 路径[/]：[green]/home/user/template_builder/preview/template.pdf[/]");
                    c.MarkupLine("[bold]基础配置文件[/]：[green]/home/user/.config/template_builder/config.json[/]");
                }),
                ("03_step2_category_menu", "步骤 2: 配置调优分类导航", c => {
                    TuiViewRenderer.RenderBreadcrumb(c, 2, "配置调优中心");
                    TuiViewRenderer.RenderCategoryMenuPreview(c, mockOverrides);
                }),
                ("04_step2_fields_list", "步骤 2: 分类字段清单与修改标记", c => {
                    var featuredCat = TuiSchemaCatalog.Categories[0];
                    c.MarkupLine($"[bold cyan]{featuredCat.Title}[/] — [grey]{featuredCat.Description}[/]");
                    c.WriteLine();
                    foreach (var f in featuredCat.Fields) {
                        var isOverridden = mockOverrides.TryGetValue(f.Key, out var val);
                        var cur = isOverridden ? val : (mockCurrent.TryGetValue(f.Key, out var cv) ? cv : "");
                        if (isOverridden) {
                            c.MarkupLine($"  • [bold #ffd600]{f.Label}[/] [yellow]({cur})[/] [bold #ffd600](已修改)[/]");
                        } else {
                            c.MarkupLine($"  • [white]{f.Label}[/] [grey]({cur})[/]");
                        }
                    }
                }),
                ("05_step2_field_card", "步骤 2: 字段三段式解释与约束卡片", c => {
                    var field = TuiSchemaCatalog.FieldsByKey["CODE_MINTED_STYLE"];
                    TuiViewRenderer.RenderFieldCard(c, field, "vs", "bw");
                }),
                ("06_overrides_table", "步骤 2: 运行时覆盖总览表", c => {
                    TuiViewRenderer.RenderOverridesTable(c, mockOverrides);
                }),
                ("07_step3_dashboard", "步骤 3: 实时监控与完成仪表板", c => {
                    TuiViewRenderer.RenderBreadcrumb(c, 3, "执行构建与实时监控");
                    TuiViewRenderer.RenderDashboard(c, sampleLogs, TimeSpan.FromSeconds(6.28), ExitCodes.Success);
                })
            };

            var summaryMd = new StringBuilder();
            summaryMd.AppendLine("# TUI 视觉排版与全屏交互快照汇总");
            summaryMd.AppendLine();
            summaryMd.AppendLine("> 本报告由 TUI 专门开发脚手架（`TuiScaffolding`）自动生成，用于在无头环境与纯文本模式下全面审计 TUI 视觉效果与对齐合理性。");
            summaryMd.AppendLine();

            foreach (var (name, title, action) in screens) {
                var plainPath = Path.Combine(outputDir, $"{name}.txt");
                var ansiPath = Path.Combine(outputDir, $"{name}.ansi");

                var plain = RenderToPlainText(action, width);
                var ansi = RenderToAnsi(action, width);

                File.WriteAllText(plainPath, plain, Encoding.UTF8);
                File.WriteAllText(ansiPath, ansi, Encoding.UTF8);

                generatedFiles.Add(plainPath);
                generatedFiles.Add(ansiPath);

                summaryMd.AppendLine($"## {title}");
                summaryMd.AppendLine($"- **纯文本快照**：`{Path.GetFileName(plainPath)}`");
                summaryMd.AppendLine($"- **ANSI 真彩快照**：`{Path.GetFileName(ansiPath)}`");
                summaryMd.AppendLine();
                summaryMd.AppendLine("```text");
                summaryMd.AppendLine(plain.TrimEnd());
                summaryMd.AppendLine("```");
                summaryMd.AppendLine();
            }

            var summaryPath = Path.Combine(outputDir, "SUMMARY.md");
            File.WriteAllText(summaryPath, summaryMd.ToString(), Encoding.UTF8);
            generatedFiles.Add(summaryPath);

            return generatedFiles;
        }
    }
}
