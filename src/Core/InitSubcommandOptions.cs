using System.IO;

namespace Core {
	/// <summary>
	/// init 子命令的上下文（已校验），由 SetAction 装配并向下注入。
	/// </summary>
	/// <param name="OutputPath">配置文件的输出路径（已确保其父目录存在）</param>
	/// <param name="Format">输出格式："jsonc"（默认，含 inline 注释）或 "json"（纯 JSON）</param>
	internal sealed record InitSubcommandOptions(
		FileInfo OutputPath,
		string Format = "jsonc"
	);
}
