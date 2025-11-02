using System.CommandLine;
using Utils;

namespace Core.Commands {
	/// <summary>
	/// “构建命令”工厂类
	/// </summary>
	/// <param name="logger">日志器</param>
	internal class BuildCommandFactory(ILogger logger) {
		private readonly ILogger _logger = logger;

		public Command CreateCommand() {

			var cmd = new Command("build", "Build pdf file from source files.");

			// 是否启用详细输出
			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
				DefaultValueFactory = (_) => false,
			};
			cmd.Options.Add(verboseOption);

			// 配置文件的路径，默认值是操作系统用户配置目录下的 NightingaleStudio/TemplateBuilder/config.json
			var userConfigPath = new UserConfigPathHelper(_logger, "NightingaleStudio", "TemplateBuilder").GetUserConfigPath();

			var configOption = new Option<FileInfo>("--config", "-c") {
				Description = "Set the path of configuration file.",
				HelpName = "CONFIG",
				DefaultValueFactory = (_) => new(Path.Combine(userConfigPath, "config.json"))
			};

			cmd.SetAction((pr) => {
				var verbose = pr.GetValue(verboseOption);
				LoggerConfig.GlobalLevel = verbose ? LogLevel.DEBUG : LogLevel.INFO;

				var config = pr.GetValue(configOption);
				if (!config!.Exists) {
					_logger.Warning($"Configuration file \"{config.FullName}\" not found, use default configuration instead.");
					config = configOption.GetDefaultValue() as FileInfo;
				}

				new PdfBuilder(_logger).Build();
			});

			return cmd;
		}
	}
}