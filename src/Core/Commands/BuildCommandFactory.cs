using System.CommandLine;
using Utils;

namespace Core.Commands {
	/// <summary>
	///
	/// “构建命令”工厂类
	///
	/// <para>
	/// build 命令使用方法: <br/>
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
	internal class BuildCommandFactory(ILogger logger) {
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
				int ExecuteBuild() {
					/* --source-files-folder -s */
					var sourceFilesFolder = pr.GetValue(sourceFilesFolderOption);
					if (sourceFilesFolder == null) {
						_logger.Error("Invalid source files folder.");
						return 2;
					}
					if (!sourceFilesFolder.Exists) {
						_logger.Error($"Source files folder \"{sourceFilesFolder.FullName}\" not found.");
						return 2;
					}

				/* --output -o */
				var output = pr.GetValue(outputOption);
				if (output == null || output.Directory == null) {
					_logger.Error("Invalid output file path.");
					return 2;
				}
				// 如果目标所在的文件夹不存在，则提前创建
				if (!output.Directory.Exists) {
					_logger.Warning($"Output directory \"{output.Directory.FullName}\" not found, created by the program.");
					output.Directory.Create();
				}
				// 修正文件后缀为pdf
				var pdfFileName = Path.GetFileNameWithoutExtension(output.Name) + ".pdf";
				var outputPdf = new FileInfo(Path.Combine(output.Directory.FullName, pdfFileName));

				/* --verbose -v */
				var verbose = pr.GetValue(verboseOption);
				_logger.SetLevel(verbose ? LogLevel.DEBUG : LogLevel.INFO);

				/* --config -c */
				// 检测 -c 是否由用户在命令行显式提供；只有显式提供的配置才走严格模式。
				bool userProvidedConfig = pr.Tokens.Any(t => t.Value == "--config" || t.Value == "-c");
				var config = pr.GetValue(configOption);
				if (!config!.Exists) {
					_logger.Warning($"Configuration file \"{config.FullName}\" not found, use default configuration instead.");
					config = configOption.GetDefaultValue() as FileInfo; // 默认值是用户配置目录下的 config.json，此默认值是在创建选项时设置的
					userProvidedConfig = false; // 退回默认配置不再严格
				} else {
					_logger.Info($"Using configuration file at \"{config.FullName}\".");
				}

				/* --template-dir -t */
				var templateDir = pr.GetValue(templateDirOption);

				// 组合根：构造服务图，读取并解析用户配置一次，然后交给 PdfBuilder。
				var resMgr = new ManifestResourceManager(_logger);
				var strictness = userProvidedConfig ? ConfigStrictness.Strict : ConfigStrictness.Lax;
				var texParser = new ConfigParser("TEX", _logger, strictness);
				var programParser = new ConfigParser("PROGRAM", _logger, strictness);
				var configJson = File.ReadAllText(config!.FullName);
				texParser.ParseConfigFile(configJson);
				programParser.ParseConfigFile(configJson);

				var options = new BuildOptions(sourceFilesFolder, outputPdf, config!, verbose, templateDir);
				return new PdfBuilder(_logger, options, texParser, programParser, resMgr).Build();
				}

				try {
					return ExecuteBuild();
				} catch (UnknownConfigKeyException ex) {
					_logger.Error(ex.Message);
					return 2;
				}
			});

			return finalCmd;
		}
	}
}