using System.CommandLine;
using Core.Commands;
using Utils;

namespace Program {
	public class Program {

		public static int Main(string[] args) {
			ILogger logger = new Logger();
			logger.Info("Application started...");

			var rootCommand = CreateRootCommand(logger);
			return rootCommand.Parse(args).Invoke();
		}

		internal static RootCommand CreateRootCommand(ILogger logger) {
			var rootCommand = new RootCommand("Config Parser Application");

			var buildCommand = new BuildCommandFactory(logger).CreateCommand();
			rootCommand.Add(buildCommand);

			return rootCommand;
		}

	}
}