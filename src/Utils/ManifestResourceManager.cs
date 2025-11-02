namespace Utils {
	/// <summary>
	/// 管理嵌入式资源的工具类
	/// </summary>
	/// <param name="logger">日志器</param>
	internal sealed class ManifestResourceManager(ILogger logger) {
		private readonly ILogger _logger = logger;

		/// <summary>
		/// 从嵌入式资源中获取指定名称的资源内容字符串
		/// </summary>
		/// <param name="resourceName">资源名称</param>
		/// <returns>字符串形式的内容</returns>
		public string GetResourceInString(string resourceName) {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream != null) {
				using var reader = new StreamReader(stream);
				return reader.ReadToEnd();
			} else {
				_logger?.Error($"Resource '{resourceName}' not found in embedded resources.");
				return string.Empty;
			}
		}
	}
}