using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Schema;
using Utils;

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

            foreach (var field in ConfigSchemaCatalog.AllFields) {
                var item = configuration.GetItem(field.Key);
                if (item == null) continue;

                var path = field.JsonPath ?? field.Key;

                // 1. JSON 原始类型准入校验（针对用户在配置文件中显式配置的项）
                if (item.Source == ConfigSource.Project && item.RawElement.HasValue) {
                    var raw = item.RawElement.Value;
                    if (field.ExpectedType == "string") {
                        if (raw.ValueKind != JsonValueKind.String) {
                            errors.Add(new ConfigValidationError(
                                path, field.Key, item.EffectiveValue ?? string.Empty,
                                Core.Localization.I18n.T("ConfigValidation.ExpectedString", "字符串 (string)"),
                                string.Format(Core.Localization.I18n.T("ConfigValidation.MustBeString", "配置项 {0} 的类型必须为字符串，但传入了 {1}。"), path, raw.ValueKind),
                                Core.Localization.I18n.T("ConfigValidation.WrapQuotes", "请使用双引号包裹该值。")
                            ));
                            continue;
                        }
                    } else if (field.ExpectedType == "boolean") {
                        if (raw.ValueKind is JsonValueKind.Array or JsonValueKind.Object) {
                            errors.Add(new ConfigValidationError(
                                path, field.Key, item.EffectiveValue ?? string.Empty,
                                Core.Localization.I18n.T("ConfigValidation.ExpectedBool", "布尔值 (true / false)"),
                                string.Format(Core.Localization.I18n.T("ConfigValidation.MustBeBool", "配置项 {0} 的类型必须为布尔值，但传入了 {1}。"), path, raw.ValueKind),
                                Core.Localization.I18n.T("ConfigValidation.SetBoolTip", "请将该项修改为 true 或 false。")
                            ));
                            continue;
                        }
                    } else if (field.ExpectedType is "integer" or "number") {
                        if (raw.ValueKind is JsonValueKind.Array or JsonValueKind.Object) {
                            errors.Add(new ConfigValidationError(
                                path, field.Key, item.EffectiveValue ?? string.Empty,
                                Core.Localization.I18n.T("ConfigValidation.ExpectedInt", "整数 (integer)"),
                                string.Format(Core.Localization.I18n.T("ConfigValidation.MustBeInt", "配置项 {0} 的类型必须为整数数值，但传入了 {1}。"), path, raw.ValueKind),
                                Core.Localization.I18n.T("ConfigValidation.SetIntTip", "请将该项修改为未加引号的整数数值。")
                            ));
                            continue;
                        }
                    } else if (field.ExpectedType == "array") {
                        if (raw.ValueKind != JsonValueKind.Array) {
                            errors.Add(new ConfigValidationError(
                                path, field.Key, item.EffectiveValue ?? string.Empty,
                                Core.Localization.I18n.T("ConfigValidation.ExpectedArray", "数组 (array)"),
                                string.Format(Core.Localization.I18n.T("ConfigValidation.MustBeArray", "配置项 {0} 的类型必须为数组，但传入了 {1}。"), path, raw.ValueKind),
                                Core.Localization.I18n.T("ConfigValidation.SetArrayTip", "请使用方括号包裹数组元素，如 [\"...\"]。")
                            ));
                            continue;
                        }

                        // 数组项类型校验：必须全部为 string
                        bool hasItemTypeError = false;
                        foreach (var arrElem in raw.EnumerateArray()) {
                            if (arrElem.ValueKind != JsonValueKind.String) {
                                errors.Add(new ConfigValidationError(
                                    path, field.Key, arrElem.ValueKind == JsonValueKind.String ? arrElem.GetString() ?? string.Empty : arrElem.GetRawText(),
                                    Core.Localization.I18n.T("ConfigValidation.ExpectedStringItem", "字符串数组项 (string)"),
                                    string.Format(Core.Localization.I18n.T("ConfigValidation.ArrayItemMustBeString", "配置项 {0} 的数组元素必须为字符串，但包含类型为 {1} 的项。"), path, arrElem.ValueKind),
                                    Core.Localization.I18n.T("ConfigValidation.ArrayItemQuoteTip", "请确保数组中的每个元素都为用双引号包裹的字符串。")
                                ));
                                hasItemTypeError = true;
                                break;
                            }
                        }
                        if (hasItemTypeError) {
                            continue;
                        }
                    }
                }

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
        public static (bool IsValid, ConfigValidationError? Error) ValidateSingleValue(ConfigFieldDefinition field, string? rawValue) {
            var path = field.JsonPath ?? field.Key;
            var val = rawValue?.Trim() ?? string.Empty;

            // 1. 整数类型校验
            if (field.ExpectedType == "integer") {
                if (string.IsNullOrWhiteSpace(val)) {
                    var expected = FormatIntegerExpected(field);
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        string.Format(Core.Localization.I18n.T("ConfigValidation.NotEmptyInt", "配置项 {0} 不能为空，必须为整数。"), path),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.ModifyToIntTip", "请将 {0} 修改为合法的整数数值（{1}）。"), path, expected)
                    ));
                }

                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal)) {
                    var expected = FormatIntegerExpected(field);
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        string.Format(Core.Localization.I18n.T("ConfigValidation.NotValidInt", "配置项 {0} 的值 \"{1}\" 不是合法的整数。"), path, val),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.ModifyToIntTip", "请将 {0} 修改为合法的整数数值（{1}）。"), path, expected)
                    ));
                }

                if (field.Minimum.HasValue && intVal < field.Minimum.Value) {
                    var expected = $">= {field.Minimum.Value}";
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        string.Format(Core.Localization.I18n.T("ConfigValidation.IntTooSmall", "配置项 {0} 的值 {1} 小于允许的最小值 {2}。"), path, intVal, field.Minimum.Value),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.SetIntMinTip", "请将 {0} 设为不小于 {1} 的整数。"), path, field.Minimum.Value)
                    ));
                }

                if (field.Maximum.HasValue && intVal > field.Maximum.Value) {
                    var expected = $"<= {field.Maximum.Value}";
                    return (false, new ConfigValidationError(
                        path, field.Key, val, expected,
                        string.Format(Core.Localization.I18n.T("ConfigValidation.IntTooLarge", "配置项 {0} 的值 {1} 大于允许的最大值 {2}。"), path, intVal, field.Maximum.Value),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.SetIntMaxTip", "请将 {0} 设为不大于 {1} 的整数。"), path, field.Maximum.Value)
                    ));
                }
            }

            // 2. 布尔类型校验
            if (field.ExpectedType == "boolean") {
                if (string.IsNullOrWhiteSpace(val)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, Core.Localization.I18n.T("ConfigValidation.ExpectedBool", "布尔值 (true / false)"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.NotEmptyBool", "配置项 {0} 不能为空，必须为布尔值。"), path),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.SetBoolTip", "请将 {0} 设为 true 或 false。"), path)
                    ));
                }

                if (!bool.TryParse(val, out _)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, Core.Localization.I18n.T("ConfigValidation.ExpectedBool", "布尔值 (true / false)"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.NotValidBool", "配置项 {0} 的值 \"{1}\" 不是合法的布尔值。"), path, val),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.SetBoolTip", "请将 {0} 设为 true 或 false。"), path)
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
                        string.Format(Core.Localization.I18n.T("ConfigValidation.NotInEnumList", "配置项 {0} 的值 \"{1}\" 不在允许的枚举值列表中。"), path, val),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.ChooseFromOptions", "请从以下合法选项中选择：{0}。"), expected)
                    ));
                }
            }

            // 4. 正则 Pattern 校验
            if (!string.IsNullOrWhiteSpace(field.Pattern) && !string.IsNullOrEmpty(val)) {
                if (!Regex.IsMatch(val, field.Pattern)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, string.Format(Core.Localization.I18n.T("ConfigValidation.PatternExpected", "符合正则模式：{0}"), field.Pattern),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.PatternMismatch", "配置项 {0} 的值 \"{1}\" 不符合格式规则（模式：{2}）。"), path, val, field.Pattern),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.PatternTip", "请按格式要求（{0}）修改配置值。"), field.Pattern)
                    ));
                }
            }

            // 5. LaTeX 尺寸表达式校验
            if (s_dimensionKeys.Contains(field.Key) && !string.IsNullOrWhiteSpace(val)) {
                if (!s_latexDimensionRegex.IsMatch(val)) {
                    return (false, new ConfigValidationError(
                        path, field.Key, val, Core.Localization.I18n.T("ConfigValidation.LatexDimensionExpected", "带长度单位的 LaTeX 尺寸（如 10pt, 1.5cm, 20mm, 0.5in）"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.InvalidLatexDimension", "配置项 {0} 的值 \"{1}\" 不是合法的 LaTeX 尺寸表达式。"), path, val),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.LatexDimensionTip", "请为 {0} 指定带单位的尺寸，如 \"10pt\"、\"1.5cm\" 或 \"20mm\"。"), path)
                    ));
                }
            }

            return (true, null);
        }

        /// <summary>
        /// 获取字段的数值下限与上限（若定义）。
        /// </summary>
        public static (int? Min, int? Max) GetNumberConstraint(string key) {
            return ConfigSchemaCatalog.GetNumberConstraint(key);
        }

        private static string FormatIntegerExpected(ConfigFieldDefinition field) {
            var intWord = Core.Localization.I18n.T("ConfigValidation.IntegerWord", "整数");
            if (field.Minimum.HasValue && field.Maximum.HasValue) {
                return $"{intWord} ({field.Minimum.Value} ~ {field.Maximum.Value})";
            }
            if (field.Minimum.HasValue) {
                return $"{intWord} (>= {field.Minimum.Value})";
            }
            if (field.Maximum.HasValue) {
                return $"{intWord} (<= {field.Maximum.Value})";
            }
            return intWord;
        }

        private static string[] GetSafeStringArray(ConfigParser parser, string key) {
            try {
                return parser[key].GetAsStringArray();
            } catch (InvalidCastException) {
                return Array.Empty<string>();
            }
        }

        private static void ValidateArrayFields(ProjectConfiguration config, List<ConfigValidationError> errors) {
            // INCLUDE_FILE_TYPES: 白名单扩展名必须非空，且必须以 '.' 开头并符合正则
            var includeTypes = GetSafeStringArray(config.ProgramParser, "INCLUDE_FILE_TYPES");
            if (includeTypes.Length == 0 || includeTypes.All(string.IsNullOrWhiteSpace)) {
                errors.Add(new ConfigValidationError(
                    "PROGRAM.include_file_types",
                    "INCLUDE_FILE_TYPES",
                    "[]",
                    Core.Localization.I18n.T("ConfigValidation.IncludeTypesExpected", "非空的扩展名白名单数组（如 [\".cs\", \".cpp\"]）"),
                    Core.Localization.I18n.T("ConfigValidation.IncludeTypesNotEmpty", "配置项 PROGRAM.include_file_types 不能为空，否则无法匹配任何源文件。"),
                    Core.Localization.I18n.T("ConfigValidation.IncludeTypesTip", "请在 PROGRAM.include_file_types 中添加至少一种源文件扩展名（需以 \".\" 开头）。")
                ));
            } else {
                foreach (var ext in includeTypes) {
                    if (string.IsNullOrWhiteSpace(ext)) continue;
                    if (!Regex.IsMatch(ext, @"^\.[A-Za-z0-9]+$")) {
                        errors.Add(new ConfigValidationError(
                            "PROGRAM.include_file_types",
                            "INCLUDE_FILE_TYPES",
                            ext,
                            Core.Localization.I18n.T("ConfigValidation.ExtFormatExpected", "扩展名需以 \".\" 开头后接字母或数字（如 \".cs\"）"),
                            string.Format(Core.Localization.I18n.T("ConfigValidation.ExtFormatIllegal", "配置项 PROGRAM.include_file_types 中的扩展名 \"{0}\" 格式非法。"), ext),
                            string.Format(Core.Localization.I18n.T("ConfigValidation.ExtFormatTip", "请修正为合法的扩展名格式（例如添加前缀点 \".{0}\"）。"), ext.TrimStart('.'))
                        ));
                    }
                }
            }

            // CODE_LANGUAGE_OVERRIDES: 格式必须为 .ext:lang
            var overrides = GetSafeStringArray(config.ProgramParser, "CODE_LANGUAGE_OVERRIDES");
            foreach (var item in overrides) {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (!Regex.IsMatch(item, @"^\.[A-Za-z0-9]+:[A-Za-z0-9_+\-]+$")) {
                    errors.Add(new ConfigValidationError(
                        "PROGRAM.code_language_overrides",
                        "CODE_LANGUAGE_OVERRIDES",
                        item,
                        Core.Localization.I18n.T("ConfigValidation.LangOverrideExpected", "格式需为 \".ext:lang\"（如 \".rs:rust\"）"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.LangOverrideIllegal", "配置项 PROGRAM.code_language_overrides 中的条目 \"{0}\" 格式非法。"), item),
                        Core.Localization.I18n.T("ConfigValidation.LangOverrideTip", "请按 \".扩展名:语言名\" 格式书写，例如 \".md:markdown\"。")
                    ));
                }
            }

            // IGNORE_PATTERNS: 元素不得为空白字符串
            var ignorePatterns = GetSafeStringArray(config.ProgramParser, "IGNORE_PATTERNS");
            foreach (var pattern in ignorePatterns) {
                if (string.IsNullOrWhiteSpace(pattern)) {
                    errors.Add(new ConfigValidationError(
                        "PROGRAM.ignore_patterns",
                        "IGNORE_PATTERNS",
                        pattern,
                        Core.Localization.I18n.T("ConfigValidation.IgnorePatternExpected", "非空的 Glob 匹配模式（如 \"**/.*\"）"),
                        Core.Localization.I18n.T("ConfigValidation.IgnorePatternEmpty", "配置项 PROGRAM.ignore_patterns 包含空通配符条目。"),
                        Core.Localization.I18n.T("ConfigValidation.IgnorePatternTip", "请移除空白项或填入有效的排除通配符。")
                    ));
                }
            }

            // SECTION_ORDER_OVERRIDES: 格式必须为 target:order
            var orderOverrides = GetSafeStringArray(config.ProgramParser, "SECTION_ORDER_OVERRIDES");
            foreach (var item in orderOverrides) {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (!Regex.IsMatch(item, @"^.+:-?\d+$")) {
                    errors.Add(new ConfigValidationError(
                        "PROGRAM.section_order_overrides",
                        "SECTION_ORDER_OVERRIDES",
                        item,
                        Core.Localization.I18n.T("ConfigValidation.OrderOverrideExpected", "格式需为 \"目标路径:整数权重\"（如 \"core.py:10\"）"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.OrderOverrideIllegal", "配置项 PROGRAM.section_order_overrides 中的条目 \"{0}\" 格式非法。"), item),
                        Core.Localization.I18n.T("ConfigValidation.OrderOverrideTip", "请按 \"相对路径:整数权重\" 格式书写，例如 \"01_intro.py:10\"。")
                    ));
                }
            }

            // SECTION_TITLE_OVERRIDES: 格式必须为 target:title
            var titleOverrides = GetSafeStringArray(config.ProgramParser, "SECTION_TITLE_OVERRIDES");
            foreach (var item in titleOverrides) {
                if (string.IsNullOrWhiteSpace(item)) continue;
                var colonIdx = item.IndexOf(':');
                if (colonIdx <= 0 || colonIdx == item.Length - 1) {
                    errors.Add(new ConfigValidationError(
                        "PROGRAM.section_title_overrides",
                        "SECTION_TITLE_OVERRIDES",
                        item,
                        Core.Localization.I18n.T("ConfigValidation.TitleOverrideExpected", "格式需为 \"目标路径:自定义标题\"（如 \"core.py:核心算法\"）"),
                        string.Format(Core.Localization.I18n.T("ConfigValidation.TitleOverrideIllegal", "配置项 PROGRAM.section_title_overrides 中的条目 \"{0}\" 格式非法。"), item),
                        Core.Localization.I18n.T("ConfigValidation.TitleOverrideTip", "请按 \"相对路径:自定义显示标题\" 格式书写，例如 \"01_intro.py:引言\"。")
                    ));
                }
            }
        }
    }
}
