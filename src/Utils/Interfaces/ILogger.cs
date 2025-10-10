namespace Utils {
	internal enum LogLevel {
		DEBUG,
		INFO,
		WARNING,
		ERROR,
		NONE,
	}
	internal interface ILogger {
		LogLevel Level { get; set; }
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Debug(string message);
	}
}