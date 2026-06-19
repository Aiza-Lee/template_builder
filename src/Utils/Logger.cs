namespace Utils {
	internal class Logger : ILogger {
		private LogLevel _level = LogLevel.INFO;

		public void Info(string message) {
			if (LogLevel.INFO > _level) return;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"{message}");
			Console.ResetColor();
		}

		public void Warning(string message) {
			if (LogLevel.WARNING > _level) return;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"[Warning] {message}");
			Console.ResetColor();
		}

		public void Error(string message) {
			if (LogLevel.ERROR > _level) return;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[Error] {message}");
			Console.ResetColor();
		}

		public void Debug(string message) {
			if (LogLevel.DEBUG > _level) return;
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"[Debug] {message}");
			Console.ResetColor();
		}

		public void SetLevel(LogLevel level) => _level = level;
	}
}