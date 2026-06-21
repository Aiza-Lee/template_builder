using Utils.Exceptions;

namespace Utils {
	/// <summary>
	/// 管理嵌入式资源的工具类
	/// </summary>
	/// <param name="logger">日志器</param>
	internal class ManifestResourceManager(ILogger logger) {
		private readonly ILogger _logger = logger;

		/// <summary>
		/// 从嵌入式资源中获取指定名称的资源内容字符串
		/// </summary>
		/// <param name="resourceName">资源名称</param>
		/// <returns>返回字符串形式的内容</returns>
		/// <exception cref="MissingEmbeddedResourceException">资源不存在时抛出</exception>
		public virtual string GetResourceInString(string resourceName) {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream == null) {
				throw new MissingEmbeddedResourceException(resourceName);
			}
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		/// <summary>
		/// 从嵌入式资源中获取指定名称的资源流
		/// </summary>
		/// <param name="resourceName">资源名称</param>
		/// <returns>返回资源流</returns>
		/// <exception cref="MissingEmbeddedResourceException">资源不存在时抛出</exception>
		public virtual Stream GetResourceAsStream(string resourceName) {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream == null) {
				throw new MissingEmbeddedResourceException(resourceName);
			}
			return stream;
		}
	}
}