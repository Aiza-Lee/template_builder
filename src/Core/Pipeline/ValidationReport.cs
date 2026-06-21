using System.Text;
using System.Text.Json;

namespace Core.Pipeline {
	/// <summary>
	/// validate 阶段单条检查的结果。
	/// </summary>
	/// <param name="Category">分类标签（如 "source" / "config" / "resources" / "placeholders" / "environment"）</param>
	/// <param name="Name">具体检查项的名称（如 "exists" / "parses" / "xelatex"）</param>
	/// <param name="Ok">true = 通过；false = 失败</param>
	/// <param name="Message">附加信息（成功时为可选备注，失败时为错误描述）</param>
	public readonly record struct ValidationCheck(
		string Category,
		string Name,
		bool Ok,
		string? Message
	);

	/// <summary>
	/// validate 阶段产出的报告。
	/// </summary>
	public sealed record ValidationReport(
		bool OverallOk,
		int ErrorCount,
		int WarningCount,
		IReadOnlyList<ValidationCheck> Checks
	) {
		/// <summary>
		/// 人类可读输出（按 Category 分组；末尾 Summary 行）。
		/// </summary>
		public string ToText() {
			var sb = new StringBuilder();
			foreach (var check in Checks) {
				var tag = check.Ok ? "[OK]   " : "[ERROR]";
				var msg = string.IsNullOrEmpty(check.Message) ? string.Empty : $"  {check.Message}";
				sb.AppendLine($"{tag} {check.Category}.{check.Name}{msg}");
			}
			sb.AppendLine($"Summary: {ErrorCount} error(s), {WarningCount} warning(s)");
			return sb.ToString();
		}

		/// <summary>
		/// 机器可读 JSON 输出（camelCase，缩进）。
		/// </summary>
		public string ToJson() {
			return JsonSerializer.Serialize(this, new JsonSerializerOptions {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				WriteIndented = true,
			});
		}
	}
}
