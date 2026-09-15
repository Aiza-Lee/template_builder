using Core.Pipeline;
using Spectre.Console;
using Spectre.Console.Rendering;
using Utils;

namespace Core.Tui {
    /// <summary>
    /// TUI 交互模式会话：依次采集「源目录 → 输出路径 → 常用配置项」，随后在全屏界面中实时滚动构建日志。
    /// <para>
    /// 构建本身<b>完全复用</b> <see cref="Pipeline.BuildPipelineRunner"/>——本类只负责输入采集与渲染。
    /// 配置项的改动不落盘，而是作为 <see cref="BuildSubcommandOptions.ConfigOverrides"/> 注入本次构建，
    /// 配置文件保持原样。
    /// </para>
    /// </summary>
    internal sealed class TuiRunner(ILogger bootstrapLogger) {
        /// <summary>日志环形缓冲区容量（xelatex 输出极其啰嗦，必须有界）。</summary>
        private const int LogCapacity = 400;

        /// <summary>渲染刷新间隔（毫秒）。</summary>
        private const int RefreshIntervalMs = 120;

        private readonly ILogger _bootstrapLogger = bootstrapLogger;

        public int Run(TuiLaunchOptions launch) {
            AnsiConsole.Clear();

            var source = PromptSourceDir(launch.SourceDir);
            var output = PromptOutputPdf(launch.OutputPdf, source);
            var (configFile, userProvided) = ResolveConfig(launch);

            var sink = new TuiLogSink(LogCapacity);
            sink.SetLevel(launch.Verbose ? LogLevel.DEBUG : LogLevel.INFO);

            var overrides = PromptConfigFields(configFile, sink);

            return RunBuild(launch, source, output, configFile, userProvided, overrides, sink);
        }

        /// <summary>
        /// 解析配置文件，复用与 <c>build</c> 子命令一致的回退逻辑（不存在时回退默认配置并生成）。
        /// </summary>
        private (FileInfo ConfigFile, bool UserProvided) ResolveConfig(TuiLaunchOptions launch) {
            return new ConfigPathResolver(_bootstrapLogger).Resolve(
                launch.ConfigFile,
                launch.UserProvidedConfig,
                launch.EnsureDefaultConfig
            );
        }

        /// <summary>
        /// 询问源文件目录。用校验器拦住不存在的路径并原地重问，避免把无效输入带进构建阶段。
        /// </summary>
        private static DirectoryInfo PromptSourceDir(DirectoryInfo? initial) {
            var prompt = new TextPrompt<string>("[bold]源文件目录[/]：")
                .PromptStyle("green")
                .Validate(raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) {
                        return ValidationResult.Error("[red]路径不能为空[/]");
                    }
                    return Directory.Exists(path)
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[red]目录不存在：{Markup.Escape(path)}[/]");
                });

            if (initial is { Exists: true }) {
                prompt.DefaultValue(initial.FullName);
            }

