namespace Utils {
	internal enum LogLevel {
		NONE,
		ERROR,
		WARNING,
		INFO,
		DEBUG,
	}

	internal static class LoggerConfig {
		/// <summary>
		/// 最详细的日志等级
		/// </summary>
		public static LogLevel GlobalLevel { get; set; } = LogLevel.INFO;
	}

	internal interface ILogger {
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Debug(string message);
	}
}