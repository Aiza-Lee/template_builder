using System.Diagnostics;
using Core.Models;
using Core.Pipeline;
using Spectre.Console;
using Spectre.Console.Rendering;
using Utils;
using Utils.Exceptions;

namespace Core.Tui {
    /// <summary>
    /// TUI 交互模式会话：提供品牌 ASCII 艺术字 Banner、步骤向导、CLI 全参数智能输入、
    /// 分类配置调优中心与实时构建监控仪表板。
    /// </summary>
    internal sealed class TuiRunner(ILogger bootstrapLogger) {
        private const int LogCapacity = 400;
        private const int RefreshIntervalMs = 120;

        private readonly ILogger _bootstrapLogger = bootstrapLogger;

        public int Run(TuiLaunchOptions launch) {
            AnsiConsole.Clear();
            RenderBanner();

            var session = new ProjectSession(_bootstrapLogger) {
                SourceDir = launch.SourceDir,
                OutputPdf = launch.OutputPdf,
                ConfigFile = launch.ConfigFile,
                UserProvidedConfig = launch.UserProvidedConfig,
                TemplateDir = launch.TemplateDir,
                Verbose = launch.Verbose
            };

            var currentStep = 1;
            var sink = new TuiLogSink(LogCapacity);
            sink.SetLevel(session.Verbose ? LogLevel.DEBUG : LogLevel.INFO);

            while (true) {
                switch (currentStep) {
                    case 1: {
                        // 步骤 1: 路径与运行参数向导 (与非 TUI 参数全面对齐)
                        RenderBreadcrumb(1, "基础路径与运行参数");
                        session.SourceDir = PromptSourceDir(session.SourceDir);
                        session.OutputPdf = PromptOutputPdf(session.OutputPdf, session.SourceDir);

                        var oldConfigPath = session.ConfigFile?.FullName;
                        var (configFile, userProvided) = PromptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = configFile;
                        session.UserProvidedConfig = userProvided;

                        var (templateDir, verbose) = PromptAdvancedOptions(launch, session.TemplateDir, session.Verbose);
                        session.TemplateDir = templateDir;
                        session.Verbose = verbose;
                        sink.SetLevel(session.Verbose ? LogLevel.DEBUG : LogLevel.INFO);

                        // 首次进入或用户在向导中切换了配置文件时，加载项目配置（带损坏/缺失恢复机制）
                        if (!string.Equals(oldConfigPath, configFile.FullName, StringComparison.OrdinalIgnoreCase)
                            || session.Configuration == null || session.Configuration.Items.Count == 0) {
                            LoadOrRecoverConfiguration(session, launch, _bootstrapLogger);
                        }

                        currentStep = 2;
                        break;
                    }

                    case 2: {
                        // 步骤 2: 配置调优中心 (7 大分类 + 全量 Schema 动态覆盖)
                        AnsiConsole.WriteLine();
                        RenderBreadcrumb(2, "配置调优中心");
                        var goBack = PromptConfigMenu(session, launch, sink);

                        if (goBack) {
                            AnsiConsole.MarkupLine("[yellow]正在返回步骤 1：重新设置基础路径与运行参数...[/]");
                            AnsiConsole.WriteLine();
                            currentStep = 1;
                        } else {
                            currentStep = 3;
                        }
                        break;
                    }

                    case 3: {
                        // 步骤 3: 实时监控与构建仪表板
                        AnsiConsole.WriteLine();
                        RenderBreadcrumb(3, "执行构建与实时监控");
                        _ = RunBuild(
                            session.SourceDir!,
                            session.OutputPdf!,
                            session.ConfigFile ?? new FileInfo("config.json"),
                            session.UserProvidedConfig,
                            session.TemplateDir,
                            session.Verbose,
                            session.Configuration?.GetActiveOverrides() ?? new Dictionary<string, string>(),
                            sink,
                            session
                        );

                        currentStep = 4;
                        break;
                    }

                    case 4: {
                        // 步骤 4: 构建结果操作与连续调整中心
                        var nextStep = PromptResultActionMenu(session, launch, sink);
                        if (nextStep == 0) {
                            return session.LastBuildResult?.ExitCode ?? ExitCodes.Success;
                        }
                        currentStep = nextStep;
                        break;
                    }
                }
            }
        }

        private static void RenderBanner() {
            TuiViewRenderer.RenderDynamicBanner(AnsiConsole.Console, SafeWindowWidth());
        }

        private static void RenderBreadcrumb(int step, string title) {
            TuiViewRenderer.RenderBreadcrumb(AnsiConsole.Console, step, title);
        }

        private (FileInfo ConfigFile, bool UserProvided) ResolveDefaultConfig(TuiLaunchOptions launch) {
            try {
                return new ConfigPathResolver(_bootstrapLogger).Resolve(
                    launch.ConfigFile,
                    launch.UserProvidedConfig,
                    launch.EnsureDefaultConfig
                );
            } catch (InvalidArgumentException) {
                // 如果用户启动 TUI 时在命令行带了不存在的 -c 参数，保留该 FileInfo 并标记为 UserProvided = true，
                // 由后续的向导和 LoadOrRecoverConfiguration 友好处理，避免直接闪退
                return (launch.ConfigFile ?? launch.EnsureDefaultConfig(), true);
            }
        }

        private static DirectoryInfo PromptSourceDir(DirectoryInfo? initial) {
            var defaultPath = initial is { Exists: true } ? initial.FullName : null;
            if (string.IsNullOrEmpty(defaultPath)) {
                if (Directory.Exists("./src")) defaultPath = Path.GetFullPath("./src");
                else if (Directory.Exists("./code")) defaultPath = Path.GetFullPath("./code");
                else defaultPath = Directory.GetCurrentDirectory();
            }

            var input = TuiLineEditor.ReadLine(
                "[bold]源文件目录[/] [grey](支持 ←/→ 移动光标编辑)[/]：",
                defaultPath,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, "路径不能为空");
                    return Directory.Exists(path) ? (true, null) : (false, $"目录不存在：{path}");
                }
            );