            return new DirectoryInfo(AnsiConsole.Prompt(prompt).Trim());
        }

        /// <summary>
        /// 询问输出 PDF 路径。未提供时默认放到源目录同级的 <c>preview/</c> 下，
        /// 与命令行构建「产物不污染源目录」的约定一致。
        /// </summary>
        private static FileInfo PromptOutputPdf(FileInfo? initial, DirectoryInfo source) {
            var hasUsableInitial = initial?.Directory is { Exists: true };
            var suggested = hasUsableInitial
                ? initial!.FullName
                : Path.Combine(
                    source.Parent?.FullName ?? source.FullName,
                    "preview",
                    source.Name + ".pdf"
                );

            var prompt = new TextPrompt<string>("[bold]输出 PDF 路径[/]：")
                .PromptStyle("green")
                .DefaultValue(suggested)
                .Validate(raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) {
                        return ValidationResult.Error("[red]输出路径不能为空[/]");
                    }
                    if (Directory.Exists(path)) {
                        return ValidationResult.Error("[red]输出路径不能是目录[/]");
                    }
                    try {
                        return string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetFullPath(path)))
                            ? ValidationResult.Error("[red]无法解析输出目录[/]")
                            : ValidationResult.Success();
                    } catch (Exception) {
                        return ValidationResult.Error("[red]输出路径非法[/]");
                    }
                });

            return new FileInfo(AnsiConsole.Prompt(prompt).Trim());
        }

        /// <summary>
        /// 展示当前配置值总表，让用户勾选要覆盖的项，再逐项询问新值。未勾选任何项 = 全部沿用配置文件。
        /// </summary>
        private static Dictionary<string, string> PromptConfigFields(FileInfo configFile, ILogger sink) {
            var current = ReadCurrentValues(configFile, sink);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]常用配置项（当前值）[/]");
            table.AddColumn("配置项");
            table.AddColumn("当前值");
            foreach (var field in TuiOptionsCatalog.Fields) {
                var value = current.TryGetValue(field.Key, out var v) ? v : "<默认>";
                table.AddRow(Markup.Escape(field.Label), Markup.Escape(value));
            }
            AnsiConsole.Write(table);

            var selection = new MultiSelectionPrompt<string>()
                .Title("[bold]选择需要覆盖的配置项[/]（不选直接回车 = 全部沿用配置文件）")
                .NotRequired()
                .PageSize(TuiOptionsCatalog.Fields.Count)
                .InstructionsText("[grey](空格 勾选 / 回车 确认)[/]");
            foreach (var field in TuiOptionsCatalog.Fields) {
                selection.AddChoice(field.Label);
            }
            var chosen = AnsiConsole.Prompt(selection).ToHashSet(StringComparer.Ordinal);

            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in TuiOptionsCatalog.Fields.Where(f => chosen.Contains(f.Label))) {
                current.TryGetValue(field.Key, out var existing);
                overrides[field.Key] = PromptSingleField(field, existing);
            }

            // 注意：必须用 MarkupLine——AnsiConsole.WriteLine 不解析 markup，会把 [green]…[/] 原样打出来
            AnsiConsole.MarkupLine(
                overrides.Count == 0
                    ? "[grey]未覆盖任何配置项，将完全按配置文件构建。[/]"
                    : $"[green]将覆盖 {overrides.Count} 个配置项（仅本次构建，不写回配置文件）。[/]"
            );
            return overrides;
        }

        /// <summary>按字段类型渲染对应的输入控件，返回字符串形式的原始值。</summary>
        private static string PromptSingleField(TuiConfigField field, string? currentValue) {
            switch (field.Kind) {
                case TuiFieldKind.Choice:
                    return AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"[bold]{Markup.Escape(field.Label)}[/]")
                            .AddChoices(field.Choices ?? [])
                            .WrapAround()
                    );

                case TuiFieldKind.Toggle:
                    var enabled = AnsiConsole.Prompt(
                        new ConfirmationPrompt($"[bold]{Markup.Escape(field.Label)}[/]") {
                            DefaultValue = string.Equals(currentValue, "true", StringComparison.OrdinalIgnoreCase),
                        }
                    );
                    return enabled ? "true" : "false";

                default:
                    var prompt = new TextPrompt<string>($"[bold]{Markup.Escape(field.Label)}[/]：").AllowEmpty();
                    if (!string.IsNullOrEmpty(currentValue)) {
                        prompt.DefaultValue(currentValue);
                    }
                    return AnsiConsole.Prompt(prompt);
            }
        }

        /// <summary>
        /// 读取配置文件中各快速配置项的当前值，用于界面预填。
        /// 解析失败（JSON 损坏）时不阻断交互——留给构建阶段按既有路径报错。
        /// </summary>
        private static Dictionary<string, string> ReadCurrentValues(FileInfo configFile, ILogger sink) {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var parser = new ConfigParser("TEX", sink, ConfigStrictness.Lax);
            try {
                parser.ParseConfigFile(File.ReadAllText(configFile.FullName), configFile.FullName);
            } catch (Exception) {
                return values;
            }

            foreach (var field in TuiOptionsCatalog.Fields) {
                var value = parser[field.Key].GetAsString();
                if (!string.IsNullOrEmpty(value)) {
                    values[field.Key] = value;
                }
            }
            return values;
        }

        /// <summary>
        /// 在 Live 显示中执行构建：构建跑在后台线程，渲染线程按固定间隔取日志快照重绘，
        /// 从而形成「实时滚动」的效果。构建结束后保留最后一帧（<c>AutoClear(false)</c>）供用户回看。
        /// </summary>
        private static int RunBuild(
            TuiLaunchOptions launch,
            DirectoryInfo source,
            FileInfo output,
            FileInfo configFile,
            bool userProvided,
            IReadOnlyDictionary<string, string> overrides,
            TuiLogSink sink
        ) {
            var options = new BuildSubcommandOptions(
                source, output, configFile, launch.Verbose, launch.TemplateDir, overrides
            );
            var runner = new Pipeline.BuildPipelineRunner(sink, new ManifestResourceManager());

            var exitCode = ExitCodes.Success;
            AnsiConsole.Live(RenderLog(sink))
                .AutoClear(false)
                .Start(ctx => {
                    // 构建在后台线程执行：Live 渲染必须占有主线程才能持续刷新
                    var buildTask = Task.Run(() => runner.Run(options, userProvided));
                    while (!buildTask.IsCompleted) {
                        ctx.UpdateTarget(RenderLog(sink));
                        ctx.Refresh();
                        Thread.Sleep(RefreshIntervalMs);
                    }
                    exitCode = buildTask.GetAwaiter().GetResult();
                    ctx.UpdateTarget(RenderLog(sink));
                    ctx.Refresh();
                });

            AnsiConsole.MarkupLine(exitCode == ExitCodes.Success
                ? $"[green]构建成功[/] → {Markup.Escape(output.FullName)}"
                : $"[red]构建失败[/]（退出码 {exitCode}）");
            return exitCode;
        }

        /// <summary>把日志快照渲染为带边框的面板，只显示能塞进当前终端高度的末尾若干行。</summary>
        private static IRenderable RenderLog(TuiLogSink sink) {
            var entries = sink.Snapshot();
            var visibleLines = Math.Max(5, SafeWindowHeight() - 8);
            var start = Math.Max(0, entries.Count - visibleLines);

            var rows = new List<IRenderable>();
            for (var i = start; i < entries.Count; i++) {
                rows.Add(new Markup(Colorize(entries[i])));
            }
            if (rows.Count == 0) {
                rows.Add(new Markup("[grey]等待构建输出…[/]"));
            }

            return new Panel(new Rows(rows))
                .Header("[bold]构建日志[/]")
                .Border(BoxBorder.Rounded)
                .Expand();
        }

        private static string Colorize(TuiLogEntry entry) {
            var text = Markup.Escape(entry.Message);
            return entry.Level switch {
                LogLevel.ERROR => $"[red]{text}[/]",
                LogLevel.WARNING => $"[yellow]{text}[/]",
                LogLevel.DEBUG => $"[grey]{text}[/]",
                _ => $"[white]{text}[/]",
            };
        }

        /// <summary>输出被重定向（非终端）时 <see cref="Console.WindowHeight"/> 会抛异常，此处兜底。</summary>
        private static int SafeWindowHeight() {
            try {
                return Console.WindowHeight;
            } catch (Exception) {
                return 30;
            }
        }
    }
}
