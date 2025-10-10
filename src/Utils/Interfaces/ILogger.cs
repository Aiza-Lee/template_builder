namespace Utils {
	public enum LogLevel {
		NONE,
		INFO,
		WARNING,
		ERROR,
		DEBUG
	}
	public interface ILogger {
		LogLevel Level { get; set; }
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Debug(string message);
	}
}