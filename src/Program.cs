using System;
using System.CommandLine;
using Core.Commands;
using Core.Localization;
using Utils;

namespace Program {
    public class Program {

        public static int Main(string[] args) {
            ILogger logger = new Logger();

            DetectAndSetEarlyLanguage(args);

            var rootCommand = CreateRootCommand(logger);
            return rootCommand.Parse(args).Invoke();
        }

        internal static void DetectAndSetEarlyLanguage(string[] args) {
            if (args == null || args.Length == 0) return;
            for (int i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase)) {
                    I18n.SetLanguage(arg.Substring(7));
                    return;
                }
                if (arg.StartsWith("-l=", StringComparison.OrdinalIgnoreCase)) {
                    I18n.SetLanguage(arg.Substring(3));
                    return;
                }
                if ((string.Equals(arg, "--lang", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(arg, "-l", StringComparison.OrdinalIgnoreCase)) &&
                    i + 1 < args.Length && !args[i + 1].StartsWith('-')) {
                    I18n.SetLanguage(args[i + 1]);
                    return;
                }
            }
        }

        internal static RootCommand CreateRootCommand(ILogger logger) {
            return new RootCommandFactory(logger).CreateRootCommand();
        }

    }
}