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
	/// build 子命令使用方法: <br/>
	/// <code>
	/// template-builder build [source-file-folder] [enable-verbose-log] [configuration] [output-path]
	///
	/// source-files-folder-options:
	///   --source-files-folder &lt;SOURCE_FILE_FOLDER&gt; | -s &lt;SOURCE_FILE_FOLDER&gt; # 源文件所在文件夹
	/// enable-verbose-log-options:
	///   --verbose | -v # 启用详细日志输出
	/// output-path-options:
	///   --output &lt;OUTPUT_PATH&gt; | -o &lt;OUTPUT_PATH&gt; # 输出文件路径
	/// configuration-options:
	///   --config &lt;CONFIG_FILE_PATH&gt; | -c &lt;CONFIG_FILE_PATH&gt; # 配置文件路径
	/// </code>
	///
	/// </para>
	///
	/// </summary>
	///
	/// <param name="logger">日志器</param>
	internal class RootCommandFactory(ILogger logger) {
		private readonly ILogger _logger = logger;

		public Command CreateCommand() {

			var finalCmd = new Command("build", "Build pdf file from source files.");

			/* --source-files-folder -s */
			var sourceFilesFolderOption = new Option<DirectoryInfo>("--source-files-folder", "-s") {
				Description = "Set the folder path of source files.",
				HelpName = "SOURCE_FILES_FOLDER",
				Required = true,
			};
			finalCmd.Options.Add(sourceFilesFolderOption);

			/* --output -o */
			var outputOption = new Option<FileInfo>("--output", "-o") {
				Description = "Set the output path of built pdf file.",
				HelpName = "OUTPUT_PATH",
				Required = true,
			};
			finalCmd.Options.Add(outputOption);

			/* --verbose -v */
			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
				DefaultValueFactory = (_) => false,
			};
			finalCmd.Options.Add(verboseOption);

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
						using var fs = new ManifestResourceManager(_logger).GetResourceAsStream("DefaultConfig.json");
						using var outFs = configFileInfo.Create();
						fs.CopyTo(outFs);
						_logger.Info($"Default configuration file created at \"{configFileInfo.FullName}\".");
					}
					return configFileInfo;
				}
			};
			finalCmd.Options.Add(configOption);

			/* --template-dir -t */
			var templateDirOption = new Option<DirectoryInfo>("--template-dir", "-t") {
				Description = "Override embedded LaTeX templates by reading Main.tex and/or CodeBlock.tex from this directory. Files not present fall back to embedded versions.",
				HelpName = "TEMPLATE_DIR",
				Required = false,
			};
			finalCmd.Options.Add(templateDirOption);

			finalCmd.SetAction((ParseResult pr) => {
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

			return finalCmd;
		}
	}
}