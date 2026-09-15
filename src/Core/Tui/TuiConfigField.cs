namespace Core.Tui {
    /// <summary>TUI 快速配置项的输入控件类型。</summary>
    internal enum TuiFieldKind {
        /// <summary>自由文本输入。</summary>
        Text,
        /// <summary>从固定选项中单选（<see cref="TuiConfigField.Choices"/> 必填）。</summary>
        Choice,
        /// <summary>是 / 否 二选一。</summary>
        Toggle,
    }

    /// <summary>
    /// 一个 TUI 快速配置项的描述。
    /// <para>
    /// <see cref="Key"/> 必须与 <see cref="Utils.ConfigParser"/> 注册的键完全一致。注意键是
    /// <b>相对于根对象</b>的：解析器按 <c>new ConfigParser("TEX", …)</c> 构造，键由根对象内部的属性路径
    /// 逐层大写拼成，因此是 <c>LAYOUT_COLUMNS</c> 而非 <c>TEX_LAYOUT_COLUMNS</c>。
    /// </para>
    /// <para>
    /// 键写错不会静默生效——覆盖应用时会被判为未知键并记 Warning 跳过；
    /// <c>TuiOptionsCatalogTests</c> 会对全部键做「已在默认配置中注册」的校验。
    /// </para>
    /// </summary>
    /// <param name="Key">配置键（相对根对象，大写下划线，如 <c>CODE_MINTED_STYLE</c>）</param>
    /// <param name="Label">界面显示名称</param>
    /// <param name="Kind">输入控件类型</param>
    /// <param name="Choices">可选值列表；仅 <see cref="TuiFieldKind.Choice"/> 使用</param>
    internal sealed record TuiConfigField(
        string Key,
        string Label,
        TuiFieldKind Kind,
        IReadOnlyList<string>? Choices = null
    );

    /// <summary>
    /// TUI 交互模式暴露的「常用配置项」清单（12 项）。
    /// <para>
    /// 选取标准：高频、无需查阅文档即可理解、且值为标量（字符串 / 数值 / 布尔）——
    /// 因为覆盖走 <c>ConfigParser.TrySetConfigValueFromString</c>，它只支持标量强制转换。
    /// 全量配置项请直接编辑配置文件。
    /// </para>
    /// </summary>
    internal static class TuiOptionsCatalog {
        /// <summary>全部快速配置项，按界面展示顺序排列。</summary>
        public static IReadOnlyList<TuiConfigField> Fields { get; } = [
            new("TITLE_CONTENT", "文档标题", TuiFieldKind.Text),
            new("AUTHOR", "作者", TuiFieldKind.Text),
            new("GLOBAL_MAIN_FONT", "西文主字体", TuiFieldKind.Text),
            new("GLOBAL_CJK_MAIN_FONT", "中文主字体", TuiFieldKind.Text),

            new("DOCCLASS_BASE_FONT_SIZE", "正文字号", TuiFieldKind.Choice, ["10pt", "11pt", "12pt"]),
            new("LAYOUT_COLUMNS", "正文列数", TuiFieldKind.Choice, ["1", "2"]),
            new("GEOMETRY_PAPER_SIZE", "纸张尺寸", TuiFieldKind.Choice, ["a4paper", "letterpaper", "a5paper"]),
            new("DOCCLASS_ORIENTATION", "页面方向", TuiFieldKind.Choice, ["portrait", "landscape"]),

            new("CODE_MINTED_STYLE", "代码高亮样式", TuiFieldKind.Text),
            new("LAYOUT_SECTION_DEPTH", "章节编号深度", TuiFieldKind.Choice, ["1", "2", "3", "4", "5"]),
            new("GLOBAL_PAGE_STYLE", "页眉页脚样式", TuiFieldKind.Choice, ["plain", "headings", "empty", "fancy"]),

            new("TYPESETTING_PARSKIP_ENABLED", "段间距模式（替代段首缩进）", TuiFieldKind.Toggle),
        ];
    }
}
