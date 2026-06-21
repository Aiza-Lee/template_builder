using System.IO;

namespace Core {
	/// <summary>
	/// validate 子命令的上下文（已校验），由 SetAction 装配并向下注入。
	/// </summary>
	/// <param name="SourceDir">源文件目录（已校验存在）</param>
	/// <param name="ConfigFile">配置文件路径（已校验存在且为文件）</param>
	/// <param name="TemplateDir">外部模板目录（可选）</param>
	/// <param name="Format">输出格式："text"（默认）或 "json"</param>
	/// <param name="CheckXelatex">是否额外检查 xelatex 在 PATH 上</param>
	internal sealed record ValidateSubcommandOptions(
		DirectoryInfo SourceDir,
		FileInfo ConfigFile,
		DirectoryInfo? TemplateDir = null,
		string Format = "text",
		bool CheckXelatex = false
	);
}