            var chosen = (input ?? defaultPath).Trim();
            return new DirectoryInfo(chosen);
        }

        private static FileInfo PromptOutputPdf(FileInfo? initial, DirectoryInfo source) {
            var hasUsableInitial = initial?.Directory is { Exists: true };
            var suggested = hasUsableInitial
                ? initial!.FullName
                : Path.Combine(
                    source.Parent?.FullName ?? source.FullName,
                    "preview",
                    source.Name + ".pdf"
                );

            var input = TuiLineEditor.ReadLine(
                "[bold]输出 PDF 路径[/] [grey](支持 ←/→ 移动光标编辑)[/]：",
                suggested,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, "输出路径不能为空");
                    if (Directory.Exists(path)) return (false, "输出路径不能是目录");
                    try {
                        return string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetFullPath(path)))
                            ? (false, "无法解析输出目录")
                            : (true, null);
                    } catch {
                        return (false, "输出路径非法");
                    }
                }
            );

            var chosen = (input ?? suggested).Trim();
            return new FileInfo(chosen);
        }

        private void LoadOrRecoverConfiguration(
            ProjectSession session,
            TuiLaunchOptions launch,
            ILogger logger
        ) {
            while (true) {
                if (session.ConfigFile == null) {
                    session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                    session.UserProvidedConfig = false;
                    return;
                }

                if (!File.Exists(session.ConfigFile.FullName)) {
                    AnsiConsole.WriteLine();
                    var panel = new Panel(
                        new Markup(
                            $"[bold red]✘ 配置文件不存在：[/]\n" +
                            $"  文件路径：[yellow]{Markup.Escape(session.ConfigFile.FullName)}[/]\n\n" +
                            $"[grey]文件在磁盘上缺失。请在外部创建/恢复该文件，或选择其他恢复方案。[/]"
                        )
                    ) {
                        Header = new PanelHeader("[bold red] 配置文件缺失 [/]"),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();

                    var choice = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[yellow]请选择恢复操作：[/]")
                            .AddChoices(
                                "外部创建/恢复后重新加载",
                                "重新输入/选择配置文件路径",
                                "使用内置默认配置继续 (不依赖外部配置)",
                                "退出程序"
                            )
                    );

                    if (choice == "外部创建/恢复后重新加载") {
                        continue;
                    }

                    if (choice == "重新输入/选择配置文件路径") {
                        var (newFile, isUser) = PromptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = newFile;
                        session.UserProvidedConfig = isUser;
                        continue;
                    }

                    if (choice == "使用内置默认配置继续 (不依赖外部配置)") {
                        session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                        session.ConfigFile = null;
                        session.UserProvidedConfig = false;
                        AnsiConsole.MarkupLine("[yellow]✔ 已切换为内置默认配置。[/]");
                        return;
                    }

                    Environment.Exit(ExitCodes.InvalidArguments);
                    return;
                }

                try {
                    var content = File.ReadAllText(session.ConfigFile.FullName);
                    var strictness = session.UserProvidedConfig ? ConfigStrictness.Strict : ConfigStrictness.Lax;
                    var config = new ProjectConfiguration(logger, strictness);
                    config.LoadProjectConfig(content, session.ConfigFile.FullName);

                    // 如果此前会话已有覆盖项，保留覆盖项
                    if (session.Configuration != null) {
                        foreach (var (k, v) in session.Configuration.GetActiveOverrides()) {
                            config.TrySetOverride(k, v);
                        }
                    }

                    session.Configuration = config;
                    return;
                } catch (Exception ex) {
                    AnsiConsole.WriteLine();
                    var panel = new Panel(
                        new Markup(
                            $"[bold red]✘ 配置文件损坏或无法解析：[/]\n" +
                            $"  文件路径：[yellow]{Markup.Escape(session.ConfigFile.FullName)}[/]\n" +
                            $"  错误信息：[red]{Markup.Escape(ex.Message)}[/]\n\n" +
                            $"[grey]请在外部编辑器中修复该文件，或重新选择有效配置文件。[/]"
                        )
                    ) {
                        Header = new PanelHeader("[bold red] 配置文件损坏 [/]"),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();

                    var choice = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[yellow]请选择恢复操作：[/]")
                            .AddChoices(
                                "外部修复后重新加载",
                                "重新输入/选择其他配置文件路径",
                                "使用内置默认配置继续 (忽略损坏配置)",
                                "退出程序"
                            )
                    );

                    if (choice == "外部修复后重新加载") {
                        continue;
                    }

                    if (choice == "重新输入/选择其他配置文件路径") {
                        var (newFile, isUser) = PromptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = newFile;
                        session.UserProvidedConfig = isUser;
                        continue;
                    }

                    if (choice == "使用内置默认配置继续 (忽略损坏配置)") {
                        session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                        session.ConfigFile = null;
                        session.UserProvidedConfig = false;
                        AnsiConsole.MarkupLine("[yellow]✔ 已切换为内置默认配置。[/]");
                        return;
                    }

                    Environment.Exit(ExitCodes.InvalidArguments);
                    return;
                }
            }
        }

        private (FileInfo ConfigFile, bool UserProvided) PromptConfigFile(
            TuiLaunchOptions launch,
            FileInfo? currentFile = null,
            bool? currentUserProvided = null
        ) {
            FileInfo defaultFile;
            bool wasProvided;
            if (currentFile != null) {
                defaultFile = currentFile;
                wasProvided = currentUserProvided ?? false;
            } else {
                var resolved = ResolveDefaultConfig(launch);
                defaultFile = resolved.ConfigFile;
                wasProvided = resolved.UserProvided;
            }

            var input = TuiLineEditor.ReadLine(
                "[bold]基础配置文件[/] [grey](支持 ←/→ 移动光标编辑)[/]：",
                defaultFile.FullName,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, "路径不能为空");
                    return File.Exists(path) ? (true, null) : (false, $"文件不存在：{path}");
                }
            );

            var chosen = (input ?? defaultFile.FullName).Trim();
            var chosenFile = new FileInfo(chosen);
            var isUserProvided = wasProvided || !string.Equals(chosenFile.FullName, defaultFile.FullName, StringComparison.OrdinalIgnoreCase);
            return (chosenFile, isUserProvided);
        }

        private static (DirectoryInfo? TemplateDir, bool Verbose) PromptAdvancedOptions(
            TuiLaunchOptions launch,
            DirectoryInfo? currentTemplateDir = null,
            bool? currentVerbose = null
        ) {
            var initialTemplateDir = currentTemplateDir ?? launch.TemplateDir;
            var initialVerbose = currentVerbose ?? launch.Verbose;

            var modify = AnsiConsole.Prompt(
                new ConfirmationPrompt("[bold]是否配置高级参数[/]（外部模板目录 / 详细调试日志）？") {
                    DefaultValue = false
                }
            );

            if (!modify) {
                return (initialTemplateDir, initialVerbose);
            }

            var tmplInput = TuiLineEditor.ReadLine(
                "[bold]外部模板目录[/] [grey](直接回车使用内嵌模板，支持 ←/→ 移动光标)[/]：",
                initialTemplateDir?.FullName,
                raw => {
                    if (string.IsNullOrWhiteSpace(raw)) return (true, null);
                    return Directory.Exists(raw.Trim())
                        ? (true, null)
                        : (false, $"目录不存在：{raw.Trim()}");
                }
            );

            var tmplRaw = (tmplInput ?? initialTemplateDir?.FullName)?.Trim();
            var templateDir = string.IsNullOrEmpty(tmplRaw) ? null : new DirectoryInfo(tmplRaw);

            var verbose = AnsiConsole.Prompt(
                new ConfirmationPrompt("[bold]启用详细调试日志 (DEBUG)[/]？") {
                    DefaultValue = initialVerbose
                }
            );

            return (templateDir, verbose);
        }

        private bool PromptConfigMenu(ProjectSession session, TuiLaunchOptions launch, ILogger sink) {
            var config = session.Configuration ?? new ProjectConfiguration(null, ConfigStrictness.Lax);
            string? lastSelectedId = session.LastFocusedCategoryId;

            while (true) {
                var menuItems = new List<TuiMenuItem>();
                menuItems.Add(new TuiMenuItem("BUILD", "完成配置，开始构建 (直接开始)", action: TuiMenuAction.Build));

                var activeOverrides = config.GetActiveOverrides();
                foreach (var cat in TuiSchemaCatalog.Categories) {
                    var overriddenCount = cat.Fields.Count(f => activeOverrides.ContainsKey(f.Key));
                    var details = overriddenCount > 0 ? $"{cat.Fields.Count} 项，已覆盖 {overriddenCount} 项" : $"{cat.Fields.Count} 项";
                    menuItems.Add(new TuiMenuItem(cat.Id, cat.Title, details, isOverridden: overriddenCount > 0, tag: cat));
                }

                if (activeOverrides.Count > 0) {
                    menuItems.Add(new TuiMenuItem("INSPECT", $"查看当前全部覆盖项 ({activeOverrides.Count} 项)", action: TuiMenuAction.Inspect));
                    menuItems.Add(new TuiMenuItem("CLEAR", "清空所有覆盖项 (恢复项目/内置配置)", action: TuiMenuAction.Clear));
                }

                // 保存当前配置到文件
                menuItems.Add(new TuiMenuItem("SAVE", "保存当前配置到项目文件 (持久化所有设置)", action: TuiMenuAction.Save));

                // 重新加载配置文件
                menuItems.Add(new TuiMenuItem("RELOAD", "重新加载配置文件 (同步磁盘外部变更)", action: TuiMenuAction.Reload));

                // 显式返回上一步
                menuItems.Add(new TuiMenuItem("BACK_STEP1", "返回上一步 (修改基础路径与参数)", action: TuiMenuAction.Back));

                string sourceLabel = session.ConfigFile == null
                    ? "[grey]内置默认配置[/]"
                    : session.UserProvidedConfig
                        ? $"[bold green]用户项目配置[/] ({Markup.Escape(session.ConfigFile.FullName)})"
                        : $"[cyan]默认项目配置[/] ({Markup.Escape(session.ConfigFile.FullName)})";

                var promptTitle = $"[bold cyan]配置调优导航中心[/]\n[grey]当前配置来源：[/]{sourceLabel} [grey]| 生效覆盖：[/][yellow]{activeOverrides.Count}[/] [grey]项[/]\n[grey](Enter/→ 确认进入，←/Esc 返回上级)[/]：";
                var chosen = TuiMenu.Prompt(promptTitle, menuItems, initialSelectedId: lastSelectedId);
                lastSelectedId = chosen.Id;
                session.LastFocusedCategoryId = chosen.Id;

                if (chosen.IsBuild) {
                    var validation = Core.Validation.ConfigValidator.Validate(config);
                    if (!validation.IsValid) {
                        AnsiConsole.MarkupLine($"[bold red]✘ 当前配置存在 {validation.Errors.Count} 处错误，无法开始构建：[/]");
                        foreach (var err in validation.Errors) {
                            AnsiConsole.MarkupLine($"  [red]• {Markup.Escape(err.PropertyPath)}[/]: {Markup.Escape(err.Message)} [grey](期望：{Markup.Escape(err.Expected)})[/]");
                        }
                        AnsiConsole.MarkupLine("[grey]按任意键返回调整配置...[/]");
                        Console.ReadKey(intercept: true);
                        continue;
                    }

                    if (session.SourceDir != null) {
                        var preflight = Core.Validation.SourcePreflight.Inspect(session.SourceDir, config, sink);
                        if (!preflight.IsValid) {
                            AnsiConsole.MarkupLine($"[bold red]✘ 源文件预检失败，无法开始构建：[/]");
                            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(preflight.Message)}[/]");
                            if (!string.IsNullOrEmpty(preflight.Recommendation)) {
                                AnsiConsole.MarkupLine($"  [yellow]建议：{Markup.Escape(preflight.Recommendation)}[/]");
                            }
                            AnsiConsole.MarkupLine("[grey]按任意键返回调整参数...[/]");
                            Console.ReadKey(intercept: true);
                            continue;
                        }
                    }

                    break;
                }

                if (chosen.IsBack) {
                    return true;
                }

                if (chosen.IsSave) {
                    PromptSaveConfig(session, launch);
                    config = session.Configuration ?? new ProjectConfiguration(null, ConfigStrictness.Lax);
                    continue;
                }

                if (chosen.IsReload) {
                    LoadOrRecoverConfiguration(session, launch, sink);
                    config = session.Configuration ?? new ProjectConfiguration(null, ConfigStrictness.Lax);
                    AnsiConsole.MarkupLine("[green]✔ 配置文件已重新加载。[/]");
                    continue;
                }

                if (chosen.IsClear) {
                    foreach (var key in activeOverrides.Keys.ToList()) {
                        config.ClearOverride(key);
                    }
                    AnsiConsole.MarkupLine("[yellow]已清空全部运行时覆盖项。[/]");
                    continue;
                }

                if (chosen.Action == TuiMenuAction.Inspect) {
                    ShowOverridesTable(config.GetActiveOverrides(), config);
                    continue;
                }

                if (chosen.Tag is TuiCategoryGroup targetCategory) {
                    EditCategoryFields(targetCategory, config);
                    lastSelectedId = targetCategory.Id;
                    session.LastFocusedCategoryId = targetCategory.Id;
                }
            }

            var finalOverrides = config.GetActiveOverrides();
            string finalSourceLabel = session.ConfigFile == null
                ? "内置默认配置"
                : session.UserProvidedConfig
                    ? $"用户项目配置 ({session.ConfigFile.FullName})"
                    : $"默认项目配置 ({session.ConfigFile.FullName})";

            AnsiConsole.MarkupLine($"[bold]配置来源[/]：{finalSourceLabel}");
            AnsiConsole.MarkupLine(finalOverrides.Count == 0
                ? "[grey]未覆盖任何配置项，将完全按配置文件/内置默认构建。[/]"
                : $"[bold #ffd600]已设置 {finalOverrides.Count} 项运行时覆盖（仅本次生效，不修改文件）。[/]");

            return false;
        }

        private static void ShowOverridesTable(IReadOnlyDictionary<string, string> overrides, ProjectConfiguration configuration) {
            TuiViewRenderer.RenderOverridesTable(AnsiConsole.Console, overrides, configuration);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]按 Enter 返回主菜单...[/]");
            Console.ReadLine();
        }

        private static void EditCategoryFields(TuiCategoryGroup category, ProjectConfiguration config) {
            string? lastSelectedFieldKey = null;
            while (true) {
                var menuItems = new List<TuiMenuItem>();
                menuItems.Add(new TuiMenuItem("BACK", "返回上级菜单", action: TuiMenuAction.Back));

                foreach (var field in category.Fields) {
                    var item = config.GetItem(field.Key);
                    var isOverridden = item?.IsOverridden == true;
                    var cur = item?.EffectiveValue;
                    var displayCur = string.IsNullOrEmpty(cur) ? "<空>" : cur;
                    if (isOverridden) {
                        displayCur += " [已覆盖]";
                    } else if (item?.HasProjectValue == true) {
                        displayCur += " [项目]";
                    }
                    menuItems.Add(new TuiMenuItem(field.Key, field.Label, displayCur, isOverridden: isOverridden, tag: field));
                }

                var chosen = TuiMenu.Prompt(
                    $"[bold cyan]{Markup.Escape(category.Title)}[/] — [grey]{Markup.Escape(category.Description)}[/]",
                    menuItems,
                    initialSelectedId: lastSelectedFieldKey
                );

                if (chosen.IsBack) {
                    break;
                }

                lastSelectedFieldKey = chosen.Id;

                if (chosen.Tag is not TuiConfigField selectedField) continue;

                EditSingleField(selectedField, config);
            }
        }

        private static void EditSingleField(TuiConfigField field, ProjectConfiguration config) {
            var item = config.GetItem(field.Key);
            if (item == null) return;

            TuiViewRenderer.RenderFieldCard(AnsiConsole.Console, field, item);

            switch (field.Kind) {
                case TuiFieldKind.Choice:
                    PromptChoiceField(field, item, config);
                    break;
                case TuiFieldKind.Toggle:
                    PromptToggleField(field, item, config);
                    break;
                default:
                    PromptTextField(field, item, config);
                    break;
            }
        }

        private static void PromptChoiceField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var menuItems = new List<TuiMenuItem>();
            menuItems.Add(new TuiMenuItem("BACK", "返回（不修改）", action: TuiMenuAction.Back));

            foreach (var choice in field.Choices ?? []) {
                var isCurrent = string.Equals(choice, item.EffectiveValue, StringComparison.Ordinal);
                var label = choice;
                if (string.Equals(choice, item.BuiltinValue, StringComparison.Ordinal)) {
                    label += " [内置默认]";
                }
                if (item.HasProjectValue && string.Equals(choice, item.ProjectValue, StringComparison.Ordinal)) {
                    label += " [项目配置]";
                }
                menuItems.Add(new TuiMenuItem(choice, label, isOverridden: isCurrent));
            }

            if (item.HasProjectValue && item.IsOverridden) {
                menuItems.Add(new TuiMenuItem("__RESET_PROJECT__", $"恢复项目配置值 ({item.ProjectValue})"));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.Ordinal))) {
                menuItems.Add(new TuiMenuItem("__RESET_BUILTIN__", $"恢复内置默认值 ({item.BuiltinValue})"));
            }

            var chosen = TuiMenu.Prompt($"[bold]请选择 {Markup.Escape(field.Label)}[/]：", menuItems);
            if (chosen.IsBack) {
                AnsiConsole.MarkupLine("[grey]已取消，未作任何修改。[/]");
                return;
            }

            if (chosen.Id == "__RESET_PROJECT__") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复项目配置值 {Markup.Escape(field.Label)} = {Markup.Escape(item.ProjectValue ?? "")}[/]");
                return;
            }

            if (chosen.Id == "__RESET_BUILTIN__") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复内置默认值 {Markup.Escape(field.Label)} = {Markup.Escape(item.BuiltinValue ?? "")}[/]");
                return;
            }

            ApplyFieldValue(field, item, chosen.Id, config);
        }

        private static void PromptToggleField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var menuItems = new List<TuiMenuItem>();
            menuItems.Add(new TuiMenuItem("BACK", "返回（不修改）", action: TuiMenuAction.Back));

            var isTrueCurrent = string.Equals(item.EffectiveValue, "true", StringComparison.OrdinalIgnoreCase);
            var trueLabel = "开启 (true)";
            var falseLabel = "关闭 (false)";
            if (string.Equals(item.BuiltinValue, "true", StringComparison.OrdinalIgnoreCase)) trueLabel += " [内置默认]";
            if (string.Equals(item.BuiltinValue, "false", StringComparison.OrdinalIgnoreCase)) falseLabel += " [内置默认]";
            if (item.HasProjectValue) {
                if (string.Equals(item.ProjectValue, "true", StringComparison.OrdinalIgnoreCase)) trueLabel += " [项目配置]";
                if (string.Equals(item.ProjectValue, "false", StringComparison.OrdinalIgnoreCase)) falseLabel += " [项目配置]";
            }

            menuItems.Add(new TuiMenuItem("true", trueLabel, isOverridden: isTrueCurrent));
            menuItems.Add(new TuiMenuItem("false", falseLabel, isOverridden: !isTrueCurrent));

            if (item.HasProjectValue && item.IsOverridden) {
                menuItems.Add(new TuiMenuItem("__RESET_PROJECT__", $"恢复项目配置值 ({item.ProjectValue})"));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.OrdinalIgnoreCase))) {
                menuItems.Add(new TuiMenuItem("__RESET_BUILTIN__", $"恢复内置默认值 ({item.BuiltinValue})"));
            }

            var chosen = TuiMenu.Prompt($"[bold]是否开启 {Markup.Escape(field.Label)}[/]：", menuItems);
            if (chosen.IsBack) {
                AnsiConsole.MarkupLine("[grey]已取消，未作任何修改。[/]");
                return;
            }

            if (chosen.Id == "__RESET_PROJECT__") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复项目配置值 {Markup.Escape(field.Label)} = {Markup.Escape(item.ProjectValue ?? "")}[/]");
                return;
            }

            if (chosen.Id == "__RESET_BUILTIN__") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复内置默认值 {Markup.Escape(field.Label)} = {Markup.Escape(item.BuiltinValue ?? "")}[/]");
                return;
            }

            ApplyFieldValue(field, item, chosen.Id, config);
        }

        private static void PromptTextField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var isNumber = field.Kind == TuiFieldKind.Number;
            var dispEffective = string.IsNullOrEmpty(item.EffectiveValue) ? "<空>" : item.EffectiveValue;
            var dispProject = item.ProjectValue != null ? (string.IsNullOrEmpty(item.ProjectValue) ? "<空>" : item.ProjectValue) : "-";
            var dispBuiltin = string.IsNullOrEmpty(item.BuiltinValue) ? "<空>" : item.BuiltinValue;

            var actionItems = new List<TuiMenuItem>();
            actionItems.Add(new TuiMenuItem("EDIT", $"编辑具体取值 (当前: {dispEffective})"));

            if (!isNumber) {
                actionItems.Add(new TuiMenuItem("SET_EMPTY", "清空取值 (设为空字符串 <空>)"));
            }

            if (item.HasProjectValue && item.IsOverridden) {
                actionItems.Add(new TuiMenuItem("RESET_PROJECT", $"恢复项目配置值 ({dispProject})"));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.Ordinal))) {
                actionItems.Add(new TuiMenuItem("RESET_BUILTIN", $"恢复内置默认值 ({dispBuiltin})"));
            }

            actionItems.Add(new TuiMenuItem("BACK", "返回（不修改）", action: TuiMenuAction.Back));

            var chosen = TuiMenu.Prompt($"[bold]请选择对【{Markup.Escape(field.Label)}】的操作[/]：", actionItems);
            if (chosen.IsBack) {
                AnsiConsole.MarkupLine("[grey]已取消，未作任何修改。[/]");
                return;
            }

            if (chosen.Id == "SET_EMPTY") {
                ApplyFieldValue(field, item, string.Empty, config);
                return;
            }

            if (chosen.Id == "RESET_PROJECT") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复项目配置值 {Markup.Escape(field.Label)} = {Markup.Escape(dispProject)}[/]");
                return;
            }

            if (chosen.Id == "RESET_BUILTIN") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]✔ 已恢复内置默认值 {Markup.Escape(field.Label)} = {Markup.Escape(dispBuiltin)}[/]");
                return;
            }

            if (chosen.Id == "EDIT") {
                string? input;
                if (isNumber) {
                    var rangeHint = field.Minimum.HasValue && field.Maximum.HasValue
                        ? $" ({field.Minimum.Value} ~ {field.Maximum.Value})"
                        : (field.Minimum.HasValue ? $" (>= {field.Minimum.Value})" : "");
                    input = TuiLineEditor.ReadLine(
                        $"[bold]请输入 {Markup.Escape(field.Label)}[/] [grey](整数{rangeHint}，直接回车确认，Esc 取消)[/]：",
                        item.EffectiveValue,
                        raw => {
                            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, raw);
                            return isValid ? (true, null) : (false, err?.Message);
                        }
                    );
                } else if (field.Kind == TuiFieldKind.Array) {
                    input = TuiLineEditor.ReadLine(
                        $"[bold]请输入 {Markup.Escape(field.Label)}[/] [grey](逗号分隔，直接回车确认，Esc 取消)[/]：",
                        item.EffectiveValue
                    );
                } else {
                    input = TuiLineEditor.ReadLine(
                        $"[bold]请输入 {Markup.Escape(field.Label)}[/] [grey](直接回车确认，Esc 取消)[/]：",
                        item.EffectiveValue,
                        raw => {
                            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, raw);
                            return isValid ? (true, null) : (false, err?.Message);
                        }
                    );
                }

                if (input == null) {
                    AnsiConsole.MarkupLine("[grey]已取消编辑，未作任何修改。[/]");
                    return;
                }

                var finalVal = isNumber ? input.Trim() : input;
                ApplyFieldValue(field, item, finalVal, config);
            }
        }

        private static void ApplyFieldValue(TuiConfigField field, ConfigItem item, string value, ProjectConfiguration config) {
            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, value);
            if (!isValid && err != null) {
                AnsiConsole.MarkupLine($"[red]✘ 校验失败：{Markup.Escape(err.Message)}（期望：{Markup.Escape(err.Expected)}；建议：{Markup.Escape(err.Recommendation)}）[/]");
                return;
            }

            if (item.HasProjectValue && string.Equals(value, item.ProjectValue, StringComparison.Ordinal)) {
                config.ResetToProject(field.Key);
                var disp = string.IsNullOrEmpty(value) ? "<空>" : value;
                AnsiConsole.MarkupLine($"[grey]✔ 与项目配置一致，恢复为项目值 {Markup.Escape(field.Label)} = {Markup.Escape(disp)}[/]");
            } else if (!item.HasProjectValue && string.Equals(value, item.BuiltinValue, StringComparison.Ordinal)) {
                config.ClearOverride(field.Key);
                var disp = string.IsNullOrEmpty(value) ? "<空>" : value;
                AnsiConsole.MarkupLine($"[grey]✔ 与内置默认一致，无需覆盖 {Markup.Escape(field.Label)} = {Markup.Escape(disp)}[/]");
            } else {
                var success = config.TrySetOverride(field.Key, value);
                var disp = string.IsNullOrEmpty(value) ? "<空>" : value;
                if (success) {
                    AnsiConsole.MarkupLine($"[bold #ffd600]✔ 已设置运行时覆盖 {Markup.Escape(field.Label)} = {Markup.Escape(disp)}[/]");
                } else {
                    AnsiConsole.MarkupLine($"[red]✘ 无法设置配置项 {Markup.Escape(field.Label)}，值非法或无法解析[/]");
                }
            }
        }

        internal static (Dictionary<string, string> Current, Dictionary<string, string> Defaults) ReadConfigValues(FileInfo configFile) {
            var currentValues = new Dictionary<string, string>(StringComparer.Ordinal);
            var defaultValues = new Dictionary<string, string>(StringComparer.Ordinal);

            var config = new ProjectConfiguration(null, ConfigStrictness.Lax);
            if (configFile.Exists) {
                var content = File.ReadAllText(configFile.FullName);
                config.LoadProjectConfig(content, configFile.FullName);
            }

            foreach (var field in TuiSchemaCatalog.AllFields) {
                var item = config.GetItem(field.Key);
                defaultValues[field.Key] = item?.BuiltinValue ?? string.Empty;
                currentValues[field.Key] = item?.EffectiveValue ?? string.Empty;
            }

            return (currentValues, defaultValues);
        }

        private static int RunBuild(
            DirectoryInfo source,
            FileInfo output,
            FileInfo configFile,
            bool userProvided,
            DirectoryInfo? templateDir,
            bool verbose,
            IReadOnlyDictionary<string, string> overrides,
            TuiLogSink sink,
            ProjectSession? session = null
        ) {
            var options = new BuildSubcommandOptions(
                source, output, configFile, verbose, templateDir, overrides
            );
            var runner = new BuildPipelineRunner(sink, new ManifestResourceManager());

            string configSourceDesc = configFile == null || !configFile.Exists
                ? "内置默认配置"
                : userProvided
                    ? $"用户项目配置 ({configFile.FullName})"
                    : $"默认项目配置 ({configFile.FullName})";

            sink.Info($"[构建准备] 配置来源: {configSourceDesc}，生效覆盖: {overrides.Count} 项。");

            BuildResult? result = null;
            var stopwatch = Stopwatch.StartNew();

            AnsiConsole.Live(RenderLog(sink))
                .AutoClear(false)
                .Start(ctx => {
                    var buildTask = Task.Run(() => runner.RunBuild(options, userProvided, session?.Configuration));
                    while (!buildTask.IsCompleted) {
                        ctx.UpdateTarget(RenderLog(sink));
                        ctx.Refresh();
                        Thread.Sleep(RefreshIntervalMs);
                    }
                    result = buildTask.GetAwaiter().GetResult();
                    ctx.UpdateTarget(RenderLog(sink));
                    ctx.Refresh();
                });

            stopwatch.Stop();
            var finalResult = result ?? BuildResult.CreateFailure(ExitCodes.XelatexFailure, BuildStatus.Failed, "未知错误");
            if (session != null) {
                session.LastBuildResult = finalResult;
            }
            RenderBuildSummary(finalResult, output, stopwatch.Elapsed.TotalSeconds, overrides.Count, configSourceDesc);
            return finalResult.ExitCode;
        }

        private static void RenderBuildSummary(BuildResult result, FileInfo output, double elapsedSeconds, int overridesCount, string configSourceDesc) {
            AnsiConsole.WriteLine();
            if (result.IsSuccess) {
                output.Refresh();
                var sizeText = output.Exists ? $"{output.Length / 1024.0:F1} KB" : "未知";

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn("[bold]指标[/]").Centered());
                table.AddColumn(new TableColumn("[bold]详情[/]"));
                var statusText = result.Status == BuildStatus.SuccessWithWarnings
                    ? "[yellow]SUCCESS (成功，附警告)[/]"
                    : "[bold green]SUCCESS (成功)[/]";
                table.AddRow("[green]构建结果[/]", statusText);
                table.AddRow("PDF 产物路径", Markup.Escape(result.OutputPdf?.FullName ?? output.FullName));
                table.AddRow("产物文件大小", sizeText);
                table.AddRow("配置来源", Markup.Escape(configSourceDesc));
                table.AddRow("编译轮数", $"{result.PassesCompleted}/{result.TotalPasses}");
                table.AddRow("总编译耗时", $"{elapsedSeconds:F1} 秒");
                table.AddRow("生效配置覆盖", $"{overridesCount} 项");
                if (result.Warnings.Count > 0) {
                    table.AddRow("[yellow]构建警告[/]", string.Join("\n", result.Warnings.Select(w => Markup.Escape(w))));
                }

                AnsiConsole.Write(new Panel(table)
                    .Header("[bold green] 构建完成仪表板 [/]")
                    .Border(BoxBorder.Rounded));
                AnsiConsole.MarkupLine("[bold green]构建成功！[/] [grey]PDF 产物已就绪。[/]");
            } else {
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn("[bold]指标[/]").Centered());
                table.AddColumn(new TableColumn("[bold]详情[/]"));
                table.AddRow("[red]构建结果[/]", $"[bold red]FAILED ({result.Status}, 退出码 {result.ExitCode})[/]");
                table.AddRow("配置来源", Markup.Escape(configSourceDesc));
                if (!string.IsNullOrEmpty(result.FailureReason)) {
                    table.AddRow("[bold red]失败原因[/]", $"[bold red]{Markup.Escape(result.FailureReason)}[/]");
                }
                if (!string.IsNullOrEmpty(result.CulpritSourceFile)) {
                    var location = result.CulpritLineNumber.HasValue
                        ? $"{result.CulpritSourceFile}:{result.CulpritLineNumber.Value}"
                        : result.CulpritSourceFile;
                    table.AddRow("[yellow]定位源文件[/]", $"[bold yellow]{Markup.Escape(location)}[/]");
                }
                if (!string.IsNullOrEmpty(result.Recommendation)) {
                    table.AddRow("[green]排查建议[/]", $"[bold green]{Markup.Escape(result.Recommendation)}[/]");
                }
                if (!string.IsNullOrEmpty(result.DiagnosticPdfPath)) {
                    table.AddRow("诊断产物", $"[grey]{Markup.Escape(result.DiagnosticPdfPath)} (已保留供排查，未覆盖正式产物)[/]");
                }
                if (!string.IsNullOrEmpty(result.ErrorMessage)) {
                    table.AddRow("错误信息", Markup.Escape(result.ErrorMessage));
                }
                if (result.Diagnostics.Count > 0) {
                    table.AddRow("诊断摘要", string.Join("\n", result.Diagnostics.Select(d => Markup.Escape(d))));
                }
                if (!string.IsNullOrEmpty(result.LogFilePath)) {
                    table.AddRow("完整日志", Markup.Escape(result.LogFilePath));
                }
                table.AddRow("中断轮数", $"第 {result.PassesCompleted} 轮");
                table.AddRow("已耗时间", $"{elapsedSeconds:F1} 秒");

                AnsiConsole.Write(new Panel(table)
                    .Header("[bold red] 构建失败报告 [/]")
                    .Border(BoxBorder.Rounded));
                AnsiConsole.MarkupLine("[bold red]构建未成功完成。[/] [grey]请参考上方诊断信息或编译日志进行排查。[/]");
            }
        }

        private static IRenderable RenderLog(TuiLogSink sink) {
            var entries = sink.Snapshot();
            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn(new TableColumn("Message"));

            if (entries.Count == 0) {
                table.AddRow(new Markup("[grey]正在初始化构建流程...[/]"));
            } else {
                var window = entries.TakeLast(15);
                foreach (var entry in window) {
                    var levelColor = entry.Level switch {
                        LogLevel.ERROR => "red",
                        LogLevel.WARNING => "yellow",
                        LogLevel.DEBUG => "grey",
                        _ => "blue"
                    };
                    var line = $"[grey]{entry.Timestamp:HH:mm:ss}[/] [{levelColor}][[{entry.Level}]][/] {Markup.Escape(entry.Message)}";
                    table.AddRow(new Markup(line));
                }
            }

            return new Panel(table)
                .Header("[bold cyan] 实时编译输出 [/]")
                .Border(BoxBorder.Rounded);
        }

        private static int SafeWindowWidth() {
            try {
                return Console.WindowWidth > 0 ? Console.WindowWidth : 80;
            } catch {
                return 80;
            }
        }

        private static int PromptResultActionMenu(
            ProjectSession session,
            TuiLaunchOptions launch,
            TuiLogSink sink
        ) {
            var lastResult = session.LastBuildResult;
            var isSuccess = lastResult?.IsSuccess == true;

            while (true) {
                AnsiConsole.WriteLine();
                var menuItems = new List<TuiMenuItem>();

                if (isSuccess) {
                    menuItems.Add(new TuiMenuItem("REBUILD", "重新构建 (使用当前配置立即再次编译)", action: TuiMenuAction.Build));
                    if (session.OutputPdf?.Exists == true) {
                        menuItems.Add(new TuiMenuItem("OPEN_PDF", "打开生成的 PDF 文件 (系统默认阅读器)"));
                    }
                    menuItems.Add(new TuiMenuItem("ADJUST", "继续微调配置 (返回配置调优中心)"));
                    menuItems.Add(new TuiMenuItem("MODIFY_PATHS", "修改基础路径与参数 (返回步骤 1)"));
                    menuItems.Add(new TuiMenuItem("VIEW_LOG", "查看完整构建日志", action: TuiMenuAction.Inspect));
                    menuItems.Add(new TuiMenuItem("SAVE", "保存当前配置到项目文件 (持久化所有设置)", action: TuiMenuAction.Save));
                    menuItems.Add(new TuiMenuItem("EXIT", "完成并退出程序", action: TuiMenuAction.Back));
                } else {
                    menuItems.Add(new TuiMenuItem("REBUILD", "重新尝试构建", action: TuiMenuAction.Build));
                    menuItems.Add(new TuiMenuItem("ADJUST", "调整配置以修复错误 (返回配置调优中心)"));
                    menuItems.Add(new TuiMenuItem("MODIFY_PATHS", "修改基础路径与参数 (返回步骤 1)"));
                    if (!string.IsNullOrEmpty(lastResult?.DiagnosticPdfPath) && File.Exists(lastResult.DiagnosticPdfPath)) {
                        menuItems.Add(new TuiMenuItem("OPEN_DIAG_PDF", "查看诊断中间产物 PDF (排查未完全崩溃页面)"));
                    }
                    menuItems.Add(new TuiMenuItem("VIEW_LOG", "查看完整构建日志与错误堆栈", action: TuiMenuAction.Inspect));
                    menuItems.Add(new TuiMenuItem("SAVE", "保存当前配置到项目文件", action: TuiMenuAction.Save));
                    menuItems.Add(new TuiMenuItem("EXIT", "退出程序", action: TuiMenuAction.Back));
                }

                var title = isSuccess
                    ? "[bold green]✔ 构建成功！请选择后续操作[/] [grey](Enter/→ 确认进入，←/Esc 退出程序)[/]："
                    : "[bold red]✘ 构建未成功！请选择后续操作[/] [grey](Enter/→ 确认进入，←/Esc 退出程序)[/]：";

                var chosen = TuiMenu.Prompt(title, menuItems);

                if (chosen.IsBack || chosen.Id == "EXIT") {
                    return 0; // 0 表示退出
                }

                if (chosen.Id == "REBUILD" || chosen.IsBuild) {
                    return 3; // 重新触发步骤 3 构建
                }

                if (chosen.Id == "ADJUST") {
                    var problemCategory = MapFailureToCategory(lastResult);
                    if (problemCategory != null) {
                        session.LastFocusedCategoryId = problemCategory;
                        var catObj = TuiSchemaCatalog.Categories.FirstOrDefault(c => c.Id == problemCategory);
                        if (catObj != null) {
                            AnsiConsole.MarkupLine($"[yellow]💡 已为您自动聚焦至关联排查分类：[/][bold cyan]{Markup.Escape(catObj.Title)}[/]");
                        }
                    }
                    return 2; // 返回步骤 2
                }

                if (chosen.Id == "MODIFY_PATHS") {
                    return 1; // 返回步骤 1
                }

                if (chosen.Id == "OPEN_PDF") {
                    OpenPdfWithSystemViewer(session.OutputPdf!.FullName);
                    continue;
                }

                if (chosen.Id == "OPEN_DIAG_PDF") {
                    OpenPdfWithSystemViewer(lastResult!.DiagnosticPdfPath!);
                    continue;
                }

                if (chosen.Id == "VIEW_LOG" || chosen.Action == TuiMenuAction.Inspect) {
                    ShowFullLog(sink);
                    continue;
                }

                if (chosen.Id == "SAVE" || chosen.IsSave) {
                    PromptSaveConfig(session, launch);
                    continue;
                }
            }
        }

        private static void PromptSaveConfig(ProjectSession session, TuiLaunchOptions launch) {
            var suggestedPath = session.ConfigFile?.FullName;
            if (string.IsNullOrEmpty(suggestedPath)) {
                suggestedPath = session.SourceDir != null
                    ? Path.Combine(session.SourceDir.FullName, "config.json")
                    : Path.GetFullPath("config.json");
            }

            var input = TuiLineEditor.ReadLine(
                "[bold]保存配置文件路径[/] [grey](直接回车确认，支持 ←/→ 移动光标)[/]：",
                suggestedPath,
                raw => {
                    var p = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(p)) return (false, "路径不能为空");
                    try {
                        var dir = Path.GetDirectoryName(Path.GetFullPath(p));
                        return (true, null);
                    } catch {
                        return (false, "路径非法");
                    }
                }
            );

            if (string.IsNullOrWhiteSpace(input)) {
                AnsiConsole.MarkupLine("[grey]已取消保存配置。[/]");
                return;
            }

            var savePath = input.Trim();
            try {
                session.Configuration.SaveToFile(savePath);
                session.ConfigFile = new FileInfo(savePath);
                session.UserProvidedConfig = true;
                AnsiConsole.MarkupLine($"[bold green]✔ 配置已成功保存至文件：[/][cyan]{Markup.Escape(session.ConfigFile.FullName)}[/]");
                AnsiConsole.MarkupLine("[grey]所有修改项已写入配置文件，后续启动与连续构建将直接复用该配置。[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[bold red]✘ 保存配置文件失败：{Markup.Escape(ex.Message)}[/]");
            }
        }

        private static void OpenPdfWithSystemViewer(string filePath) {
            if (!File.Exists(filePath)) {
                AnsiConsole.MarkupLine($"[red]✘ 产物文件不存在：{Markup.Escape(filePath)}[/]");
                return;
            }

            try {
                if (OperatingSystem.IsWindows()) {
                    Process.Start(new ProcessStartInfo {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                } else if (OperatingSystem.IsMacOS()) {
                    Process.Start("open", [filePath]);
                } else {
                    Process.Start("xdg-open", [filePath]);
                }
                AnsiConsole.MarkupLine($"[green]✔ 已调用系统默认阅读器打开：[/][cyan]{Markup.Escape(filePath)}[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[yellow]无法自动启动阅读器：{Markup.Escape(ex.Message)}，请手动打开：{Markup.Escape(filePath)}[/]");
            }
        }

        private static void ShowFullLog(TuiLogSink sink) {
            var entries = sink.Snapshot();
            AnsiConsole.WriteLine();
            var rule = new Rule("[bold cyan]完整构建与诊断日志[/]");
            rule.LeftJustified();
            AnsiConsole.Write(rule);

            foreach (var entry in entries) {
                var prefix = entry.Level switch {
                    LogLevel.ERROR => "[bold red][ERROR][/]",
                    LogLevel.WARNING => "[bold yellow][WARN ][/]",
                    LogLevel.DEBUG => "[grey][DEBUG][/]",
                    _ => "[grey][INFO ][/]"
                };
                AnsiConsole.MarkupLine($"{prefix} {Markup.Escape(entry.Message)}");
            }

            AnsiConsole.Write(new Rule());
            AnsiConsole.MarkupLine("[grey]按 Enter 返回操作菜单...[/]");
            Console.ReadLine();
        }

        internal static string? MapFailureToCategory(BuildResult? result) {
            if (result == null || result.IsSuccess) return null;
            var text = (result.FailureReason ?? "") + " " +
                       (result.Recommendation ?? "") + " " +
                       string.Join(" ", result.Diagnostics);

            if (text.Contains("font", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("字体", StringComparison.Ordinal) ||
                text.Contains("fontspec", StringComparison.OrdinalIgnoreCase)) {
                return "fonts";
            }
            if (text.Contains("minted", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("pygmentize", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("shell-escape", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("tab_size", StringComparison.OrdinalIgnoreCase)) {
                return "category_code";
            }
            if (text.Contains("margin", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("geometry", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("边距", StringComparison.Ordinal) ||
                text.Contains("paper", StringComparison.OrdinalIgnoreCase)) {
                return "geometry_layout";
            }
            if (text.Contains("hyperref", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("bookmark", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("toc", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("目录", StringComparison.Ordinal)) {
                return "hyperref_toc";
            }
            if (text.Contains("head", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("foot", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("页眉", StringComparison.Ordinal) ||
                text.Contains("页脚", StringComparison.Ordinal)) {
                return "header_footer";
            }
            return null;
        }
    }
}

