using System.Text;

namespace Utils {
	/// <summary>
	/// 转义 LaTeX 特殊字符。把用户输入的字符串安全地插入到 LaTeX 文档中。
	/// 处理 11 个 LaTeX 保留字符：<c>\ { } # $ % & _ ^ ~</c>。
	/// </summary>
	internal static class LatexEscaper {
		private static readonly IReadOnlyDictionary<char, string> Replacements = new Dictionary<char, string> {
			{ '\\', @"\textbackslash{}" },
			{ '{', @"\{" }, { '}', @"\}" },
			{ '#', @"\#" }, { '$', @"\$" },
			{ '%', @"\%" }, { '&', @"\&" },
			{ '_', @"\_" }, { '^', @"\^{}" },
			{ '~', @"\textasciitilde{}" }
		};

		/// <summary>
		/// 转义 <paramref name="text"/> 中的 LaTeX 特殊字符。null / 空字符串原样返回。
		/// </summary>
		public static string Escape(string? text) {
			if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
			var sb = new StringBuilder(text.Length);
			foreach (var ch in text) {
				if (Replacements.TryGetValue(ch, out var replacement)) {
					sb.Append(replacement);
				} else {
					sb.Append(ch);
				}
			}
			return sb.ToString();
		}
	}
}
