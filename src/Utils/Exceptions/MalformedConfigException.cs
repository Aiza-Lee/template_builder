namespace Utils.Exceptions {
	/// <summary>
	/// 用户配置文件 JSON 内容损坏或解析时发生意外异常时抛出。
	/// 调用方应捕获并映射为 <see cref="Core.ExitCodes.MalformedConfig"/>。
	/// </summary>
	internal class MalformedConfigException : Exception {
		public string? SourcePath { get; }

		public MalformedConfigException(string message, string? sourcePath = null, Exception? inner = null)
			: base(message, inner) {
			SourcePath = sourcePath;
		}
	}
}
