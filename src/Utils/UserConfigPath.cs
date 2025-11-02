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
		/// <exception cref="PlatformNotSupportedException">没有mac设备，暂时不支持mac系统</exception>
		public string GetUserConfigPath() {
			string basePath;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
				basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
			} else {
				throw new PlatformNotSupportedException("unsupported platform");
			}

			return Path.Combine(basePath, _organizationName, _appName);
		}
	}
}