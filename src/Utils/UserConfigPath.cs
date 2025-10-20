using System.Runtime.InteropServices;

namespace Utils {
	internal class UserConfigPath(ILogger logger, string organizationName, string appName) {
		private readonly ILogger _logger = logger;
		private readonly string _organizationName = organizationName;
		private readonly string _appName = appName;

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