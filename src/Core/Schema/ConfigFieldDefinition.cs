using System.Collections.Generic;

namespace Core.Schema {
    /// <summary>
    /// 领域层配置项元数据定义（来自 DefaultConfig.schema.json 单一真实源）。
    /// 解除核心配置管理、持久化与校验逻辑对 UI/TUI 呈现层的反向依赖。
    /// </summary>
    /// <param name="Key">配置键（相对根对象，大写下划线，如 LAYOUT_COLUMNS）</param>
    /// <param name="Label">显示名称</param>
    /// <param name="Choices">可选枚举值列表</param>
    /// <param name="Description">功能说明文字（来自 Schema 文档）</param>
    /// <param name="Constraints">内容约束说明或格式建议</param>
    /// <param name="Category">所属功能分组</param>
    /// <param name="Section">所属顶级根对象（"TEX" 或 "PROGRAM"）</param>
    /// <param name="ExpectedType">目标 JSON 类型（如 "string", "integer", "boolean", "array"）</param>
    /// <param name="Minimum">整数/数值约束的最小值</param>
    /// <param name="Maximum">整数/数值约束的最大值</param>
    /// <param name="Pattern">字符串/字段正则格式规则</param>
    /// <param name="JsonPath">原始 JSON 属性层级路径（如 "TEX.code.tab_size"）</param>
    internal record ConfigFieldDefinition(
        string Key,
        string Label,
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
}
