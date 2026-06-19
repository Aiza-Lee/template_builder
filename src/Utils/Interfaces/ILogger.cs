namespace Utils {
	internal enum LogLevel {
		NONE,
		ERROR,
		WARNING,
		INFO,
		DEBUG,
	}

	internal interface ILogger {
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Debug(string message);
		void SetLevel(LogLevel level);
	}
}