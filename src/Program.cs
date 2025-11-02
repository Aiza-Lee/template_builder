using System.CommandLine;
using Core.Commands;
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

			var buildCommand = new BuildCommandFactory(_logger).CreateCommand();
			rootCommand.Add(buildCommand);

			return rootCommand;
		}

		

	}
}