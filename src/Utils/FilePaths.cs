namespace Utils {
	/// <summary>
	/// 全局文件路径管理静态类
	/// </summary>
	internal static class FilePaths {
		private static string _userConfigPath = string.Empty;

		/// <summary>
		/// 注册用户配置文件路径
		/// </summary>
		/// <param name="path">配置文件绝对路径</param>
		public static void RegisterUserConfigPath(string path) {
			_userConfigPath = path;
		}

		/// <summary>
		/// 用户配置文件路径，用于解析用户自定义路径中的相对路径
		/// </summary>
		public static string GetConfigInPath(IConfigParser configParser, string configName) {
			var configFilePath = configParser[configName].GetAsString();
			return Path.Combine(AppContext.BaseDirectory, configFilePath);
		}

		public static FileInfo GetUserConfigFileInfo() {
			return new (Path.Combine(AppContext.BaseDirectory, "configuration", "config.json"));
		}
	}
}