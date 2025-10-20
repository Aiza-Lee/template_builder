using System.CommandLine;
using Utils;

namespace Core.Commands {
	internal class BuildCommandFactory(ILogger logger) {
		private readonly ILogger _logger = logger;

		public Command CreateCommand() {

			var cmd = new Command("build", "Build pdf file from source files.");

			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
				DefaultValueFactory = (_) => false,
			};
			cmd.Options.Add(verboseOption);

			var userConfigPath = new UserConfigPath(_logger, "NightingaleStudio", "TemplateBuilder").GetUserConfigPath();

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