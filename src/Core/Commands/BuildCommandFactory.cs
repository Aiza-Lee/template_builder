using System.CommandLine;
using System.Runtime.InteropServices.Marshalling;
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

			finalCmd.SetAction((pr) => {
				/* --source-files-folder -s */
				var sourceFilesFolder = pr.GetValue(sourceFilesFolderOption);
				if (sourceFilesFolder == null) {
					_logger.Error("Invalid source files folder.");
					return;
				}
				if (!sourceFilesFolder.Exists) {
					_logger.Error($"Source files folder \"{sourceFilesFolder.FullName}\" not found.");
					return;
				}
				CommandInfoHelper.SourceFilesDirectoryInfo = sourceFilesFolder;

				/* --output -o */
				var output = pr.GetValue(outputOption);
				if (output == null || output.Directory == null) {
					_logger.Error("Invalid output file path.");
					return;
				}
				// 如果目标所在的文件夹不存在，则提前创建
				if (!output.Directory.Exists) {
					_logger.Warning($"Output directory \"{output.Directory.FullName}\" not found, created by the program.");
					output.Directory.Create();
				}
				// 修正文件后缀为pdf
				var pdfFileName = Path.GetFileNameWithoutExtension(output.Name) + ".pdf";
				CommandInfoHelper.OutputFileInfo = new FileInfo(
					Path.Combine(output.Directory.FullName, pdfFileName)
				);

				/* --verbose -v */
				var verbose = pr.GetValue(verboseOption);
				LoggerConfig.GlobalLevel = verbose ? LogLevel.DEBUG : LogLevel.INFO;
				CommandInfoHelper.IsVerboseEnabled = verbose;

				/* --config -c */
				var config = pr.GetValue(configOption);
				if (!config!.Exists) {
					_logger.Warning($"Configuration file \"{config.FullName}\" not found, use default configuration instead.");
					config = configOption.GetDefaultValue() as FileInfo; // 默认值是用户配置目录下的 config.json，此默认值是在创建选项时设置的
				}
				CommandInfoHelper.ConfigurationFileInfo = config!;

				new PdfBuilder(_logger).Build();
			});

			return finalCmd;
		}
	}
}