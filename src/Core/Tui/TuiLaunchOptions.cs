namespace Core.Tui {
    /// <summary>
    /// 启动 TUI 交互模式所需的上下文。所有路径均来自命令行（可为 null = 由 TUI 提示输入）。
    /// </summary>
    /// <param name="SourceDir">命令行已提供的源目录（可选，无效或无值时由 TUI 询问）</param>
    /// <param name="OutputPdf">命令行已提供的输出 PDF 路径（可选）</param>
    /// <param name="ConfigFile">命令行已解析出的配置文件路径（可为默认路径）</param>
    /// <param name="UserProvidedConfig">配置文件是否由用户在命令行显式指定（决定严格模式）</param>
    /// <param name="TemplateDir">外部模板目录（可选）</param>
    /// <param name="Verbose">是否启用 DEBUG 级日志</param>
    /// <param name="EnsureDefaultConfig">默认配置的回退工厂（首次运行时从嵌入资源生成）</param>
    internal sealed record TuiLaunchOptions(
        DirectoryInfo? SourceDir,
        FileInfo? OutputPdf,
        FileInfo? ConfigFile,
        bool UserProvidedConfig,
        DirectoryInfo? TemplateDir,
        bool Verbose,
        Func<FileInfo> EnsureDefaultConfig
    );
}
