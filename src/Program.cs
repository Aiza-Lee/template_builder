using System.CommandLine;
using Core;
using Utils;

namespace Program {
	public class Program {

		private static readonly Logger _logger;
		private static readonly PdfBuilder _pdfBuilder;

		static Program() {
			_logger = new Logger();
			_pdfBuilder = new PdfBuilder(_logger);
		}

		public static int Main(string[] args) {
			_logger.Info("Application started.");

			var rootCommand = CreateRootCommand();
			return rootCommand.Parse(args).Invoke();
		}

		private static RootCommand CreateRootCommand() {
			var rootCommand = new RootCommand("Config Parser Application");

			var buildCommand = new Command("build", "Build pdf file from source files");
			rootCommand.Add(buildCommand);

			// var logLevelOption = new Option<string>("--log-level", "-l") {
			// 	Description = "Set the logging level (info, warning, error)",
			// 	HelpName = "LOG_LEVEL",
			// };
			// var verboseOption = new Option<bool>("--verbose", "-v") {
			// 	Description = "Enable verbose output",
			// 	HelpName = "VERBOSE",
			// };

			// rootCommand.Options.Add(logLevelOption);
			// rootCommand.Options.Add(verboseOption);

			rootCommand.SetAction((parseResult) => {
				// var logLevel = parseResult.GetValue(logLevelOption);
				// SetGlobalLogLevel(logLevel);

				// var verbose = parseResult.GetValue(verboseOption);
				// if (verbose) SetGlobalLogLevel(LogLevel.INFO);

				var buildCommandResult = parseResult.GetResult(buildCommand);
				if (buildCommandResult != null) {
					_pdfBuilder?.Build();
				}
			});

			return rootCommand;
		}

		// private static void SetGlobalLogLevel(string? logLevel) {
		// 	if (string.IsNullOrEmpty(logLevel)) {
		// 		SetGlobalLogLevel(LogLevel.INFO);
		// 		return;
		// 	}

		// 	if (!Enum.TryParse<LogLevel>(logLevel, true, out var levelEnum)) {
		// 		_logger.Warning($"Invalid log level: \"{logLevel}\". Defaulting to INFO.");
		// 		levelEnum = LogLevel.INFO;
		// 	}
		// 	SetGlobalLogLevel(levelEnum);
		// }

		// private static void SetGlobalLogLevel(LogLevel level) {
		// 	_logger.Level = level;
		// }

	}
}