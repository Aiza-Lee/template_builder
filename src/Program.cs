using System.CommandLine;
using Core;
using Utils;

namespace Program {
	public class Program {

		private static readonly Logger _logger;

		static Program() {
			_logger = new Logger();
		}

		public static int Main(string[] args) {
			_logger.Info("Application started...");

			var rootCommand = CreateRootCommand();
			return rootCommand.Parse(args).Invoke();
		}

		private static RootCommand CreateRootCommand() {
			var rootCommand = new RootCommand("Config Parser Application");

			var buildCommand = new Command("build", "Build pdf file from source files");

			var verboseOption = new Option<bool>("--verbose", "-v") {
				Description = "Enable verbose output",
				HelpName = "VERBOSE",
				DefaultValueFactory = (_) => false,
			};
			buildCommand.Options.Add(verboseOption);

			buildCommand.SetAction((pr) => {
				// _logger.Info("What the fuck, holy shit.");
				var verbose = pr.GetValue(verboseOption);
				_logger.Info($"Verbose mode is {(verbose ? "enabled" : "disabled")}");
				LoggerConfig.GlobalLevel = verbose ? LogLevel.DEBUG : LogLevel.INFO;

				new PdfBuilder(_logger).Build();
			});

			rootCommand.Add(buildCommand);

			return rootCommand;
		}

	}
}