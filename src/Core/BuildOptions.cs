using System.IO;

namespace Core {
	/// <summary>
	/// 构建命令所需的全部上下文（已校验），由组合根构造并向下注入。
	/// </summary>
	/// <param name="SourceDir">源文件目录（已校验存在）</param>
	/// <param name="OutputPdf">输出 PDF 路径（已确保其父目录存在）</param>
	/// <param name="ConfigFile">配置文件路径</param>
	/// <param name="Verbose">是否启用 DEBUG 日志</param>
	/// <param name="TemplateDir">外部模板目录（可选）。若提供，Main.tex / CodeBlock.tex 在该目录存在时优先使用，否则落回嵌入资源</param>
	internal sealed record BuildOptions(
		DirectoryInfo SourceDir,
		FileInfo OutputPdf,
		FileInfo ConfigFile,
		bool Verbose,
		DirectoryInfo? TemplateDir = null
	);
}