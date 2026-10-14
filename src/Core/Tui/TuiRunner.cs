using System.Diagnostics;
using Core.Localization;
using Core.Models;
using Core.Pipeline;
using Core.SourceTree;
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
                        RenderBreadcrumb(1, I18n.T("Breadcrumb.Step1", "基础路径与运行参数"));
                        session.SourceDir = PromptSourceDir(session.SourceDir);
                        session.OutputPdf = PromptOutputPdf(session.OutputPdf, session.SourceDir);

                        var (configFile, userProvided) = PromptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = configFile;
                        session.UserProvidedConfig = userProvided;

                        var (templateDir, verbose) = PromptAdvancedOptions(launch, session.TemplateDir, session.Verbose);
                        session.TemplateDir = templateDir;
                        session.Verbose = verbose;
                        sink.SetLevel(session.Verbose ? LogLevel.DEBUG : LogLevel.INFO);

                        // 首次启动未加载配置，或用户在向导中重选/切换了配置文件时，加载项目配置（带损坏/缺失恢复机制）
                        var shouldLoadConfig = !session.IsConfigurationLoaded
                            || !string.Equals(session.LoadedConfigPath, configFile.FullName, StringComparison.OrdinalIgnoreCase);

                        if (shouldLoadConfig) {
                            LoadOrRecoverConfiguration(session, launch, _bootstrapLogger);
                            if (launch.InitialPreset != null && session.Configuration != null) {
                                LayoutPresets.Apply(session.Configuration, launch.InitialPreset);
                            }
                        }

                        currentStep = 2;
                        break;
                    }

                    case 2: {
                        // 步骤 2: 配置调优中心 (7 大分类 + 全量 Schema 动态覆盖)
                        AnsiConsole.WriteLine();
                        RenderBreadcrumb(2, I18n.T("Breadcrumb.Step2", "配置调优中心"));
                        var goBack = PromptConfigMenu(session, launch, sink);

                        if (goBack) {
                            AnsiConsole.MarkupLine($"[yellow]{I18n.T("Prompt.NavStep1Notice", "正在返回步骤 1：重新设置基础路径与运行参数...")}[/]");
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
                        RenderBreadcrumb(3, I18n.T("Breadcrumb.Step3", "执行构建与实时监控"));
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
            TerminalWidthRegistry.EnsureAdequateWidthAndRender(AnsiConsole.Console, width => {
                TuiViewRenderer.RenderDynamicBanner(AnsiConsole.Console, width);
            });
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
                $"[bold]{I18n.T("Prompt.SourceDir", "源文件目录")}[/]：\n",
                defaultPath,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, I18n.T("Prompt.PathEmpty", "路径不能为空"));
                    return Directory.Exists(path) ? (true, null) : (false, string.Format(I18n.T("Prompt.DirNotFound", "目录不存在：{0}"), path));
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
                $"[bold]{I18n.T("Prompt.OutputPdf", "输出 PDF 路径")}[/]：\n",
                suggested,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, I18n.T("Prompt.OutputPathEmpty", "输出路径不能为空"));
                    if (Directory.Exists(path)) return (false, I18n.T("Prompt.OutputPathIsDir", "输出路径不能是目录"));
                    try {
                        return string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetFullPath(path)))
                            ? (false, I18n.T("Prompt.OutputCannotResolve", "无法解析输出目录"))
                            : (true, null);
                    } catch {
                        return (false, I18n.T("Prompt.OutputPathInvalid", "输出路径非法"));
                    }
                }
            );

            var chosen = (input ?? suggested).Trim();
            return new FileInfo(chosen);
        }

        internal void LoadOrRecoverConfiguration(
            ProjectSession session,
            TuiLaunchOptions launch,
            ILogger logger
        ) {
            TuiSessionRecoveryService.LoadOrRecoverConfiguration(session, launch, logger, PromptConfigFile);
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
                $"[bold]{I18n.T("Prompt.ConfigFile", "基础配置文件")}[/]：\n",
                defaultFile.FullName,
                raw => {
                    var path = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(path)) return (false, I18n.T("Prompt.PathEmpty", "路径不能为空"));
                    return File.Exists(path) ? (true, null) : (false, string.Format(I18n.T("Prompt.FileNotFound", "文件不存在：{0}"), path));
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
                new ConfirmationPrompt($"[bold]{I18n.T("Prompt.AdvancedOptions", "是否配置高级参数（外部模板目录 / 详细调试日志）？")}[/]") {
                    DefaultValue = false
                }
            );

            if (!modify) {
                return (initialTemplateDir, initialVerbose);
            }

            var tmplInput = TuiLineEditor.ReadLine(
                $"[bold]{I18n.T("Prompt.TemplateDir", "外部模板目录 (直接回车使用内嵌模板)：")}[/]\n",
                initialTemplateDir?.FullName,
                raw => {
                    if (string.IsNullOrWhiteSpace(raw)) return (true, null);
                    return Directory.Exists(raw.Trim())
                        ? (true, null)
                        : (false, string.Format(I18n.T("Prompt.DirNotFound", "目录不存在：{0}"), raw.Trim()));
                }
            );

            var tmplRaw = (tmplInput ?? initialTemplateDir?.FullName)?.Trim();
            var templateDir = string.IsNullOrEmpty(tmplRaw) ? null : new DirectoryInfo(tmplRaw);

            var verbose = AnsiConsole.Prompt(
                new ConfirmationPrompt($"[bold]{I18n.T("Prompt.DebugLog", "启用详细调试日志 (DEBUG)？")}[/]") {
                    DefaultValue = initialVerbose
                }
            );

            return (templateDir, verbose);
        }

        private static bool ValidateAndPreflight(ProjectConfiguration config, ProjectSession session, ILogger sink) {
            var validation = Core.Validation.ConfigValidator.Validate(config);
            if (!validation.IsValid) {
                var titleFmt = I18n.T("Validation.ConfigErrorsTitle", "✘ 当前配置存在 {0} 处错误，无法开始构建：");
                AnsiConsole.MarkupLine($"[bold red]{string.Format(titleFmt, validation.Errors.Count)}[/]");
                foreach (var err in validation.Errors) {
                    var expText = string.Format(I18n.T("Validation.Expected", "期望：{0}"), Markup.Escape(err.Expected));
                    AnsiConsole.MarkupLine($"  [red]• {Markup.Escape(err.PropertyPath)}[/]: {Markup.Escape(err.Message)} [grey]({expText})[/]");
                }
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Validation.PressAnyKeyConfig", "按任意键返回调整配置...")}[/]");
                Console.ReadKey(intercept: true);
                return false;
            }

            if (session.SourceDir != null) {
                var preflight = Core.Validation.SourcePreflight.Inspect(session.SourceDir, config, sink);
                if (!preflight.IsValid) {
                    AnsiConsole.MarkupLine($"[bold red]{I18n.T("Validation.PreflightFailed", "✘ 源文件预检失败，无法开始构建：")}[/]");
                    AnsiConsole.MarkupLine($"  [red]{Markup.Escape(preflight.Message)}[/]");
                    if (!string.IsNullOrEmpty(preflight.Recommendation)) {
                        AnsiConsole.MarkupLine($"  [yellow]{string.Format(I18n.T("Validation.Recommendation", "建议：{0}"), Markup.Escape(preflight.Recommendation))}[/]");
                    }
                    AnsiConsole.MarkupLine($"[grey]{I18n.T("Validation.PressAnyKeyParams", "按任意键返回调整参数...")}[/]");
                    Console.ReadKey(intercept: true);
                    return false;
                }
            }

            return true;
        }

        private bool PromptConfigMenu(ProjectSession session, TuiLaunchOptions launch, ILogger sink) {
            var config = session.Configuration ?? new ProjectConfiguration(null, ConfigStrictness.Lax);
            string? lastSelectedId = session.LastFocusedCategoryId;

            while (true) {
                var menuItems = new List<TuiMenuItem>();

                var activeOverrides = config.GetActiveOverrides();
                foreach (var cat in TuiSchemaCatalog.Categories) {
                    var overriddenCount = cat.Fields.Count(f => activeOverrides.ContainsKey(f.Key));
                    var modFmt = I18n.T("CategoryPreview.ModifiedFmt", "{0} 项已改");
                    var details = overriddenCount > 0 ? string.Format(modFmt, overriddenCount) : null;
                    menuItems.Add(new TuiMenuItem(cat.Id, cat.DisplayTitle, details, isOverridden: overriddenCount > 0, tag: cat));
                }

                // 快速搜索定位配置项
                menuItems.Add(new TuiMenuItem("SEARCH", I18n.T("Menu.Search", "搜索配置项"), action: TuiMenuAction.Search));

                // 源码收录与章节树构建前预览
                menuItems.Add(new TuiMenuItem("PREVIEW", I18n.T("Menu.Preview", "预览收录与章节树"), action: TuiMenuAction.Preview));

                // 排版预设方案
                menuItems.Add(new TuiMenuItem("PRESET", I18n.T("Menu.Preset", "应用排版预设"), action: TuiMenuAction.Preset));

                // 编译排版小样
                menuItems.Add(new TuiMenuItem("SAMPLE", I18n.T("Menu.Sample", "编译排版小样"), action: TuiMenuAction.Sample));

                if (activeOverrides.Count > 0) {
                    menuItems.Add(new TuiMenuItem("INSPECT", I18n.TFormat("Menu.Inspect", "查看覆盖项 ({0})", activeOverrides.Count), action: TuiMenuAction.Inspect));
                    menuItems.Add(new TuiMenuItem("CLEAR", I18n.T("Menu.ClearAll", "清空所有覆盖项"), action: TuiMenuAction.Clear));
                }

                // 保存当前配置到文件
                menuItems.Add(new TuiMenuItem("SAVE", I18n.T("Menu.Save", "保存配置到文件"), action: TuiMenuAction.Save));

                // 重新加载配置文件
                menuItems.Add(new TuiMenuItem("RELOAD", I18n.T("Menu.Reload", "重新加载配置"), action: TuiMenuAction.Reload));

                // 语言切换选项
                menuItems.Add(new TuiMenuItem("LANGUAGE", I18n.TFormat("Menu.Language", "语言设置 / Language ({0})", I18n.CurrentLanguageDisplayName)));

                // 显式返回上一步
                menuItems.Add(new TuiMenuItem("BACK_STEP1", I18n.T("Menu.BackStep1", "返回上一步"), action: TuiMenuAction.Back));

                string sourceLabel = session.ConfigFile == null
                    ? I18n.T("ConfigMenu.SourceBuiltin", "内置默认")
                    : session.UserProvidedConfig
                        ? string.Format(I18n.T("ConfigMenu.SourceUser", "用户项目 ({0})"), Markup.Escape(session.ConfigFile.Name))
                        : string.Format(I18n.T("ConfigMenu.SourceDefault", "默认项目 ({0})"), Markup.Escape(session.ConfigFile.Name));

                var promptTitle = $"[bold {TuiTheme.Secondary}]{I18n.T("ConfigMenu.Title", "配置调优")}[/]  [grey]{string.Format(I18n.T("ConfigMenu.Source", "来源: {0}"), sourceLabel)}  {string.Format(I18n.T("ConfigMenu.OverridesCount", "覆盖: {0}"), activeOverrides.Count)}[/]：";
                var chosen = TuiMenu.Prompt(
                    promptTitle,
                    menuItems,
                    initialSelectedId: lastSelectedId,
                    onEnterAction: TuiMenuItem.ConfirmSentinel
                );
                lastSelectedId = chosen.Id;
                session.LastFocusedCategoryId = chosen.Id;

                if (chosen.IsBuild) {
                    if (ValidateAndPreflight(config, session, sink)) {
                        break;
                    }
                    continue;
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
                    AnsiConsole.MarkupLine("[green]" + I18n.T("ConfigMenu.Reloaded", "✔ 配置文件已重新加载。") + "[/]");
                    continue;
                }

                if (chosen.Id == "LANGUAGE") {
                    PromptLanguageSelection();
                    continue;
                }

                if (chosen.IsClear) {
                    foreach (var key in activeOverrides.Keys.ToList()) {
                        config.ClearOverride(key);
                    }
                    AnsiConsole.MarkupLine("[yellow]" + I18n.T("ConfigMenu.AllCleared", "已清空全部运行时覆盖项。") + "[/]");
                    continue;
                }

                if (chosen.IsSearch) {
                    var shouldBuild = TuiConfigEditor.SearchAndEditFields(config);
                    if (shouldBuild && ValidateAndPreflight(config, session, sink)) {
                        break;
                    }
                    continue;
                }

                if (chosen.IsPreview) {
                    ShowContentPreview(session.SourceDir, config);
                    continue;
                }

                if (chosen.IsPreset) {
                    PromptApplyPreset(config);
                    continue;
                }

                if (chosen.IsSample) {
                    PromptRunSampleBuild(session, launch, sink);
                    continue;
                }

                if (chosen.Action == TuiMenuAction.Inspect) {
                    ShowOverridesTable(config.GetActiveOverrides(), config);
                    continue;
                }

                if (chosen.Tag is TuiCategoryGroup targetCategory) {
                    var shouldBuild = TuiConfigEditor.EditCategoryFields(targetCategory, config);
                    lastSelectedId = targetCategory.Id;
                    session.LastFocusedCategoryId = targetCategory.Id;
                    if (shouldBuild && ValidateAndPreflight(config, session, sink)) {
                        break;
                    }
                }
            }

            var finalOverrides = config.GetActiveOverrides();
            string finalSourceLabel = session.ConfigFile == null
                ? I18n.T("ConfigMenu.SourceBuiltinDesc", "内置默认配置")
                : session.UserProvidedConfig
                    ? string.Format(I18n.T("ConfigMenu.SourceUserDesc", "用户项目配置 ({0})"), Markup.Escape(session.ConfigFile.FullName))
                    : string.Format(I18n.T("ConfigMenu.SourceDefaultDesc", "默认项目配置 ({0})"), Markup.Escape(session.ConfigFile.FullName));

            AnsiConsole.MarkupLine($"[bold]{I18n.T("ConfigMenu.ConfigSource", "配置来源")}[/]：{finalSourceLabel}");
            AnsiConsole.MarkupLine(finalOverrides.Count == 0
                ? $"[grey]{I18n.T("ConfigMenu.NoOverridesNotice", "未覆盖任何配置项，将完全按配置文件/内置默认构建。")}[/]"
                : $"[bold {TuiTheme.Warning}]{string.Format(I18n.T("ConfigMenu.OverridesActiveNotice", "已设置 {0} 项运行时覆盖（仅本次生效，不修改文件）。"), finalOverrides.Count)}[/]");

            return false;
        }

        private static void PromptLanguageSelection() {
            var items = new List<TuiMenuItem> {
                new("zh-CN", "简体中文 (Chinese Simplified)", isOverridden: I18n.CurrentLanguage == AppLanguage.ZhCn),
                new("en-US", "English (US)", isOverridden: I18n.CurrentLanguage == AppLanguage.EnUs)
            };
            var chosen = TuiMenu.Prompt(
                I18n.T("Language.SelectPrompt", "请选择界面语言 / Select Interface Language："),
                items,
                initialSelectedId: I18n.CurrentLanguageCode
            );
            if (chosen.IsBack) return;
            I18n.SetLanguage(chosen.Id);
            AnsiConsole.MarkupLine($"[green]✔ {I18n.T("Language.Switched", "已成功切换界面语言")}[/]");
        }

        private static void ShowOverridesTable(IReadOnlyDictionary<string, string> overrides, ProjectConfiguration configuration) {
            TuiViewRenderer.RenderOverridesTable(AnsiConsole.Console, overrides, configuration);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{I18n.T("ConfigMenu.PressEnter", "按 Enter 返回主菜单...")}[/]");
            Console.ReadLine();
        }

        private static void ShowContentPreview(DirectoryInfo? sourceDir, ProjectConfiguration config) {
            if (sourceDir == null || !sourceDir.Exists) {
                AnsiConsole.MarkupLine("[bold yellow]" + I18n.T("ConfigMenu.NoSourceDir", "未找到源文件目录，请先在基础参数中指定存在的源码目录。") + "[/]");
                AnsiConsole.MarkupLine("[grey]" + I18n.T("ConfigMenu.PressEnter", "按 Enter 返回主菜单...") + "[/]");
                Console.ReadLine();
                return;
            }

            var filterMode = SourceTreeFilterMode.All;
            string? lastSelectedModeId = null;

            while (true) {
                var plan = SourceTreePlan.Build(sourceDir, config);
                TuiViewRenderer.RenderSourceTreePreview(AnsiConsole.Console, plan, filterMode);

                var modeItems = new List<TuiMenuItem>();
                modeItems.Add(TuiMenuItem.BackSentinel);
                modeItems.Add(new TuiMenuItem("MODE_ALL", I18n.T("SourceTree.ModeAll", "完整章节树"), isOverridden: filterMode == SourceTreeFilterMode.All));
                modeItems.Add(new TuiMenuItem("MODE_INCLUDED", string.Format(I18n.T("SourceTree.ModeIncluded", "已收录文件: {0} 项"), plan.IncludedFiles.Count), isOverridden: filterMode == SourceTreeFilterMode.IncludedOnly));
                modeItems.Add(new TuiMenuItem("MODE_EXCLUDED", string.Format(I18n.T("SourceTree.ModeExcluded", "已排除文件: {0} 项"), plan.ExcludedFiles.Count), isOverridden: filterMode == SourceTreeFilterMode.ExcludedOnly));

                var chosen = TuiMenu.Prompt($"[bold]{I18n.T("SourceTree.ViewPrompt", "预览视图")}[/]：", modeItems, initialSelectedId: lastSelectedModeId);
                if (chosen.IsBack) break;

                lastSelectedModeId = chosen.Id;
                if (chosen.Id == "MODE_ALL") filterMode = SourceTreeFilterMode.All;
                else if (chosen.Id == "MODE_INCLUDED") filterMode = SourceTreeFilterMode.IncludedOnly;
                else if (chosen.Id == "MODE_EXCLUDED") filterMode = SourceTreeFilterMode.ExcludedOnly;
            }
        }

        private static void PromptApplyPreset(ProjectConfiguration config) {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine($"[bold cyan]{I18n.T("Preset.LibraryTitle", "🎨 排版预设方案库")}[/]");
            AnsiConsole.MarkupLine($"[grey]{I18n.T("Preset.LibraryDesc", "快速应用面向不同阅读与打印场景的最佳配置组合。")}[/]");
            AnsiConsole.WriteLine();

            var menuItems = new List<TuiMenuItem>();
            menuItems.Add(TuiMenuItem.BackSentinel);
            foreach (var preset in LayoutPresets.All) {
                menuItems.Add(new TuiMenuItem(preset.Name, $"{preset.DisplayName} — {preset.DisplayTagline}", preset.DisplayDescription));
            }

            var chosen = TuiMenu.Prompt(
                $"[bold yellow]{I18n.T("Preset.SelectPrompt", "请选择要应用的排版预设：")}[/]",
                menuItems,
                defaultIndex: 1
            );
            if (chosen.IsBack) return;

            var chosenPreset = LayoutPresets.All.FirstOrDefault(p => string.Equals(p.Name, chosen.Id, StringComparison.OrdinalIgnoreCase));
            if (chosenPreset == null) return;

            LayoutPresets.Apply(config, chosenPreset);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]{string.Format(I18n.T("Preset.Applied", "✔ 已成功应用预设：{0}"), Markup.Escape(chosenPreset.DisplayName))}[/]");
            AnsiConsole.MarkupLine($"[grey]{string.Format(I18n.T("Preset.Desc", "预设说明：{0}"), Markup.Escape(chosenPreset.DisplayDescription))}[/]");
            AnsiConsole.WriteLine();

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn($"[bold]{I18n.T("Preset.ColField", "配置项")}[/]");
            table.AddColumn($"[bold]{I18n.T("Preset.ColValue", "预设值")}[/]");
            table.AddColumn($"[bold]{I18n.T("Preset.ColDesc", "中文说明")}[/]");

            foreach (var (k, v) in chosenPreset.Settings) {
                var label = TuiSchemaCatalog.FieldsByKey.TryGetValue(k, out var field) ? field.DisplayLabel : k;
                table.AddRow($"[yellow]{Markup.Escape(k)}[/]", $"[green]{Markup.Escape(v)}[/]", $"[grey]{Markup.Escape(label)}[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{I18n.T("Preset.PressAnyKey", "按任意键返回主配置菜单...")}[/]");
            Console.ReadKey(intercept: true);
        }

        private static void PromptRunSampleBuild(ProjectSession session, TuiLaunchOptions launch, ILogger sink) {
            var config = session.Configuration ?? new ProjectConfiguration(null, ConfigStrictness.Lax);
            if (session.SourceDir == null || !session.SourceDir.Exists) {
                AnsiConsole.MarkupLine($"[bold red]{I18n.T("Sample.NoSource", "✘ 源目录未指定或不存在，无法生成小样。")}[/]");
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Sample.PressAnyKey", "按任意键返回...")}[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            var validation = Core.Validation.ConfigValidator.Validate(config);
            if (!validation.IsValid) {
                var errFmt = I18n.T("Sample.Errors", "✘ 当前配置存在 {0} 处错误，无法编译小样：");
                AnsiConsole.MarkupLine($"[bold red]{string.Format(errFmt, validation.Errors.Count)}[/]");
                foreach (var err in validation.Errors) {
                    AnsiConsole.MarkupLine($"  [red]• {Markup.Escape(err.PropertyPath)}[/]: {Markup.Escape(err.Message)}");
                }
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Sample.PressAnyKey", "按任意键返回...")}[/]");
                Console.ReadKey(intercept: true);
                return;
            }

            int defaultSample = launch.InitialSampleLimit ?? 3;
            var promptText = string.Format(I18n.T("Sample.PromptLimit", "小样抽样文件数量 (默认 {0} 个，输入数字)："), defaultSample);
            var fileCountStr = TuiLineEditor.ReadLine(
                $"[bold]{promptText}[/]\n",
                defaultSample.ToString(),
                raw => {
                    if (string.IsNullOrWhiteSpace(raw)) return (true, null);
                    return int.TryParse(raw.Trim(), out var n) && n > 0 && n <= 20
                        ? (true, null)
                        : (false, I18n.T("Sample.LimitInvalid", "请输入 1 到 20 之间的正整数"));
                }
            );
            int sampleLimit = int.TryParse(fileCountStr?.Trim(), out var limit) && limit > 0 ? limit : defaultSample;

            var previewDir = Path.Combine(
                session.SourceDir.Parent?.FullName ?? session.SourceDir.FullName,
                "preview"
            );
            Directory.CreateDirectory(previewDir);
            var samplePdfPath = Path.Combine(previewDir, $"{session.SourceDir.Name}-sample.pdf");
            var samplePdfFile = new FileInfo(samplePdfPath);

            var tempConfigFile = session.ConfigFile ?? new FileInfo(Path.Combine(session.SourceDir.FullName, ".template_builder", "build", "mid-config.json"));

            var sampleOptions = new BuildSubcommandOptions(
                session.SourceDir,
                samplePdfFile,
                tempConfigFile,
                session.Verbose,
                session.TemplateDir,
                config.GetActiveOverrides(),
                SampleFileLimit: sampleLimit
            );

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold cyan]{string.Format(I18n.T("Sample.Compiling", "正在提取前 {0} 个源码文件编译排版小样..."), sampleLimit)}[/]");

            var pipeline = new BuildPipelineRunner(sink, new ManifestResourceManager());
            var result = pipeline.Run(sampleOptions, userProvidedConfig: session.UserProvidedConfig);

            AnsiConsole.WriteLine();
            if (result == ExitCodes.Success) {
                AnsiConsole.MarkupLine($"[bold green]{I18n.T("Sample.Success", "✔ 小样生成成功！产物已写入：")}[/] [yellow]{Markup.Escape(samplePdfFile.FullName)}[/]");
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Sample.Tip", "提示：小样已应用当前生效的所有排版预设与运行时配置覆盖。")}[/]");
                AnsiConsole.WriteLine();

                var actOpen = I18n.T("Sample.ActionOpen", "打开小样 PDF 进行视觉审查");
                var actBack = I18n.T("Sample.ActionBack", "返回配置导航中心继续微调");

                var sampleItems = new List<TuiMenuItem> {
                    new("OPEN", actOpen),
                    new("BACK", actBack, action: TuiMenuAction.Back)
                };

                var chosen = TuiMenu.Prompt(
                    $"[yellow]{I18n.T("Sample.Options", "小样操作选项：")}[/]",
                    sampleItems
                );

                if (chosen.Id == "OPEN") {
                    OpenPdfWithSystemViewer(samplePdfFile.FullName);
                }
            } else {
                AnsiConsole.MarkupLine($"[bold red]{string.Format(I18n.T("Sample.Failed", "✘ 小样编译未成功（退出码 {0}）。请检查编译诊断或调整排版参数。"), result)}[/]");
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Sample.PressAnyKeyMenu", "按任意键返回配置菜单...")}[/]");
                Console.ReadKey(intercept: true);
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
                ? I18n.T("ConfigMenu.SourceBuiltinDesc", "内置默认配置")
                : userProvided
                    ? string.Format(I18n.T("ConfigMenu.SourceUserDesc", "用户项目配置 ({0})"), configFile.FullName)
                    : string.Format(I18n.T("ConfigMenu.SourceDefaultDesc", "默认项目配置 ({0})"), configFile.FullName);

            sink.Info(string.Format(I18n.T("Build.PrepNotice", "[构建准备] 配置来源: {0}，生效覆盖: {1} 项。"), configSourceDesc, overrides.Count));

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
            var finalResult = result ?? BuildResult.CreateFailure(ExitCodes.XelatexFailure, BuildStatus.Failed, I18n.T("Build.UnknownError", "未知错误"));
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
                var sizeText = output.Exists ? $"{output.Length / 1024.0:F1} KB" : I18n.T("Build.Unknown", "未知");

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn($"[bold]{I18n.T("Build.Metrics", "指标")}[/]").Centered());
                table.AddColumn(new TableColumn($"[bold]{I18n.T("Build.Details", "详情")}[/]"));
                var statusText = result.Status == BuildStatus.SuccessWithWarnings
                    ? $"[yellow]{I18n.T("Build.StatusSuccessWarn", "SUCCESS (成功，附警告)")}[/]"
                    : $"[bold green]{I18n.T("Build.StatusSuccess", "SUCCESS (成功)")}[/]";
                table.AddRow($"[green]{I18n.T("Build.Result", "构建结果")}[/]", statusText);
                table.AddRow(I18n.T("Build.PdfPath", "PDF 产物路径"), Markup.Escape(result.OutputPdf?.FullName ?? output.FullName));
                table.AddRow(I18n.T("Build.FileSize", "产物文件大小"), sizeText);
                table.AddRow(I18n.T("Build.ConfigSource", "配置来源"), Markup.Escape(configSourceDesc));
                table.AddRow(I18n.T("Build.PassCount", "编译轮数"), $"{result.PassesCompleted}/{result.TotalPasses}");
                table.AddRow(I18n.T("Build.TotalDuration", "总编译耗时"), string.Format(I18n.T("Build.DurationSeconds", "{0:F1} 秒"), elapsedSeconds));
                table.AddRow(I18n.T("Build.ActiveOverridesCount", "生效配置覆盖"), string.Format(I18n.T("Build.OverridesItems", "{0} 项"), overridesCount));
                if (result.Warnings.Count > 0) {
                    table.AddRow($"[yellow]{I18n.T("Build.Warnings", "构建警告")}[/]", string.Join("\n", result.Warnings.Select(w => Markup.Escape(w))));
                }

                AnsiConsole.Write(new Panel(table)
                    .Header($"[bold green]{I18n.T("Build.SuccessHeader", " 构建完成仪表板 ")}[/]")
                    .Border(BoxBorder.Rounded));
                AnsiConsole.MarkupLine($"[bold green]{I18n.T("Build.SuccessSummary", "构建成功！")}[/] [grey]{I18n.T("Build.SuccessPdfReady", "PDF 产物已就绪。")}[/]");
            } else {
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn($"[bold]{I18n.T("Build.Metrics", "指标")}[/]").Centered());
                table.AddColumn(new TableColumn($"[bold]{I18n.T("Build.Details", "详情")}[/]"));
                var failStatusMsg = string.Format(I18n.T("Build.FailedStatus", "FAILED ({0}, 退出码 {1})"), result.Status, result.ExitCode);
                table.AddRow($"[red]{I18n.T("Build.Result", "构建结果")}[/]", $"[bold red]{failStatusMsg}[/]");
                table.AddRow(I18n.T("Build.ConfigSource", "配置来源"), Markup.Escape(configSourceDesc));
                if (!string.IsNullOrEmpty(result.FailureReason)) {
                    table.AddRow($"[bold red]{I18n.T("Build.FailedReason", "失败原因")}[/]", $"[bold red]{Markup.Escape(result.FailureReason)}[/]");
                }
                if (!string.IsNullOrEmpty(result.CulpritSourceFile)) {
                    var location = result.CulpritLineNumber.HasValue
                        ? $"{result.CulpritSourceFile}:{result.CulpritLineNumber.Value}"
                        : result.CulpritSourceFile;
                    table.AddRow($"[yellow]{I18n.T("Build.LocateSource", "定位源文件")}[/]", $"[bold yellow]{Markup.Escape(location)}[/]");
                }
                if (!string.IsNullOrEmpty(result.Recommendation)) {
                    table.AddRow($"[green]{I18n.T("Build.Suggestion", "排查建议")}[/]", $"[bold green]{Markup.Escape(result.Recommendation)}[/]");
                }
                if (!string.IsNullOrEmpty(result.DiagnosticPdfPath)) {
                    var diagNotice = string.Format(I18n.T("Build.DiagnosticPdfNotice", "{0} (已保留供排查，未覆盖正式产物)"), Markup.Escape(result.DiagnosticPdfPath));
                    table.AddRow(I18n.T("Build.DiagnosticPdf", "诊断产物"), $"[grey]{diagNotice}[/]");
                }
                if (!string.IsNullOrEmpty(result.ErrorMessage)) {
                    table.AddRow(I18n.T("Build.ErrorMessage", "错误信息"), Markup.Escape(result.ErrorMessage));
                }
                if (result.Diagnostics.Count > 0) {
                    table.AddRow(I18n.T("Build.DiagSummary", "诊断摘要"), string.Join("\n", result.Diagnostics.Select(d => Markup.Escape(d))));
                }
                if (!string.IsNullOrEmpty(result.LogFilePath)) {
                    table.AddRow(I18n.T("Build.FullLog", "完整日志"), Markup.Escape(result.LogFilePath));
                }
                table.AddRow(I18n.T("Build.PassInterrupted", "中断轮数"), string.Format(I18n.T("Build.PassInterruptedFmt", "第 {0} 轮"), result.PassesCompleted));
                table.AddRow(I18n.T("Build.TotalDuration", "已耗时间"), string.Format(I18n.T("Build.DurationSeconds", "{0:F1} 秒"), elapsedSeconds));

                AnsiConsole.Write(new Panel(table)
                    .Header($"[bold red]{I18n.T("Build.FailedHeader", " 构建失败报告 ")}[/]")
                    .Border(BoxBorder.Rounded));
                AnsiConsole.MarkupLine($"[bold red]{I18n.T("Build.FailedSummary", "构建未成功完成。")}[/] [grey]{I18n.T("Build.FailedHelp", "请参考上方诊断信息或编译日志进行排查。")}[/]");
            }
        }

        private static IRenderable RenderLog(TuiLogSink sink) {
            var entries = sink.Snapshot();
            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn(new TableColumn("Message"));

            if (entries.Count == 0) {
                table.AddRow(new Markup($"[grey]{I18n.T("Build.Initializing", "正在初始化构建流程...")}[/]"));
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
                .Header($"[bold cyan]{I18n.T("Build.LiveHeader", " 实时编译输出 ")}[/]")
                .Border(BoxBorder.Rounded);
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
                var menuItems = new List<TuiMenuItem> {
                    new("REBUILD", I18n.T("Result.Rebuild", "重新构建"), action: TuiMenuAction.Build)
                };

                if (isSuccess) {
                    if (session.OutputPdf?.Exists == true) {
                        menuItems.Add(new TuiMenuItem("OPEN_PDF", I18n.T("Result.OpenPdf", "打开 PDF")));
                    }
                    menuItems.Add(new TuiMenuItem("ADJUST", I18n.T("Build.ActionTweakConfig", "微调配置")));
                } else {
                    menuItems.Add(new TuiMenuItem("ADJUST", I18n.T("Build.ActionAdjustConfig", "调整配置")));
                    if (!string.IsNullOrEmpty(lastResult?.DiagnosticPdfPath) && File.Exists(lastResult.DiagnosticPdfPath)) {
                        menuItems.Add(new TuiMenuItem("OPEN_DIAG_PDF", I18n.T("Build.ActionViewDiagPdf", "查看诊断 PDF")));
                    }
                }

                menuItems.Add(new TuiMenuItem("MODIFY_PATHS", I18n.T("Build.ActionChangePaths", "修改路径")));
                menuItems.Add(new TuiMenuItem("VIEW_LOG", I18n.T("Result.ViewLog", "查看构建日志"), action: TuiMenuAction.Inspect));
                menuItems.Add(new TuiMenuItem("SAVE", I18n.T("Menu.Save", "保存配置到文件"), action: TuiMenuAction.Save));
                menuItems.Add(new TuiMenuItem("EXIT", I18n.T("Result.Exit", "退出"), action: TuiMenuAction.Back));

                var title = isSuccess
                    ? $"[bold {TuiTheme.Success}]{I18n.T("Build.ResultSuccessPrompt", "✔ 构建成功：")}[/]"
                    : $"[bold {TuiTheme.Danger}]{I18n.T("Build.ResultFailedPrompt", "✘ 构建未成功：")}[/]";

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
                            AnsiConsole.MarkupLine($"[yellow]{I18n.T("Build.FocusCategoryTip", "💡 已为您自动聚焦至关联排查分类：")}[/][bold cyan]{Markup.Escape(catObj.DisplayTitle)}[/]");
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
                $"[bold]{I18n.T("Save.PromptPath", "保存配置文件路径 (直接回车确认)：")}[/]\n",
                suggestedPath,
                raw => {
                    var p = raw?.Trim();
                    if (string.IsNullOrWhiteSpace(p)) return (false, I18n.T("Prompt.PathEmpty", "路径不能为空"));
                    try {
                        var dir = Path.GetDirectoryName(Path.GetFullPath(p));
                        return (true, null);
                    } catch {
                        return (false, I18n.T("Prompt.OutputPathInvalid", "路径非法"));
                    }
                }
            );

            if (string.IsNullOrWhiteSpace(input)) {
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Save.Cancelled", "已取消保存配置。")}[/]");
                return;
            }

            var savePath = input.Trim();
            try {
                session.Configuration.SaveToFile(savePath);
                session.ConfigFile = new FileInfo(savePath);
                session.UserProvidedConfig = true;
                AnsiConsole.MarkupLine($"[bold green]{I18n.T("Save.Success", "✔ 配置已成功保存至文件：")}[/][cyan]{Markup.Escape(session.ConfigFile.FullName)}[/]");
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Save.SuccessDesc", "所有修改项已写入配置文件，后续启动与连续构建将直接复用该配置。")}[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[bold red]{string.Format(I18n.T("Save.Failed", "✘ 保存配置文件失败：{0}"), Markup.Escape(ex.Message))}[/]");
            }
        }

        private static void OpenPdfWithSystemViewer(string filePath) {
            if (!File.Exists(filePath)) {
                AnsiConsole.MarkupLine($"[red]{string.Format(I18n.T("Open.NotFound", "✘ 产物文件不存在：{0}"), Markup.Escape(filePath))}[/]");
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
                AnsiConsole.MarkupLine($"[green]{I18n.T("Open.Success", "✔ 已调用系统默认阅读器打开：")}[/][cyan]{Markup.Escape(filePath)}[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[yellow]{string.Format(I18n.T("Open.Failed", "无法自动启动阅读器：{0}，请手动打开：{1}"), Markup.Escape(ex.Message), Markup.Escape(filePath))}[/]");
            }
        }

        private static void ShowFullLog(TuiLogSink sink) {
            var entries = sink.Snapshot();
            AnsiConsole.WriteLine();
            var rule = new Rule($"[bold cyan]{I18n.T("Log.Title", "完整构建与诊断日志")}[/]");
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
            AnsiConsole.MarkupLine($"[grey]{I18n.T("Log.PressEnter", "按 Enter 返回操作菜单...")}[/]");
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
