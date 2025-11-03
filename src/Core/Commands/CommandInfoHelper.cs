/// <summary>
/// 记录command相关信息的辅助类
/// </summary>
internal static class CommandInfoHelper {

	private static FileInfo? _configurationFileInfo;
	/// <summary>
	/// 配置文件路径信息，已验证存在
	/// </summary>
	public static FileInfo ConfigurationFileInfo {
		get {
			if (_configurationFileInfo == null) {
				throw new InvalidOperationException("ConfigurationFileInfo has not been set.");
			}
			return _configurationFileInfo;
		}
		set => _configurationFileInfo = value;
	}

	private static DirectoryInfo? _sourceFilesDirectoryInfo;
	/// <summary>
	/// 源文件夹路径信息，已验证存在
	/// </summary>
	public static DirectoryInfo SourceFilesDirectoryInfo {
		get {
			if (_sourceFilesDirectoryInfo == null) {
				throw new InvalidOperationException("SourceFilesDirectoryInfo has not been set.");
			}
			return _sourceFilesDirectoryInfo;
		}
		set => _sourceFilesDirectoryInfo = value;
	}

	private static FileInfo? _outputFileInfo;
	/// <summary>
	/// 输出文件路径信息，已验证其所在的文件夹存在
	/// </summary>
	public static FileInfo OutputFileInfo {
		get {
			if (_outputFileInfo == null) {
				throw new InvalidOperationException("OutputFileInfo has not been set.");
			}
			return _outputFileInfo;
		}
		set => _outputFileInfo = value;
	}

	/// <summary>
	/// 是否启用详细日志输出
	/// </summary>
	public static bool IsVerboseEnabled { get; set; } = false;
}