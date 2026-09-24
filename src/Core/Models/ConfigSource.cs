namespace Core.Models {
    /// <summary>
    /// 配置项值的来源层级。优先级为：内置默认 (Builtin) -> 项目配置 (Project) -> 运行时覆盖 (Override)。
    /// </summary>
    internal enum ConfigSource {
        Builtin,
        Project,
        Override
    }
}
