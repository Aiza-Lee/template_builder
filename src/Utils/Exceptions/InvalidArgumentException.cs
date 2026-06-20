namespace Utils.Exceptions {
	/// <summary>
	/// 命令行参数缺失或语义错误时抛出。
	/// 调用方应捕获并映射为 <see cref="Core.ExitCodes.InvalidArguments"/>。
	/// </summary>
	internal class InvalidArgumentException : Exception {
		public InvalidArgumentException(string message) : base(message) { }
	}
}
