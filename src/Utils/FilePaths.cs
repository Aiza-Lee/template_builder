namespace Utils {
	/// <summary>
	/// 应用程序文件路径管理
	/// </summary>
	internal static class FilePaths {
		/// <summary>
		/// 程序所在目录
		/// </summary>
		public static string BaseDirectory => AppContext.BaseDirectory;

		/// <summary>
		/// 用户配置文件路径，用于解析用户自定义路径中的相对路径
		/// </summary>
		public static string GetConfigInPath(IConfigParser configParser, string configName) {
			var configFilePath = configParser.QueryConfig(configName)[0];
			return Path.Combine(BaseDirectory, configFilePath);
		}

		public static FileInfo GetUserConfigFileInfo() {
			return new (Path.Combine(BaseDirectory, "configuration", "config.json"));
		}
	}
}