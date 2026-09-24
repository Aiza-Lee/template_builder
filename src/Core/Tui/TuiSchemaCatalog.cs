using System.Text.Json;
using Utils;

namespace Core.Tui {
    /// <summary>
    /// 从嵌入式 <c>DefaultConfig.schema.json</c> 自动派生与构建 TUI 配置元数据目录。
    /// <para>
    /// 单一真实源（SSOT）：Schema 定义了所有配置项的键名结构、字段类型、枚举值、
    /// 约束规则及中文描述。本类通过解析 Schema 动态构建全量配置元数据，
    /// 消除手写配置清单的双重维护负担，使后续新增配置时 TUI 自动生效。
    /// </para>
    /// </summary>
    internal static class TuiSchemaCatalog {
        private static readonly Lazy<CatalogData> s_data = new(LoadCatalog);

        public static IReadOnlyList<TuiConfigField> AllFields => s_data.Value.AllFields;
        public static IReadOnlyDictionary<string, TuiConfigField> FieldsByKey => s_data.Value.FieldsByKey;
        public static IReadOnlyList<TuiCategoryGroup> Categories => s_data.Value.Categories;
        public static IReadOnlyList<TuiConfigField> FeaturedFields => s_data.Value.FeaturedFields;

        private sealed record CatalogData(
            IReadOnlyList<TuiConfigField> AllFields,
            IReadOnlyDictionary<string, TuiConfigField> FieldsByKey,
            IReadOnlyList<TuiCategoryGroup> Categories,
            IReadOnlyList<TuiConfigField> FeaturedFields
        );

