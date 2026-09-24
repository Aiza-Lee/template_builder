using System.CommandLine;
using Core.Models;
using Core.Pipeline;
using Core.Tui;
using Utils;
using Utils.Exceptions;

namespace Core.Commands {
    /// <summary>
    ///
    /// 根命令工厂：构造顶层 <see cref="RootCommand"/> 及其子命令。
    ///
    /// <para>
    /// 用法: <br/>
    /// <code>
    /// template-builder build    [options]   # 编译源目录为 PDF（默认行为改为必须显式指定）
    /// template-builder validate [options]   # 校验源目录 / 配置 / 模板，不调 xelatex
    /// template-builder init     [options]   # 生成带注释的默认配置骨架
    /// </code>
    ///
    /// </para>
    ///
    /// </summary>
    ///
    internal class RootCommandFactory(
        ILogger logger,
        TextWriter? stdoutWriter = null,
        TextWriter? errorWriter = null
    ) {
        private readonly ILogger _logger = logger;
        private readonly TextWriter? _stdoutWriter = stdoutWriter;
        private readonly TextWriter? _errorWriter = errorWriter;

        public RootCommand CreateRootCommand() {
            var root = new RootCommand("将算法 / 代码模板目录编译成 PDF 文档。");

            root.Add(CreateBuildSubcommand());
            root.Add(CreateValidateSubcommand());
            root.Add(CreateInitSubcommand());

            // 无子命令时给出 usage 提示并返回 InvalidArguments
            root.SetAction((ParseResult pr) => {
                _logger.Error("未指定子命令。用法：template_builder <build|validate|init> [...]");
                return ExitCodes.InvalidArguments;
            });

            return root;
        }

        /// <summary>
        /// build 子命令：编译源目录为 PDF。
        /// </summary>
        private Command CreateBuildSubcommand() {
            var cmd = new Command("build", "从源文件目录构建 PDF。");

            /* --source-files-folder -s */
            // 非 Required：--tui 模式下由交互界面询问；非 --tui 时在 action 中显式报缺参（见下）
            var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
                Description = "源文件目录的路径。使用 --tui 时可省略，改为交互式输入。",
                HelpName = "SOURCE_FILES_FOLDER",
                Required = false,
            };
            cmd.Options.Add(sourceFilesFolderOption);

            /* --output -o */
            var outputOption = new Option<FileInfo>("--output", "-o") {
                Description = "构建产物 PDF 的输出路径。使用 --tui 时可省略，改为交互式输入。",
                HelpName = "OUTPUT_PATH",
                Required = false,
            };
            cmd.Options.Add(outputOption);

            /* --tui */
            var tuiOption = new Option<bool>("--tui") {
                Description = "启动全屏交互模式：提示输入源目录 / 输出路径与常用配置项，并实时滚动构建日志。",
                HelpName = "TUI",
                DefaultValueFactory = (_) => false,
            };
            cmd.Options.Add(tuiOption);

            /* --verbose -v */
            var verboseOption = new Option<bool>("--verbose", "-v") {
                Description = "启用详细输出（DEBUG 级别日志）。",
                HelpName = "VERBOSE",
                DefaultValueFactory = (_) => false,
            };
            cmd.Options.Add(verboseOption);

            /* --config -c */
            var configOption = new Option<FileInfo>("--config", "-c") {
                Description = "配置文件的路径。",
                HelpName = "CONFIG",
                DefaultValueFactory = (_) => GetDefaultConfigFileInfo(),
            };
            cmd.Options.Add(configOption);

            /* --template-dir -t */
            var templateDirOption = new Option<DirectoryInfo>("--template-dir", "-t") {
                Description = "从该目录读取 Main.tex 和/或 CodeBlock.tex 覆盖内嵌模板；目录中未提供的文件回退到内嵌版本。",
                HelpName = "TEMPLATE_DIR",
                Required = false,
            };
            cmd.Options.Add(templateDirOption);

            /* --preset */
            var presetOption = new Option<string>("--preset") {
                Description = "应用排版预设方案：competition (比赛打印)、reading (屏幕阅读)、compact (紧凑速查)。",
                HelpName = "PRESET",
                Required = false,
            };
            cmd.Options.Add(presetOption);

            /* --sample */
            var sampleOption = new Option<int?>("--sample") {
                Description = "生成排版小样（仅编译前 N 个源码文件，默认 3 个），用于快速确认版心与字体效果。",
                HelpName = "COUNT",
                Arity = ArgumentArity.ZeroOrOne,
            };
            cmd.Options.Add(sampleOption);

            /* --tui-scaffold */
            var tuiScaffoldOption = new Option<DirectoryInfo>("--tui-scaffold") {
                Description = "生成 TUI 各视图的排版快照与视觉审查汇报，用于无头环境与纯文本感知，不进入交互会话。",
                HelpName = "OUTPUT_DIR",
                Required = false,
            };
            cmd.Options.Add(tuiScaffoldOption);

            cmd.SetAction((ParseResult pr) => {
                _logger.SetLevel(pr.GetValue(verboseOption) ? LogLevel.DEBUG : LogLevel.INFO);

                var scaffoldDir = pr.GetValue(tuiScaffoldOption);
                if (scaffoldDir != null) {
                    var targetDir = scaffoldDir.FullName;
                    var generated = TuiScaffolding.GenerateAllSnapshots(targetDir);
                    _logger.Info($"TUI 脚手架快照生成完成：共写入 {generated.Count} 个文件至 \"{targetDir}\"。");
                    _logger.Info($"可直接查看 \"{Path.Combine(targetDir, "SUMMARY.md")}\" 审查排版与视觉感知效果。");
                    return ExitCodes.Success;
                }

                bool userProvidedAtCli = pr.Tokens.Any(t => t.Value == "--config" || t.Value == "-c");

                if (pr.GetValue(tuiOption)) {
                    // 必须在进入 TUI 前拦截非交互环境：否则 Spectre 读按键时会抛未捕获异常
                    if (!TuiEnvironment.SupportsInteractiveUi()) {
                        _logger.Error("--tui 需要交互式终端（TTY），当前标准输入或输出已被重定向。");
                        _logger.Error("请在真实终端中运行，或改用非交互模式：build -s <源目录> -o <输出.pdf>。");
                        return ExitCodes.InvalidArguments;
                    }

                    try {
                        return new TuiRunner(_logger).Run(new TuiLaunchOptions(
                            pr.GetValue(sourceFilesFolderOption),
                            pr.GetValue(outputOption),
                            pr.GetValue(configOption),
                            userProvidedAtCli,
                            pr.GetValue(templateDirOption),
                            pr.GetValue(verboseOption),
                            EnsureDefaultConfigFileExists
                        ));
                    } catch (MissingEmbeddedResourceException ex) {
                        // 默认配置骨架的嵌入资源缺失：与命令行路径保持一致的退出码
                        _logger.Error(ex.Message);
                        return ExitCodes.MissingEmbeddedResource;
                    }
                }

                try {
                    var presetName = pr.GetValue(presetOption);
                    Dictionary<string, string>? overrides = null;
                    if (!string.IsNullOrWhiteSpace(presetName)) {
                        var preset = LayoutPresets.FindById(presetName);
                        if (preset == null) {
                            var available = string.Join(", ", LayoutPresets.All.Select(p => p.Id));
                            _logger.Error($"未知的排版预设 \"{presetName}\"。可选预设方案：{available}。");
                            return ExitCodes.InvalidArguments;
                        }
                        overrides = new Dictionary<string, string>(preset.Settings, StringComparer.OrdinalIgnoreCase);
                    }

                    int? sampleLimit = null;
                    if (pr.Tokens.Any(t => t.Value == "--sample" || t.Value.StartsWith("--sample="))) {
                        sampleLimit = pr.GetValue(sampleOption) ?? 3;
                        if (sampleLimit <= 0) {
                            _logger.Error("小样文件数量必须为大于 0 的正整数。");
                            return ExitCodes.InvalidArguments;
                        }
                    }

                    var resolver = new OutputPathResolver(_logger);
                    var src = resolver.ResolveSourceDir(pr.GetValue(sourceFilesFolderOption));
                    var outPdf = resolver.ResolveOutputPdf(pr.GetValue(outputOption));

                    var (cfg, userProvided) = new ConfigPathResolver(_logger).Resolve(
                        pr.GetValue(configOption),
                        userProvidedAtCli,
                        EnsureDefaultConfigFileExists
                    );

                    var options = new BuildSubcommandOptions(
                        src, outPdf, cfg,
                        pr.GetValue(verboseOption),
                        pr.GetValue(templateDirOption),
                        overrides,
                        sampleLimit
                    );
                    return new BuildPipelineRunner(_logger, new ManifestResourceManager())
                        .Run(options, userProvided);
                } catch (InvalidArgumentException ex) {
                    _logger.Error(ex.Message);
                    return ExitCodes.InvalidArguments;
                } catch (MissingEmbeddedResourceException ex) {
                    _logger.Error(ex.Message);
                    return ExitCodes.MissingEmbeddedResource;
                }
            });

            return cmd;
        }

