using System.CommandLine;
using Utils;

namespace Program {
	public class Program {

		private static readonly List<ILogger> _loggers = [];
		private static ILogger? _logger;

		public static int Main(string[] args) {
			_logger = new Logger("[Program]");
			_loggers.Add(_logger);
			_logger.Info("Application started.");
			var rootCommand = CreateRootCommand();
			return rootCommand.Parse(args).Invoke();
		}

		private static RootCommand CreateRootCommand() {
			var rootCommand = new RootCommand("Config Parser Application");

			var buildCommand = new Command("build", "Build pdf file from source files");
			rootCommand.Add(buildCommand);

			var logLevelOption = new Option<string>("--log-level", "-l") {
				Description = "Set the logging level (info, warning, error)",
				HelpName = "LOG_LEVEL",
			};
			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
			};

			rootCommand.Options.Add(logLevelOption);
			rootCommand.Options.Add(verboseOption);

			rootCommand.SetAction((parseResult) => {
				var logLevel = parseResult.GetValue(logLevelOption);
				SetLogLevel(logLevel);
			});

			return rootCommand;
		}

		private static void SetLogLevel(string? logLevel) {
			var levelEnum = LogLevel.INFO;
			try {
				if (string.IsNullOrEmpty(logLevel)) return;
				levelEnum = Enum.Parse<LogLevel>(logLevel.ToUpper());
			} catch (Exception) {
				_logger?.Error($"Invalid log level: {logLevel}. Defaulting to INFO.");
			}
			foreach (var logger in _loggers) {
				logger.Level = levelEnum;
			}
		}

	}
}