namespace Utils {
	public class Logger(string tag) : ILogger {
		private readonly string _tag = tag;
		public LogLevel Level { get; set; } = LogLevel.INFO;

		public void Info(string message) {
			if (Level < LogLevel.INFO) return;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"{_tag} Info: {message}");
			Console.ResetColor();
		}

		public void Warning(string message) {
			if (Level < LogLevel.WARNING) return;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"{_tag} Warning: {message}");
			Console.ResetColor();
		}

		public void Error(string message) {
			if (Level < LogLevel.ERROR) return;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"{_tag} Error: {message}");
			Console.ResetColor();
		}

		public void Debug(string message) {
#if DEBUG
			if (Level < LogLevel.DEBUG) return;
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"{_tag} Debug: {message}");
			Console.ResetColor();
#endif
		}
	}
}