        private static readonly Dictionary<string, string> s_friendlyLabels = new(StringComparer.Ordinal) {
            ["AUTHOR"] = "作者",
            ["SUBJECT"] = "文档主题",
            ["TITLE_CONTENT"] = "文档标题",
            ["TITLE_NOTE"] = "副标题/说明",
            ["TITLE_DATE"] = "日期",
            ["TITLE_FONT_SIZE_CMD"] = "标题字号命令",
            ["TITLE_ALIGNMENT"] = "标题对齐方式",
            ["TITLE_ESCAPE_LATEX_SPECIALS"] = "转义 LaTeX 特殊字符",
            ["CODE_MINTED_STYLE"] = "代码高亮样式",
            ["CODE_FONT_FAMILY"] = "代码字体",
            ["CODE_FONT_SIZE"] = "代码字号",
            ["CODE_LINE_HEIGHT"] = "代码行距",
            ["CODE_TAB_SIZE"] = "制表符宽度",
            ["CODE_BG_COLOR"] = "代码背景色",
            ["CODE_BREAK_LINES"] = "长代码自动换行",
            ["CODE_LINE_NUMBERS"] = "显示代码行号",
            ["CODE_NUMBERS_SEP"] = "行号与代码间距",
            ["CODE_FRAME"] = "代码块边框",
            ["CODE_AUTOGOBBLE"] = "代码自动缩进吸收",
            ["CODE_HIGHLIGHT_COLOR"] = "高亮行背景色",
            ["CODE_MATHESCAPE"] = "代码数学公式转义",
            ["CODE_ESCAPEINSIDE"] = "代码块逃逸字符",
            ["CODE_XLEFTMARGIN"] = "代码左边距",
            ["CODE_XRIGHTMARGIN"] = "代码右边距",
            ["CODE_FIRSTNUMBER"] = "起始行号",
            ["CODE_STEPNUMBER"] = "行号递增步长",
            ["CODE_NUMBERSTYLE"] = "行号字体样式",
            ["CODE_SHOWSPACES"] = "显示空格符号",
            ["CODE_SHOWTABS"] = "显示制表符符号",
            ["CODE_MINTED_OUTPUTDIR"] = "minted 缓存目录",
            ["HYPERREF_ENABLE_COLORLINKS"] = "启用彩色超链接",
            ["HYPERREF_LINK_COLOR"] = "内部链接颜色",
            ["HYPERREF_FILE_COLOR"] = "文件链接颜色",
            ["HYPERREF_URL_COLOR"] = "URL 链接颜色",
            ["HYPERREF_NUMBERED_BOOKMARKS"] = "书签显示章节编号",
            ["HYPERREF_OPEN_BOOKMARKS"] = "打开 PDF 展开书签",
            ["HYPERREF_BOOKMARKS_DEPTH"] = "书签最大深度",
            ["HYPERREF_CITE_COLOR"] = "引用文献颜色",
            ["HYPERREF_ANCHOR_COLOR"] = "锚点颜色",
            ["HYPERREF_PDF_BORDER"] = "超链接边框样式",
            ["HYPERREF_PDF_LANG"] = "PDF 语言标记",
            ["GEOMETRY_PAPER_SIZE"] = "纸张尺寸",
            ["GEOMETRY_LEFT_MARGIN"] = "版心左边距",
            ["GEOMETRY_RIGHT_MARGIN"] = "版心右边距",
            ["GEOMETRY_TOP_MARGIN"] = "版心上边距",
            ["GEOMETRY_BOTTOM_MARGIN"] = "版心下边距",
            ["GEOMETRY_COLUMN_SEP"] = "双栏分栏间隙",
            ["GEOMETRY_HEADHEIGHT"] = "页眉高度",
            ["GEOMETRY_HEADSEP"] = "页眉与正文间距",
            ["GEOMETRY_FOOTSKIP"] = "页脚与正文间距",
            ["GEOMETRY_COLUMN_RULE"] = "双栏垂直分隔线",
            ["LAYOUT_SECTION_DEPTH"] = "章节编号深度",
            ["LAYOUT_TOC_DEPTH"] = "目录展示深度",
            ["LAYOUT_COLUMNS"] = "正文列数",
            ["LAYOUT_TOC_IN_COLUMNS"] = "目录跟随双栏",
            ["LAYOUT_ESCAPE_SECTION_NAMES"] = "章节名转义特殊字符",
            ["FANCY_HEAD_LEFT"] = "页眉左侧内容",
            ["FANCY_HEAD_CENTER"] = "页眉居中内容",
            ["FANCY_HEAD_RIGHT"] = "页眉右侧内容",
            ["FANCY_FOOT_LEFT"] = "页脚左侧内容",
            ["FANCY_FOOT_CENTER"] = "页脚居中内容",
            ["FANCY_FOOT_RIGHT"] = "页脚右侧内容",
            ["FANCY_RULE_WIDTH"] = "页眉分割线粗细",
            ["TYPESETTING_MICROTYPE_PROTRUSION"] = "字符伸出 (protrusion)",
            ["TYPESETTING_MICROTYPE_EXPANSION"] = "字体微伸缩 (expansion)",
            ["TYPESETTING_MICROTYPE_KERNING"] = "字偶距增强 (kerning)",
            ["TYPESETTING_PARSKIP_ENABLED"] = "段间距模式（替代段首缩进）",
            ["SECTION_FORMAT_SECTION"] = "Section 标题格式",
            ["SECTION_FORMAT_SUBSECTION"] = "Subsection 标题格式",
            ["SECTION_FORMAT_SUBSUBSECTION"] = "Subsubsection 标题格式",
            ["SECTION_FORMAT_PARAGRAPH"] = "Paragraph 标题格式",
            ["SECTION_FORMAT_SUBPARAGRAPH"] = "Subparagraph 标题格式",
            ["SECTION_FORMAT_SEPARATOR"] = "标题与编号间距",
            ["METADATA_KEYWORDS"] = "PDF 关键字列表",
            ["GLOBAL_MAIN_FONT"] = "西文主字体",
            ["GLOBAL_CJK_MAIN_FONT"] = "中文主字体",
            ["GLOBAL_CJK_SANS_FONT"] = "中文无衬线字体",
            ["GLOBAL_CJK_MAIN_BOLD_FONT"] = "中文粗体字体文件",
            ["GLOBAL_CJK_MAIN_ITALIC_FONT"] = "中文斜体字体文件",
            ["GLOBAL_CJK_AUTO_FAKE_BOLD"] = "伪粗体合成",
            ["GLOBAL_CJK_AUTO_FAKE_SLANT"] = "伪斜体合成",
            ["GLOBAL_CUSTOM_FONT_DIRS"] = "自定义本地字体目录",
            ["GLOBAL_PAGE_STYLE"] = "页眉页脚样式",
            ["TOC_TITLE"] = "目录标题文本",
            ["TOC_DOT_LEADERS"] = "目录虚线引导符",
            ["DOCCLASS_BASE_FONT_SIZE"] = "正文字号",
            ["DOCCLASS_ORIENTATION"] = "页面方向",
            ["DOCCLASS_FONTSET"] = "ctex 字体集预设",
            ["INCLUDE_FILE_TYPES"] = "包含源文件类型",
            ["IGNORE_PATTERNS"] = "排除 Glob 模式",
            ["CODE_LANGUAGE_OVERRIDES"] = "代码语言覆盖映射",
            ["BUILD_TIMEOUT_SECONDS"] = "编译超时秒数",
            ["BUILD_PASS_COUNT"] = "XeLaTeX 编译轮数"
        };

