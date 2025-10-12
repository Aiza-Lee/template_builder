namespace Utils {
	internal class Logger() : ILogger {
		public void Info(string message) {
			if (LogLevel.INFO > LoggerConfig.GlobalLevel) return;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"{message}");
			Console.ResetColor();
		}

		public void Warning(string message) {
			if (LogLevel.WARNING > LoggerConfig.GlobalLevel) return;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"[Warning] {message}");
			Console.ResetColor();
		}

		public void Error(string message) {
			if (LogLevel.ERROR > LoggerConfig.GlobalLevel) return;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[Error] {message}");
			Console.ResetColor();
		}

		public void Debug(string message) {
#if DEBUG
			if (LogLevel.DEBUG > LoggerConfig.GlobalLevel) return;
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"[Debug] {message}");
			Console.ResetColor();
#endif
		}
	}
}