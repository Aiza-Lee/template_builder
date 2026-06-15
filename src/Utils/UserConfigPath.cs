using System.Runtime.InteropServices;

namespace Utils {
	/// <summary>
	/// 获取用户配置文件路径的工具类
	/// </summary>
	/// <param name="logger">日志器</param>
	/// <param name="organizationName">组织/公司名</param>
	/// <param name="appName">应用名</param>
	internal class UserConfigPathHelper(ILogger logger, string organizationName, string appName) {
		private readonly ILogger _logger = logger;
		private readonly string _organizationName = organizationName;
		private readonly string _appName = appName;

		/// <summary>
		/// 获取用户配置文件的绝对路径
		/// </summary>
		/// <returns></returns>
		/// <exception cref="PlatformNotSupportedException">未识别的操作系统平台</exception>
		public string GetUserConfigPath() {
			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string basePath = ResolveBasePath(RuntimeInformation.IsOSPlatform, userProfile);
			return Path.Combine(basePath, _organizationName, _appName);
		}

		/// <summary>
		/// 根据平台解析用户配置根目录。抽成纯函数便于测试。
		/// </summary>
		/// <param name="isOsPlatform">平台判断回调（便于测试时注入）</param>
		/// <param name="userProfile">当前用户的 home 目录（Windows 上对应 USERPROFILE，*nix 上对应 HOME）</param>
		internal static string ResolveBasePath(Func<OSPlatform, bool> isOsPlatform, string userProfile) {
			if (isOsPlatform(OSPlatform.Windows)) {
				return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			}
			if (isOsPlatform(OSPlatform.Linux)) {
				return Path.Combine(userProfile, ".config");
			}
			if (isOsPlatform(OSPlatform.OSX)) {
				return Path.Combine(userProfile, "Library", "Application Support");
			}
			throw new PlatformNotSupportedException("unsupported platform: " + RuntimeInformation.OSDescription);
		}
	}
}
