using System.IO;

namespace Core {
    /// <summary>
    /// init 子命令的上下文（已校验），由 SetAction 装配并向下注入。
    /// </summary>
    /// <param name="OutputPath">配置文件的输出路径（已确保其父目录存在）</param>
    /// <param name="Format">输出格式："jsonc"（默认，含 inline 注释）或 "json"（纯 JSON）</param>
    /// <param name="Force">是否强制覆盖已存在的目标文件（覆盖时会自动创建 .bak 备份）</param>
    internal sealed record InitSubcommandOptions(
        FileInfo OutputPath,
        string Format = "jsonc",
        bool Force = false
    );
}