        private static readonly string[] s_featuredKeys = [
            "TITLE_CONTENT",
            "AUTHOR",
            "GLOBAL_MAIN_FONT",
            "GLOBAL_CJK_MAIN_FONT",
            "DOCCLASS_BASE_FONT_SIZE",
            "LAYOUT_COLUMNS",
            "GEOMETRY_PAPER_SIZE",
            "DOCCLASS_ORIENTATION",
            "CODE_MINTED_STYLE",
            "LAYOUT_SECTION_DEPTH",
            "GLOBAL_PAGE_STYLE",
            "TYPESETTING_PARSKIP_ENABLED"
        ];

        private static CatalogData LoadCatalog() {
            var rawJson = new ManifestResourceManager().GetResourceInString("DefaultConfig.schema.json");
            using var doc = JsonDocument.Parse(rawJson);
            var rootProps = doc.RootElement.GetProperty("properties");

            var allFields = new List<TuiConfigField>();

            if (rootProps.TryGetProperty("TEX", out var texProp)) {
                TraverseSchema(texProp, [], "TEX", allFields);
            }
            if (rootProps.TryGetProperty("PROGRAM", out var progProp)) {
                TraverseSchema(progProp, [], "PROGRAM", allFields);
            }

            var byKey = allFields.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);

            // 构建分类
            var categories = new List<TuiCategoryGroup> {
                new(
                    "featured",
                    "常用核心配置",
                    "高频调整项：标题、作者、字体、字号、列数、代码高亮等",
                    s_featuredKeys.Where(k => byKey.ContainsKey(k)).Select(k => byKey[k]).ToList()
                ),
                new(
                    "title",
                    "文档与标题页",
                    "作者、主标题、副标题、日期、对齐方式及字符转义",
                    allFields.Where(f => f.Category == "文档与标题页").ToList()
                ),
                new(
                    "code",
                    "代码排版 (minted)",
                    "代码字体、字号、行距、背景色、行号、边框及语言设置",
                    allFields.Where(f => f.Category == "代码排版 (minted)").ToList()
                ),
                new(
                    "layout",
                    "页面版心与布局",
                    "纸张尺寸、四周边距、双栏分隔线、页眉高度及方向",
                    allFields.Where(f => f.Category == "页面版心与布局").ToList()
                ),
                new(
                    "fonts",
                    "字体与微排版",
                    "西文主字体、中文宋体/黑体、微排版开关及段间距模式",
                    allFields.Where(f => f.Category == "字体与微排版").ToList()
                ),
                new(
                    "header_footer",
                    "页眉页脚与样式",
                    "页眉页脚 6 个槽位、分割线粗细及 Section 标题格式",
                    allFields.Where(f => f.Category == "页眉页脚与样式").ToList()
                ),
                new(
                    "hyperref_toc",
                    "超链接与目录",
                    "超链接着色、PDF 书签深度、目录标题及虚线引导符",
                    allFields.Where(f => f.Category == "超链接与目录").ToList()
                ),
                new(
                    "build",
                    "构建与文件过滤",
                    "包含/排除文件扩展名、编译超时秒数及 pass 轮数",
                    allFields.Where(f => f.Category == "构建与文件过滤").ToList()
                )
            };

            var featured = s_featuredKeys.Where(k => byKey.ContainsKey(k)).Select(k => byKey[k]).ToList();

            return new CatalogData(allFields, byKey, categories, featured);
        }

