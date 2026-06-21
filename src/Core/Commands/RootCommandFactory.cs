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
			var root = new RootCommand("Compile algorithm/code template directories into PDF.");

			root.Add(CreateBuildSubcommand());
			root.Add(CreateValidateSubcommand());
			root.Add(CreateInitSubcommand());

			// 无子命令时给出 usage 提示并返回 InvalidArguments
			root.SetAction((ParseResult pr) => {
				_logger.Error("required command not specified. Usage: template_builder <build|validate|init> [...]");
				return ExitCodes.InvalidArguments;
			});

			return root;
		}

		/// <summary>
		/// build 子命令：编译源目录为 PDF。
		/// </summary>
		private Command CreateBuildSubcommand() {
			var cmd = new Command("build", "Build pdf file from source files.");

			/* --source-files-folder -s */
			var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
				Description = "Set the folder path of source files.",
				HelpName = "SOURCE_FILES_FOLDER",
				Required = true,
			};
			cmd.Options.Add(sourceFilesFolderOption);

			/* --output -o */
			var outputOption = new Option<FileInfo>("--output", "-o") {
				Description = "Set the output path of built pdf file.",
				HelpName = "OUTPUT_PATH",
				Required = true,
			};
			cmd.Options.Add(outputOption);

			/* --verbose -v */
			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
				DefaultValueFactory = (_) => false,
			};
			cmd.Options.Add(verboseOption);

			/* --config -c */
			var configOption = new Option<FileInfo>("--config", "-c") {
				Description = "Set the path of configuration file.",
				HelpName = "CONFIG",
				DefaultValueFactory = (_) => {
					// 配置文件的路径，默认值是操作系统用户配置目录下的 NightingaleStudio/TemplateBuilder/config.json
					var userConfigPath = new UserConfigPathHelper(_logger, "NightingaleStudio", "TemplateBuilder").GetUserConfigPath();
					var configFileInfo = new FileInfo(Path.Combine(userConfigPath, "config.json"));
					if (!configFileInfo.Exists) {
						// 确保目录存在
						if (!configFileInfo.Directory!.Exists) {
							configFileInfo.Directory.Create();
						}
						// 从嵌入式资源中复制默认配置文件到该路径
						using var fs = new ManifestResourceManager(_logger).GetResourceAsStream("DefaultConfig.jsonc");
						using var outFs = configFileInfo.Create();
						fs.CopyTo(outFs);
						_logger.Info($"Default configuration file created at \"{configFileInfo.FullName}\".");
					}
					return configFileInfo;
				}
			};
			cmd.Options.Add(configOption);

			/* --template-dir -t */
			var templateDirOption = new Option<DirectoryInfo>("--template-dir", "-t") {
				Description = "Override embedded LaTeX templates by reading Main.tex and/or CodeBlock.tex from this directory. Files not present fall back to embedded versions.",
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
						configOption.GetDefaultValue() as FileInfo
					);

					var options = new BuildSubcommandOptions(
						src, outPdf, cfg,
						pr.GetValue(verboseOption),
						pr.GetValue(templateDirOption)
					);
					return new BuildPipelineRunner(_logger, new ManifestResourceManager(_logger))
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
		/// validate 子命令（占位）：检查源码 / 配置 / 模板的完整性，不调 xelatex。
		/// </summary>
		private Command CreateValidateSubcommand() {
			var cmd = new Command("validate", "Validate source tree, config, and templates without invoking xelatex.");

			/* --source-files-folder -s */
			var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
				Description = "Set the folder path of source files.",
				HelpName = "SOURCE_FILES_FOLDER",
				Required = true,
			};
			cmd.Options.Add(sourceFilesFolderOption);

			/* --config -c */
			var configOption = new Option<FileInfo>("--config", "-c") {
				Description = "Set the path of configuration file.",
				HelpName = "CONFIG",
				Required = true,
			};
			cmd.Options.Add(configOption);

			/* --template-dir -t */
			var templateDirOption = new Option<DirectoryInfo>("--template-dir", "-t") {
				Description = "Override embedded LaTeX templates by reading Main.tex and/or CodeBlock.tex from this directory.",
				HelpName = "TEMPLATE_DIR",
				Required = false,
			};
			cmd.Options.Add(templateDirOption);

			/* --format */
			var formatOption = new Option<string>("--format") {
				Description = "Output format: text (default) or json.",
				HelpName = "FORMAT",
				DefaultValueFactory = (_) => "text",
			};
			cmd.Options.Add(formatOption);

			/* --check-xelatex */
			var checkXelatexOption = new Option<bool>("--check-xelatex") {
				Description = "Also verify xelatex is on PATH.",
				HelpName = "CHECK_XELATEX",
				DefaultValueFactory = (_) => false,
			};
			cmd.Options.Add(checkXelatexOption);

			cmd.SetAction((ParseResult pr) => {
				_logger.Error("validate subcommand is not yet implemented.");
				return ExitCodes.XelatexFailure;
			});

			return cmd;
		}

		/// <summary>
		/// init 子命令（占位）：写出带注释的默认配置骨架。
		/// </summary>
		private Command CreateInitSubcommand() {
			var cmd = new Command("init", "Emit a starter config file with default values and inline comments.");

			/* --output -o */
			var outputOption = new Option<FileInfo>("--output", "-o") {
				Description = "Output path for the generated config file.",
				HelpName = "OUTPUT_PATH",
				Required = true,
			};
			cmd.Options.Add(outputOption);

			/* --format */
			var formatOption = new Option<string>("--format") {
				Description = "Output format: jsonc (default) or json.",
				HelpName = "FORMAT",
				DefaultValueFactory = (_) => "jsonc",
			};
			cmd.Options.Add(formatOption);

			cmd.SetAction((ParseResult pr) => {
				_logger.Error("init subcommand is not yet implemented.");
				return ExitCodes.XelatexFailure;
			});

			return cmd;
		}
	}
}
