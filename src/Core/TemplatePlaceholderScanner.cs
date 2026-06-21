using System.Text.RegularExpressions;

namespace Core {
	/// <summary>
	/// 在模板替换后扫描未替换的占位符。
	/// <para>
	/// 匹配 <c>&lt;&lt;UPPER_SNAKE&gt;&gt;</c> 与 <c>##UPPER_SNAKE##</c> 两种形式。
	/// 注意：包含 <c>&lt;&lt;UPPER_SNAKE&gt;&gt;</c> 字面量的代码片段（例如 C++ 流输出 <c>&lt;&lt;FLAG&gt;&gt;</c>）会被误报。
	/// 当下约定占位符全部使用大写加下划线，实际代码中此类字面量罕见。
	/// </para>
	/// </summary>
	internal static class TemplatePlaceholderScanner {
		private static readonly Regex Pattern = new(@"<<[A-Z_]+>>|##[A-Z_]+##", RegexOptions.Compiled);
		private static readonly Regex MainPlaceholderPattern = new(@"##[A-Z_]+##", RegexOptions.Compiled);

		public static IEnumerable<string> FindUnresolved(string content) {
			foreach (Match m in Pattern.Matches(content)) {
				yield return m.Value;
			}
		}

		/// <summary>
		/// 提取 <c>##KEY##</c> 形式的占位符（去包裹的 <c>##</c>），用于校验 Main.tex 中的占位符
		/// 是否都能在 TEX 配置段找到映射。
		/// </summary>
		public static IEnumerable<string> FindMainPlaceholders(string content) {
			foreach (Match m in MainPlaceholderPattern.Matches(content)) {
				// m.Value = "##FOO##" → yield "FOO"
				yield return m.Value.Substring(2, m.Value.Length - 4);
			}
		}
	}
}