        /// <summary>
        /// validate 子命令：检查源码 / 配置 / 模板的完整性，不调 xelatex。
        /// </summary>
        private Command CreateValidateSubcommand() {
            var cmd = new Command("validate", "校验源目录 / 配置 / 模板的完整性，不调用 xelatex。");

            /* --source-files-folder -s */
            var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
                Description = "源文件目录的路径。",
                HelpName = "SOURCE_FILES_FOLDER",
                Required = true,
            };
            cmd.Options.Add(sourceFilesFolderOption);

            /* --config -c */
            var configOption = new Option<FileInfo>("--config", "-c") {
                Description = "配置文件的路径。",
                HelpName = "CONFIG",
                DefaultValueFactory = (_) => GetDefaultConfigFileInfo(),
            };
            cmd.Options.Add(configOption);

            /* --template-dir -t */
            var templateDirOption = new Option<DirectoryInfo>("--template-dir", "-t") {
                Description = "从该目录读取 Main.tex 和/或 CodeBlock.tex 覆盖内嵌模板。",
                HelpName = "TEMPLATE_DIR",
                Required = false,
            };
            cmd.Options.Add(templateDirOption);

            /* --format */
            var formatOption = new Option<string>("--format") {
                Description = "输出格式：text（默认）或 json。",
                HelpName = "FORMAT",
                DefaultValueFactory = (_) => "text",
            };
            cmd.Options.Add(formatOption);

