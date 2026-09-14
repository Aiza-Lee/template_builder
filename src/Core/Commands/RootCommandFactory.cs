using System.CommandLine;
using Core.Pipeline;
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
    /// <param name="logger">日志器</param>
    internal class RootCommandFactory(ILogger logger) {
        private readonly ILogger _logger = logger;

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
            var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
                Description = "源文件目录的路径。",
                HelpName = "SOURCE_FILES_FOLDER",
                Required = true,
            };
            cmd.Options.Add(sourceFilesFolderOption);

            /* --output -o */
            var outputOption = new Option<FileInfo>("--output", "-o") {
                Description = "构建产物 PDF 的输出路径。",
                HelpName = "OUTPUT_PATH",
                Required = true,
            };
            cmd.Options.Add(outputOption);

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

            cmd.SetAction((ParseResult pr) => {
                _logger.SetLevel(pr.GetValue(verboseOption) ? LogLevel.DEBUG : LogLevel.INFO);
                try {
                    var resolver = new OutputPathResolver(_logger);
                    var src = resolver.ResolveSourceDir(pr.GetValue(sourceFilesFolderOption));
                    var outPdf = resolver.ResolveOutputPdf(pr.GetValue(outputOption));

                    bool userProvidedAtCli = pr.Tokens.Any(t => t.Value == "--config" || t.Value == "-c");
                    var (cfg, userProvided) = new ConfigPathResolver(_logger).Resolve(
                        pr.GetValue(configOption),
                        userProvidedAtCli,
                        EnsureDefaultConfigFileExists
                    );

                    var options = new BuildSubcommandOptions(
                        src, outPdf, cfg,
                        pr.GetValue(verboseOption),
                        pr.GetValue(templateDirOption)
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
                Required = true,
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
                try {
                    var src = pr.GetValue(sourceFilesFolderOption)!;
                    var cfg = pr.GetValue(configOption)!;
                    if (!src.Exists) {
                        _logger.Error($"源目录不存在：{src.FullName}");
                        return ExitCodes.InvalidArguments;
                    }
                    if (!cfg.Exists) {
                        _logger.Error($"配置文件不存在：{cfg.FullName}");
                        return ExitCodes.InvalidArguments;
                    }
                    var options = new ValidateSubcommandOptions(
                        src,
                        cfg,
                        pr.GetValue(templateDirOption),
                        pr.GetValue(formatOption)!,
                        pr.GetValue(checkXelatexOption)
                    );
                    return new ValidationRunner(_logger, new ManifestResourceManager()).Run(options);
                } catch (InvalidArgumentException ex) {
                    _logger.Error(ex.Message);
                    return ExitCodes.InvalidArguments;
                }
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

            cmd.SetAction((ParseResult pr) => {
                var outputPath = pr.GetValue(outputOption)!;
                // 确保父目录存在
                if (outputPath.Directory is { } parent && !parent.Exists) {
                    parent.Create();
                }
                var options = new InitSubcommandOptions(outputPath, pr.GetValue(formatOption)!);
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
        internal FileInfo EnsureDefaultConfigFileExists() {
            var configFileInfo = GetDefaultConfigFileInfo();
            if (!configFileInfo.Exists) {
                if (configFileInfo.Directory is { Exists: false } dir) {
                    dir.Create();
                }
                using var fs = new ManifestResourceManager().GetResourceAsStream("DefaultConfig.jsonc");
                using var outFs = configFileInfo.Create();
                fs.CopyTo(outFs);
                _logger.Info($"已在 \"{configFileInfo.FullName}\" 创建默认配置文件。");
                configFileInfo.Refresh();
            }
            return configFileInfo;
        }
    }
}
