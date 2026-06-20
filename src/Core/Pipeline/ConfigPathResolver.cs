using Utils;

namespace Core.Pipeline {
	/// <summary>
	/// 处理 `-c` 选项的路径回退：当用户给出的路径不存在时，回到由
	/// <c>configOption.DefaultValueFactory</c> 计算出的默认路径（用户配置目录下的
	/// <c>config.json</c>，首次运行从嵌入资源拷贝）。同时报告该路径最终是否
	/// 由用户显式提供，以决定 <see cref="ConfigStrictness"/>。
	/// </summary>
	internal sealed class ConfigPathResolver(ILogger logger) {
		private readonly ILogger _logger = logger;

		/// <summary>
		/// 解析 `-c` 选项。
		/// </summary>
		/// <param name="requested">用户提供（或默认值工厂产出）的配置文件路径</param>
		/// <param name="userProvidedAtCli">命令行 token 中是否含 <c>-c</c> / <c>--config</c></param>
		/// <param name="defaultFallback">当 <paramref name="requested"/> 不存在时回退的路径</param>
		/// <returns>已存在的配置文件路径，以及最终是否仍视为「用户提供」（影响严格模式）</returns>
		public (FileInfo ConfigFile, bool UserProvided) Resolve(
			FileInfo? requested,
			bool userProvidedAtCli,
			FileInfo? defaultFallback
		) {
			if (requested != null && requested.Exists) {
				_logger.Info($"Using configuration file at \"{requested.FullName}\".");
				return (requested, userProvidedAtCli);
			}

			var requestedPath = requested?.FullName ?? "<null>";
			_logger.Warning($"Configuration file \"{requestedPath}\" not found, use default configuration instead.");

			if (defaultFallback == null) {
				// 这分支理论上不可达：configOption.DefaultValueFactory 一定会算出一个 FileInfo
				throw new InvalidOperationException("Default configuration fallback is not available.");
			}
			return (defaultFallback, false); // 退回默认配置不再严格
		}
	}
}