            /* --check-xelatex */
            var checkXelatexOption = new Option<bool>("--check-xelatex") {
                Description = "同时校验 xelatex 与 pygmentize 是否在 PATH 上。",
                HelpName = "CHECK_XELATEX",
                DefaultValueFactory = (_) => false,
            };
            cmd.Options.Add(checkXelatexOption);

            cmd.SetAction((ParseResult pr) => {
                var rawFormat = pr.GetValue(formatOption) ?? "text";
                if (!string.Equals(rawFormat, "text", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(rawFormat, "json", StringComparison.OrdinalIgnoreCase)) {
                    _logger.Error($"--format 参数值 \"{rawFormat}\" 无效。合法值为 \"text\" 或 \"json\"。");
                    return ExitCodes.InvalidArguments;
                }
                var format = rawFormat.ToLowerInvariant();
                var isJson = format == "json";
                var effectiveLogger = isJson ? new DiagnosticStderrLogger(_logger, _errorWriter) : _logger;

                var src = pr.GetValue(sourceFilesFolderOption)!;

                bool userProvidedAtCli = pr.Tokens.Any(t => t.Value == "--config" || t.Value == "-c");
                FileInfo cfg;
                if (userProvidedAtCli && pr.GetValue(configOption) is { Exists: false } missingCfg) {
                    cfg = missingCfg;
                } else {
                    try {
                        var (resolvedCfg, _) = new ConfigPathResolver(effectiveLogger).Resolve(
                            pr.GetValue(configOption),
                            userProvidedAtCli,
                            () => EnsureDefaultConfigFileExists(effectiveLogger)
                        );
                        cfg = resolvedCfg;
                    } catch (InvalidArgumentException ex) {
                        if (isJson) {
                            var checks = new List<ValidationCheck>();
                            if (src.Exists) {
                                checks.Add(new ValidationCheck("source", "exists", true, src.FullName));
                            }
                            checks.Add(new ValidationCheck("config", "exists", false, ex.Message));
                            var report = new ValidationReport(false, 1, 0, checks);
                            (_stdoutWriter ?? Console.Out).WriteLine(report.ToJson());
                            return ExitCodes.InvalidArguments;
                        }
                        _logger.Error(ex.Message);
                        return ExitCodes.InvalidArguments;
                    }
                }

                var options = new ValidateSubcommandOptions(
                    src,
                    cfg,
                    pr.GetValue(templateDirOption),
                    format,
                    pr.GetValue(checkXelatexOption)
                );
                return new ValidationRunner(effectiveLogger, new ManifestResourceManager(), _stdoutWriter).Run(options);
            });

            return cmd;
        }


