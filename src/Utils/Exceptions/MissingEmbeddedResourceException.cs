namespace Utils.Exceptions {
	/// <summary>
	/// 请求的嵌入式资源未找到（理论上属编译期错误，发行包中不应触发）。
	/// 调用方应捕获并映射为 <see cref="Core.ExitCodes.MissingEmbeddedResource"/>。
	/// </summary>
	internal class MissingEmbeddedResourceException : Exception {
		public string ResourceName { get; }

		public MissingEmbeddedResourceException(string resourceName)
			: base($"Resource '{resourceName}' not found in embedded resources.") {
			ResourceName = resourceName;
		}
	}
}
