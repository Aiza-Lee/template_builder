namespace Core.Tui {
    /// <summary>TUI 快速配置项的输入控件类型。</summary>
    internal enum TuiFieldKind {
        /// <summary>自由文本输入。</summary>
        Text,
        /// <summary>从固定选项中单选（<see cref="TuiConfigField.Choices"/> 必填）。</summary>
        Choice,
        /// <summary>是 / 否 二选一。</summary>
        Toggle,
        /// <summary>数值输入（带范围约束）。</summary>
        Number,
        /// <summary>字符串数组输入（逗号分隔或 JSON 语法）。</summary>
        Array,
    }

    /// <summary>
    /// 一个 TUI 配置项的描述。
    /// <para>
    /// <see cref="Key"/> 必须与 <see cref="Utils.ConfigParser"/> 注册的键完全一致。注意键是
    /// <b>相对于根对象</b>的：解析器按 <c>new ConfigParser("TEX", …)</c> 构造，键由根对象内部的属性路径
    /// 逐层大写拼成，因此是 <c>LAYOUT_COLUMNS</c> 而非 <c>TEX_LAYOUT_COLUMNS</c>。
    /// </para>
    /// </summary>
    /// <param name="Key">配置键（相对根对象，大写下划线，如 <c>CODE_MINTED_STYLE</c>）</param>
    /// <param name="Label">界面显示名称</param>
    /// <param name="Kind">输入控件类型</param>
    /// <param name="Choices">可选值列表；仅 <see cref="TuiFieldKind.Choice"/> 使用</param>
    /// <param name="Description">功能说明文字（来自 Schema 文档）</param>
    /// <param name="Constraints">内容约束说明或格式建议</param>
    /// <param name="Category">所属功能分组</param>
    /// <param name="Section">所属顶级根对象（"TEX" 或 "PROGRAM"）</param>
    /// <param name="ExpectedType">目标 JSON 类型（如 "string", "integer", "boolean", "array"）</param>
    /// <param name="Minimum">整数/数值约束的最小值</param>
    /// <param name="Maximum">整数/数值约束的最大值</param>
    /// <param name="Pattern">字符串/字段正则格式规则</param>
    /// <param name="JsonPath">原始 JSON 属性层级路径（如 "TEX.code.tab_size"）</param>
    internal sealed record TuiConfigField(
        string Key,
        string Label,
        TuiFieldKind Kind,
        IReadOnlyList<string>? Choices = null,
        string? Description = null,
        string? Constraints = null,
        string? Category = null,
        string Section = "TEX",
        string? ExpectedType = null,
        int? Minimum = null,
        int? Maximum = null,
        string? Pattern = null,
        string? JsonPath = null
    );

    /// <summary>
    /// TUI 配置分类组。
    /// </summary>
    /// <param name="Id">分类标识符</param>
    /// <param name="Title">界面显示标题</param>
    /// <param name="Description">分类功能简要说明</param>
    /// <param name="Fields">该分类下包含的配置项集合</param>
    internal sealed record TuiCategoryGroup(
        string Id,
        string Title,
        string Description,
        IReadOnlyList<TuiConfigField> Fields
    );

    /// <summary>
    /// TUI 交互模式暴露的常用核心配置项清单。
    /// 由 <see cref="TuiSchemaCatalog"/> 统一提供单一真实源。
    /// </summary>
    internal static class TuiOptionsCatalog {
        /// <summary>常用快速配置项，由 TuiSchemaCatalog 自动挂载详细描述与约束。</summary>
        public static IReadOnlyList<TuiConfigField> Fields => TuiSchemaCatalog.FeaturedFields;
    }
}