        /// <summary>
        /// init 子命令：写出带注释的默认配置骨架。
        /// </summary>
        private Command CreateInitSubcommand() {
            var cmd = new Command("init", "生成带默认值与 inline 注释的配置文件骨架。");

            /* --output -o */
            var outputOption = new Option<FileInfo>("--output", "-o") {
                Description = "生成配置文件的输出路径。",
                HelpName = "OUTPUT_PATH",
                Required = true,
            };
            cmd.Options.Add(outputOption);

            /* --format */
            var formatOption = new Option<string>("--format") {
                Description = "输出格式：jsonc（默认）或 json。",
                HelpName = "FORMAT",
                DefaultValueFactory = (_) => "jsonc",
            };
            cmd.Options.Add(formatOption);

            /* --force / -f */
            var forceOption = new Option<bool>("--force", "-f") {
                Description = "覆盖已存在的配置文件，并自动保留 .bak 备份。",
                DefaultValueFactory = (_) => false,
            };
            cmd.Options.Add(forceOption);

            cmd.SetAction((ParseResult pr) => {
                var rawFormat = pr.GetValue(formatOption) ?? "jsonc";
                if (!string.Equals(rawFormat, "jsonc", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(rawFormat, "json", StringComparison.OrdinalIgnoreCase)) {
                    _logger.Error($"--format 参数值 \"{rawFormat}\" 无效。合法值为 \"jsonc\" 或 \"json\"。");
                    return ExitCodes.InvalidArguments;
                }
                var format = rawFormat.ToLowerInvariant();

                var outputPath = pr.GetValue(outputOption)!;
                // 确保父目录存在
                if (outputPath.Directory is { } parent && !parent.Exists) {
                    parent.Create();
                }
                var options = new InitSubcommandOptions(outputPath, format, pr.GetValue(forceOption));
                return new ConfigInitializer(_logger, new ManifestResourceManager()).Run(options);
            });

            return cmd;
        }

        /// <summary>
        /// 获取默认用户配置文件路径对象（不执行磁盘 I/O 也不创建文件）。
        /// </summary>
        internal static FileInfo GetDefaultConfigFileInfo() {
            var userConfigPath = new UserConfigPathHelper("NightingaleStudio", "TemplateBuilder").GetUserConfigPath();
            return new FileInfo(Path.Combine(userConfigPath, "config.json"));
        }

        /// <summary>
        /// 确保默认用户配置文件存在；仅在真正需要使用默认配置且文件不存在时才从嵌入资源生成。
        /// </summary>
        internal FileInfo EnsureDefaultConfigFileExists() => EnsureDefaultConfigFileExists(_logger);

        /// <summary>
        /// 确保默认用户配置文件存在（支持自定义日志器）。
        /// </summary>
        internal FileInfo EnsureDefaultConfigFileExists(ILogger customLogger) {
            var configFileInfo = GetDefaultConfigFileInfo();
            if (!configFileInfo.Exists) {
                if (configFileInfo.Directory is { Exists: false } dir) {
                    dir.Create();
                }
                using var fs = new ManifestResourceManager().GetResourceAsStream("DefaultConfig.jsonc");
                using var outFs = configFileInfo.Create();
                fs.CopyTo(outFs);
                customLogger.Info($"已在 \"{configFileInfo.FullName}\" 创建默认配置文件。");
                customLogger.Info("提示：可运行 init 命令生成带注释的配置骨架以便自定义。");
                configFileInfo.Refresh();
            }
            return configFileInfo;
        }
    }
}
