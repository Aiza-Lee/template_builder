using Microsoft.Extensions.FileSystemGlobbing;
using Utils;

namespace Utils {
	/// <summary>
	/// 源码树的一次性遍历结果项。
	/// </summary>
	/// <param name="Info">底层 <see cref="FileSystemInfo"/>，目录或文件</param>
	/// <param name="Depth">消费者插入 section 时应使用的层级（0 = 根目录的直接子项）</param>
	/// <param name="IsDirectory">true 表示目录，false 表示文件</param>
	public readonly record struct SourceEntry(
		FileSystemInfo Info,
		int Depth,
		bool IsDirectory
	);

	/// <summary>
	/// 递归遍历源码目录的工具，吐出按 (目录优先 / 字母序) 排序的 <see cref="SourceEntry"/>。
	/// 隐藏项（以 <c>.</c> 开头）和被 ignore glob 命中的项被跳过。
	/// </summary>
	internal static class SourceTreeWalker {
		/// <summary>
		/// 深度优先遍历 <paramref name="root"/> 下所有非隐藏、非 ignore 的目录与文件。
		/// </summary>
		/// <param name="root">遍历起点</param>
		/// <param name="ignorePatterns">glob 排除模式（Microsoft.Extensions.FileSystemGlobbing 语法）</param>
		/// <param name="logger">可选，跳过隐藏/ignore 时用于记录 Warning</param>
		public static IEnumerable<SourceEntry> Walk(
			DirectoryInfo root,
			IReadOnlyList<string> ignorePatterns,
			ILogger? logger = null
		) {
			var matcher = BuildMatcher(ignorePatterns);
			return WalkInternal(root, 0, matcher, logger);
		}

		/// <summary>
		/// 判断给定名称是否被 ignore glob 命中。提取为 public static 便于测试。
		/// </summary>
		public static bool IsIgnored(string name, Matcher matcher) {
			// HasMatches = false 表示被 exclude 命中
			return !matcher.Match(name).HasMatches;
		}

		private static Matcher BuildMatcher(IReadOnlyList<string> ignorePatterns) {
			var matcher = new Matcher();
			matcher.AddInclude("*");
			foreach (var pattern in ignorePatterns) {
				if (!string.IsNullOrWhiteSpace(pattern)) {
					matcher.AddExclude(pattern);
				}
			}
			return matcher;
		}

		private static IEnumerable<SourceEntry> WalkInternal(
			DirectoryInfo dir,
			int depth,
			Matcher matcher,
			ILogger? logger
		) {
			// 子目录优先（按字母序），递归深入
			foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)) {
				if (subDir.Name.StartsWith('.')) {
					logger?.Warning($"Skipping hidden directory: {subDir.FullName}");
					continue;
				}
				if (IsIgnored(subDir.Name, matcher)) {
					logger?.Warning($"Skipping directory '{subDir.FullName}' due to ignore pattern match.");
					continue;
				}
				yield return new SourceEntry(subDir, depth, IsDirectory: true);
				foreach (var entry in WalkInternal(subDir, depth + 1, matcher, logger)) {
					yield return entry;
				}
			}

			// 当前目录下的文件（按字母序）
			foreach (var file in dir.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) {
				if (file.Name.StartsWith('.')) {
					logger?.Warning($"Skipping hidden file: {file.FullName}");
					continue;
				}
				if (IsIgnored(file.Name, matcher)) {
					logger?.Warning($"Skipping file '{file.FullName}' due to ignore pattern match.");
					continue;
				}
				yield return new SourceEntry(file, depth, IsDirectory: false);
			}
		}
	}
}
