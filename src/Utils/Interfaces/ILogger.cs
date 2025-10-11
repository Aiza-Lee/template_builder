namespace Utils {
	internal enum LogLevel {
		NONE,
		ERROR,
		WARNING,
		INFO,
		DEBUG,
	}
	internal interface ILogger {
		/// <summary>
		/// 日志级别
		/// </summary>
		LogLevel Level { get; set; }
		void Info(string message);
		void Warning(string message);
		void Error(string message);
		void Debug(string message);
	}
}