        private static void TraverseSchema(
            JsonElement node,
            List<string> path,
            string rootName,
            List<TuiConfigField> collector
        ) {
            if (!node.TryGetProperty("properties", out var properties)) return;

            foreach (var prop in properties.EnumerateObject()) {
                if (prop.Name == "$schema") continue;

                path.Add(prop.Name);
                var spec = prop.Value;
                var type = spec.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (type == "object" && spec.TryGetProperty("properties", out _)) {
                    TraverseSchema(spec, path, rootName, collector);
                } else {
                    var key = string.Join('_', path.Select(p => p.ToUpperInvariant()));
                    var label = s_friendlyLabels.TryGetValue(key, out var l) ? l : prop.Name;
                    var desc = spec.TryGetProperty("description", out var d) ? d.GetString() : null;

                    IReadOnlyList<string>? choices = null;
                    if (spec.TryGetProperty("enum", out var enumArr) && enumArr.ValueKind == JsonValueKind.Array) {
                        choices = enumArr.EnumerateArray()
                            .Select(e => e.ToString())
                            .ToList();
                    }

                    // 特殊针对 DOCCLASS_BASE_FONT_SIZE 与 GEOMETRY_PAPER_SIZE 补充已知典型 choices
                    if (choices == null) {
                        if (key == "DOCCLASS_BASE_FONT_SIZE") {
                            choices = ["10pt", "11pt", "12pt"];
                        } else if (key == "GEOMETRY_PAPER_SIZE") {
                            choices = ["a4paper", "letterpaper", "a5paper"];
                        } else if (key == "LAYOUT_SECTION_DEPTH" || key == "LAYOUT_TOC_DEPTH" || key == "HYPERREF_BOOKMARKS_DEPTH") {
                            choices = ["1", "2", "3", "4", "5"];
                        }
                    }

                    var kind = TuiFieldKind.Text;
                    if (choices is { Count: > 0 }) {
                        kind = TuiFieldKind.Choice;
                    } else if (type == "boolean") {
                        kind = TuiFieldKind.Toggle;
                    } else if (type is "integer" or "number") {
                        kind = TuiFieldKind.Number;
                    } else if (type == "array") {
                        kind = TuiFieldKind.Array;
                    }

                    var constraints = BuildConstraints(spec, choices, key);
                    var category = DetermineCategory(key, rootName);

                    int? minimum = spec.TryGetProperty("minimum", out var min) ? min.GetInt32() : null;
                    int? maximum = spec.TryGetProperty("maximum", out var max) ? max.GetInt32() : null;
                    string? pattern = spec.TryGetProperty("pattern", out var pat) ? pat.GetString() : null;
                    string jsonPath = $"{rootName}.{string.Join('.', path)}";

                    collector.Add(new TuiConfigField(
                        Key: key,
                        Label: label,
                        Kind: kind,
                        Choices: choices,
                        Description: desc,
                        Constraints: constraints,
                        Category: category,
                        Section: rootName,
                        ExpectedType: type,
                        Minimum: minimum,
                        Maximum: maximum,
                        Pattern: pattern,
                        JsonPath: jsonPath
                    ));
                }
                path.RemoveAt(path.Count - 1);
            }
        }

        private static string? BuildConstraints(JsonElement spec, IReadOnlyList<string>? choices, string key) {
            var parts = new List<string>();

            if (choices is { Count: > 0 }) {
                parts.Add($"可选值：{string.Join(", ", choices)}");
            }
            if (spec.TryGetProperty("minimum", out var min)) {
                if (spec.TryGetProperty("maximum", out var max)) {
                    parts.Add($"取值范围：{min.GetInt32()} ~ {max.GetInt32()}");
                } else {
                    parts.Add($"最小值：>= {min.GetInt32()}");
                }
            }
            if (spec.TryGetProperty("pattern", out var pattern)) {
                parts.Add($"格式正则：{pattern.GetString()}");
            }
            if (key.EndsWith("_MARGIN") || key.EndsWith("_HEIGHT") || key.EndsWith("_SEP") || key.EndsWith("_SIZE") || key.EndsWith("_WIDTH")) {
                parts.Add("需带单位（如 10pt, 1cm, 15mm, 0.5in）");
            }

            return parts.Count > 0 ? string.Join("；", parts) : null;
        }

        private static string DetermineCategory(string key, string rootName) {
            if (rootName == "PROGRAM") return "构建与文件过滤";

            if (key.StartsWith("AUTHOR") || key.StartsWith("SUBJECT") || key.StartsWith("TITLE_")) {
                return "文档与标题页";
            }
            if (key.StartsWith("CODE_")) {
                return "代码排版 (minted)";
            }
            if (key.StartsWith("GEOMETRY_") || key.StartsWith("LAYOUT_") || key.StartsWith("DOCCLASS_")) {
                return "页面版心与布局";
            }
            if (key.StartsWith("GLOBAL_MAIN_FONT") || key.StartsWith("GLOBAL_CJK_") || key.StartsWith("GLOBAL_CUSTOM_") || key.StartsWith("TYPESETTING_")) {
                return "字体与微排版";
            }
            if (key.StartsWith("GLOBAL_PAGE_STYLE") || key.StartsWith("FANCY_") || key.StartsWith("SECTION_")) {
                return "页眉页脚与样式";
            }
            if (key.StartsWith("HYPERREF_") || key.StartsWith("METADATA_") || key.StartsWith("TOC_")) {
                return "超链接与目录";
            }

            return "其他设置";
        }
    }
}
