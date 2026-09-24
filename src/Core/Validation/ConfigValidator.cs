using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Tui;

namespace Core.Validation {
    /// <summary>
    /// 配置校验错误信息。
    /// </summary>
    /// <param name="PropertyPath">配置项 JSON 属性层级路径（如 "TEX.code.tab_size"）</param>
    /// <param name="Key">内部配置键名（如 "CODE_TAB_SIZE"）</param>
    /// <param name="CurrentValue">当前配置的非法值</param>
    /// <param name="Expected">期望的类型、取值范围或格式规则说明</param>
    /// <param name="Message">错误描述</param>
    /// <param name="Recommendation">针对该配置项的修复建议</param>
    public sealed record ConfigValidationError(
        string PropertyPath,
        string Key,
        string CurrentValue,
        string Expected,
        string Message,
        string Recommendation
    );

    /// <summary>
    /// 配置校验结果。
    /// </summary>
    public sealed record ConfigValidationResult(
        bool IsValid,
        IReadOnlyList<ConfigValidationError> Errors
    ) {
        public static ConfigValidationResult Success { get; } = new(true, Array.Empty<ConfigValidationError>());
        public static ConfigValidationResult Failure(IReadOnlyList<ConfigValidationError> errors) => new(false, errors);
    }

    /// <summary>
    /// 统一配置规则校验器：
    /// 对 ProjectConfiguration 的最终生效值进行类型、枚举范围、正则约束、LaTeX 尺寸格式及字段关系校验。
    /// 提供单字段校验供 TUI 交互拦截，以及全量校验供 CLI build 与 validate 准入判定。
    /// </summary>
    internal static class ConfigValidator {
        private static readonly Regex s_latexDimensionRegex = new(
            @"^(\s*(-?\d+(\.\d+)?)\s*(pt|mm|cm|in|ex|em|bp|dd|pc|sp)\s*|0|0pt|\\[a-zA-Z]+.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        private static readonly Dictionary<string, string[]> s_schemaEnums = new(StringComparer.OrdinalIgnoreCase) {
            ["LAYOUT_COLUMNS"] = ["1", "2"],
            ["BUILD_PASS_COUNT"] = ["1", "2"],
            ["TITLE_ALIGNMENT"] = ["center", "flushleft", "flushright"],
            ["CODE_FRAME"] = ["none", "leftline", "lines", "single"],
            ["GLOBAL_PAGE_STYLE"] = ["plain", "headings", "empty", "fancy"],
            ["DOCCLASS_ORIENTATION"] = ["landscape", "portrait"],
            ["DOCCLASS_FONTSET"] = ["auto", "fandol", "ubuntu", "windows", "mac", "none"]
        };

        private static readonly HashSet<string> s_dimensionKeys = new(StringComparer.OrdinalIgnoreCase) {
            "GEOMETRY_LEFT_MARGIN",
            "GEOMETRY_RIGHT_MARGIN",
            "GEOMETRY_TOP_MARGIN",
            "GEOMETRY_BOTTOM_MARGIN",
            "GEOMETRY_COLUMN_SEP",
            "GEOMETRY_HEADHEIGHT",
            "GEOMETRY_HEADSEP",
            "GEOMETRY_FOOTSKIP",
            "CODE_NUMBERS_SEP",
            "CODE_XLEFTMARGIN",
            "CODE_XRIGHTMARGIN",
            "FANCY_RULE_WIDTH",
            "DOCCLASS_BASE_FONT_SIZE"
        };

        /// <summary>
        /// 全量校验项目配置中所有项的最终生效值。
        /// </summary>
        public static ConfigValidationResult Validate(ProjectConfiguration configuration) {
            var errors = new List<ConfigValidationError>();

            foreach (var field in TuiSchemaCatalog.AllFields) {
                var item = configuration.GetItem(field.Key);
                if (item == null) continue;

                var effectiveValue = item.EffectiveValue;
                var singleResult = ValidateSingleValue(field, effectiveValue);
                if (!singleResult.IsValid && singleResult.Error != null) {
                    errors.Add(singleResult.Error);
                }
            }

            // 针对数组类型字段的项深度校验
            ValidateArrayFields(configuration, errors);

            return errors.Count == 0 ? ConfigValidationResult.Success : ConfigValidationResult.Failure(errors);
        }

        /// <summary>
        /// 校验单个字段的新取值。供 TUI 输入时实时拦截与单元测试使用。
        /// </summary>
        public static (bool IsValid, ConfigValidationError? Error) ValidateSingleValue(TuiConfigField field, string? rawValue) {
            var path = field.JsonPath ?? field.Key;
            var val = rawValue?.Trim() ?? string.Empty;

            // 1. 整数类型校验
            if (field.ExpectedType == "integer") {
                if (string.IsNullOrWhiteSpace(val)) {
                    var expected = FormatIntegerExpected(field);
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        $"配置项 {path} 不能为空，必须为整数。",
                        $"请将 {path} 修改为合法的整数数值（{expected}）。"
                    ));
                }

                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal)) {
                    var expected = FormatIntegerExpected(field);
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        $"配置项 {path} 的值 \"{val}\" 不是合法的整数。",
                        $"请将 {path} 修改为合法的整数数值（{expected}）。"
                    ));
                }

                if (field.Minimum.HasValue && intVal < field.Minimum.Value) {
                    var expected = $">= {field.Minimum.Value}";
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        $"配置项 {path} 的值 {intVal} 小于允许的最小值 {field.Minimum.Value}。",
                        $"请将 {path} 设为不小于 {field.Minimum.Value} 的整数。"
                    ));
                }

                if (field.Maximum.HasValue && intVal > field.Maximum.Value) {
                    var expected = $"<= {field.Maximum.Value}";
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        $"配置项 {path} 的值 {intVal} 大于允许的最大值 {field.Maximum.Value}。",
                        $"请将 {path} 设为不大于 {field.Maximum.Value} 的整数。"
                    ));
                }
            }

            // 2. 布尔类型校验
            if (field.ExpectedType == "boolean") {
                if (string.IsNullOrWhiteSpace(val)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, "布尔值 (true / false)",
                        $"配置项 {path} 不能为空，必须为布尔值。",
                        $"请将 {path} 设为 true 或 false。"
                    ));
                }

                if (!bool.TryParse(val, out _) && val != "1" && val != "0") {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, "布尔值 (true / false)",
                        $"配置项 {path} 的值 \"{val}\" 不是合法的布尔值。",
                        $"请将 {path} 设为 true 或 false。"
                    ));
                }
            }

            // 3. 枚举约束校验
            if (s_schemaEnums.TryGetValue(field.Key, out var allowedEnums)) {
                // 特例：DOCCLASS_FONTSET 允许空字符串（代表 auto）
                if (field.Key.Equals("DOCCLASS_FONTSET", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(val)) {
                    // 合法
                } else if (!allowedEnums.Any(e => e.Equals(val, StringComparison.OrdinalIgnoreCase))) {
                    var expected = string.Join(", ", allowedEnums);
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        $"配置项 {path} 的值 \"{val}\" 不在允许的枚举值列表中。",
                        $"请从以下合法选项中选择：{expected}。"
                    ));
                }
            }

            // 4. 正则 Pattern 校验
            if (!string.IsNullOrWhiteSpace(field.Pattern) && !string.IsNullOrEmpty(val)) {
                if (!Regex.IsMatch(val, field.Pattern)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, $"符合正则模式：{field.Pattern}",
                        $"配置项 {path} 的值 \"{val}\" 不符合格式规则（模式：{field.Pattern}）。",
                        $"请按格式要求（{field.Pattern}）修改配置值。"
                    ));
                }
            }

            // 5. LaTeX 尺寸表达式校验
            if (s_dimensionKeys.Contains(field.Key) && !string.IsNullOrWhiteSpace(val)) {
                if (!s_latexDimensionRegex.IsMatch(val)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, "带长度单位的 LaTeX 尺寸（如 10pt, 1.5cm, 20mm, 0.5in）",
                        $"配置项 {path} 的值 \"{val}\" 不是合法的 LaTeX 尺寸表达式。",
                        $"请为 {path} 指定带单位的尺寸，如 \"10pt\"、\"1.5cm\" 或 \"20mm\"。"
                    ));
                }
            }

            return (true, null);
        }

        /// <summary>
        /// 获取字段的数值下限与上限（若定义）。
        /// </summary>
        public static (int? Min, int? Max) GetNumberConstraint(string key) {
            if (TuiSchemaCatalog.FieldsByKey.TryGetValue(key, out var field)) {
                return (field.Minimum, field.Maximum);
            }
            return (null, null);
        }

        private static string FormatIntegerExpected(TuiConfigField field) {
            if (field.Minimum.HasValue && field.Maximum.HasValue) {
                return $"整数 ({field.Minimum.Value} ~ {field.Maximum.Value})";
            }
            if (field.Minimum.HasValue) {
                return $"整数 (>= {field.Minimum.Value})";
            }
            if (field.Maximum.HasValue) {
                return $"整数 (<= {field.Maximum.Value})";
            }
            return "整数";
        }

        private static void ValidateArrayFields(ProjectConfiguration config, List<ConfigValidationError> errors) {
            // INCLUDE_FILE_TYPES: 白名单扩展名必须非空，且必须以 '.' 开头并符合正则
            var includeTypes = config.ProgramParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
            if (includeTypes.Length == 0 || includeTypes.All(string.IsNullOrWhiteSpace)) {
                errors.Add(new ConfigValidationError(
                    "PROGRAM.include_file_types",
                    "INCLUDE_FILE_TYPES",
                    "[]",
                    "非空的扩展名白名单数组（如 [\".cs\", \".cpp\"]）",
                    "配置项 PROGRAM.include_file_types 不能为空，否则无法匹配任何源文件。",
                    "请在 PROGRAM.include_file_types 中添加至少一种源文件扩展名（需以 \".\" 开头）。"
                ));
            } else {
                foreach (var ext in includeTypes) {
                    if (string.IsNullOrWhiteSpace(ext)) continue;
                    if (!Regex.IsMatch(ext, @"^\.[A-Za-z0-9]+$")) {
                        errors.Add(new ConfigValidationError(
                            "PROGRAM.include_file_types",
                            "INCLUDE_FILE_TYPES",
                            ext,
                            "扩展名需以 \".\" 开头后接字母或数字（如 \".cs\"）",
                            $"配置项 PROGRAM.include_file_types 中的扩展名 \"{ext}\" 格式非法。",
                            $"请修正为合法的扩展名格式（例如添加前缀点 \".{ext.TrimStart('.')}\"）。"
                        ));
                    }
                }
            }

            // CODE_LANGUAGE_OVERRIDES: 格式必须为 .ext:lang
            var overrides = config.ProgramParser["CODE_LANGUAGE_OVERRIDES"].GetAsStringArray();
            foreach (var item in overrides) {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (!Regex.IsMatch(item, @"^\.[A-Za-z0-9]+:[A-Za-z0-9_+\-]+$")) {
                    errors.Add(new ConfigValidationError(
                        "PROGRAM.code_language_overrides",
                        "CODE_LANGUAGE_OVERRIDES",
                        item,
                        "格式需为 \".ext:lang\"（如 \".rs:rust\"）",
                        $"配置项 PROGRAM.code_language_overrides 中的条目 \"{item}\" 格式非法。",
                        "请按 \".扩展名:语言名\" 格式书写，例如 \".md:markdown\"。"
                    ));
                }
            }
        }
    }
}
