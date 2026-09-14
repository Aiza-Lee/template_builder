using System.Text;
using Utils;

namespace Core {
    /// <summary>
    /// 生成代码块TeX的类
    /// </summary>
    internal class CodeBlockGenerator {
        private readonly ILogger _logger;
        private readonly DirectoryInfo _sourceDirInfo;
        private readonly IConfigParser _programConfigParser;
        private readonly int _tabSize;
        private int _unresolvedPlaceholderCount;

        private readonly string CODE_BLOCK_TEMPLATE = string.Empty;
        private readonly HashSet<string> _includeFileTypes;
        private readonly string[] IGNORE_PATTERNS = [];

        /// <summary>
        /// 全部可用的章节命令（深度 0..4 对应 section..subparagraph）。代码内置硬编码 5 个；
        /// 实际使用时通过 <c>sectionDepth</c> 截取前 N 项（用户在 TEX.layout.section_depth 配置）。
        /// </summary>
        private static readonly string[] _allSectionCommands = [
            "section", "subsection", "subsubsection", "paragraph", "subparagraph"
        ];

        /// <summary>
        /// 24 条扩展名→minted 语言名默认映射。代码内置；用户在 PROGRAM.code_language_overrides
        /// 中写增量覆盖（如 {".md": "markdown"}），运行时合并到 <see cref="_languageMap"/>。
        /// 此映射同时作为「是否使用 minted 代码块」的判断依据（在映射中 = minted，否则 = 纯文本）。
        /// </summary>
        private static readonly Dictionary<string, string> _defaultExtMap = new(StringComparer.OrdinalIgnoreCase) {
            { "c", "c" }, { "cpp", "cpp" }, { "cc", "cpp" }, { "cxx", "cpp" },
            { "hpp", "cpp" }, { "hxx", "cpp" }, { "h", "c" }, { "java", "java" },
            { "cs", "csharp" }, { "rs", "rust" }, { "ts", "typescript" },
            { "js", "javascript" }, { "py", "python" }, { "rb", "ruby" },
            { "go", "go" }, { "php", "php" }, { "html", "html" },
            { "css", "css" }, { "xml", "xml" }, { "json", "json" },
            { "sh", "bash" }, { "bat", "batch" }, { "ps1", "powershell" },
            { "swift", "swift" }, { "kt", "kotlin" }, { "m", "objective-c" },
            { "sql", "sql" }, { "yaml", "yaml" }, { "yml", "yaml" }
        };


        // 实例字段（替代原 readonly 字段）
        /// <summary>合并默认 + 用户覆盖后的扩展名→语言映射。</summary>
        private readonly Dictionary<string, string> _languageMap;
        /// <summary>截取自 <see cref="_allSectionCommands"/> 的前 N 项（深度 [1,5]）。</summary>
        private readonly string[] _sectionCommands;
        /// <summary>章节名是否走 LatexEscaper；用户可在 TEX.layout.escape_section_names 关闭。</summary>
        private readonly bool _escapeSectionNames;


        public CodeBlockGenerator(
            ILogger logger,
            IConfigParser programConfigParser,
            int tabSize,
            DirectoryInfo sourceDir,
            string codeBlockTemplate,
            int sectionDepth,
            bool escapeSectionNames
        ) {
            _logger = logger;
            _sourceDirInfo = sourceDir;
            _programConfigParser = programConfigParser;
            _tabSize = tabSize;
            _escapeSectionNames = escapeSectionNames;

            CODE_BLOCK_TEMPLATE = codeBlockTemplate;
            var includeFileTypes = _programConfigParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
            _includeFileTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in includeFileTypes) {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                _includeFileTypes.Add(ext.StartsWith('.') ? ext : "." + ext);
            }
            IGNORE_PATTERNS = _programConfigParser["IGNORE_PATTERNS"].GetAsStringArray();

            // 章节深度 clamp 到 [1, _allSectionCommands.Length]
            var depth = Math.Clamp(sectionDepth, 1, _allSectionCommands.Length);
            _sectionCommands = _allSectionCommands.Take(depth).ToArray();

            // 语言映射：默认表 + 用户增量覆盖（PROGRAM.code_language_overrides）
            _languageMap = new Dictionary<string, string>(_defaultExtMap, StringComparer.OrdinalIgnoreCase);
            var overrides = _programConfigParser["CODE_LANGUAGE_OVERRIDES"].GetAsStringArray();
            foreach (var pair in overrides) {
                ParseAndApplyOverride(pair);
            }
        }

        /// <summary>
        /// 解析一对 "ext:lang" 字符串并合入 <see cref="_languageMap"/>。非法条目跳过（缺冒号 /
        /// 非 . 前缀 / 字段空）；非法条目通过 ILogger warn（初始化时调用，无重复 warn 风险）。
        /// </summary>
        private void ParseAndApplyOverride(string pair) {
            var idx = pair.IndexOf(':');
            if (idx <= 0 || idx >= pair.Length - 1) return; // 缺冒号或空字段，静默跳过
            var ext = pair[..idx].Trim();
            var lang = pair[(idx + 1)..].Trim();
            if (!ext.StartsWith('.')) {
                _logger.Warning($"code_language_overrides 条目 \"{pair}\" 的扩展名缺少 \".\" 前缀，已跳过。");
                return;
            }
            _languageMap[ext[1..]] = lang;
        }

        /// <summary>
        /// 生成代码块TeX
        /// </summary>
        /// <returns></returns>
        public string Generate() {
            _unresolvedPlaceholderCount = 0;
            var sourceDirInfo = _sourceDirInfo;
            if (!sourceDirInfo.Exists) {
                Directory.CreateDirectory(sourceDirInfo.FullName);
                _logger.Warning($"源目录 \"{sourceDirInfo.FullName}\" 不存在。已自动创建该目录，请添加源文件后重新构建。");
                return string.Empty;
            }

            var strBuilder = new StringBuilder();
            var lastIndex = _sectionCommands.Length - 1;
            var pendingDirs = new List<(int Depth, string Name, int EffectiveDepth)>();

            foreach (var entry in SourceTreeWalker.Walk(sourceDirInfo, IGNORE_PATTERNS, _logger)) {
                // 弹出深度不小于当前 entry 深度（即已离开该分支）的未决目录
                while (pendingDirs.Count > 0 && pendingDirs[^1].Depth >= entry.Depth) {
                    pendingDirs.RemoveAt(pendingDirs.Count - 1);
                }

                // 深度超过章节层级时 clamp 到最后一层（不报错不跳过）
                var effectiveDepth = entry.Depth >= _sectionCommands.Length ? lastIndex : entry.Depth;

                if (entry.IsDirectory) {
                    // 目录暂存为未决，待其子树中发现有效源码文件时再输出
                    pendingDirs.Add((entry.Depth, entry.Info.Name, effectiveDepth));
                    continue;
                }

                var codeBlock = GenerateCodeBlock_File((FileInfo)entry.Info);
                if (string.IsNullOrEmpty(codeBlock)) continue;

                // 存在有效代码文件，先输出所有父级未决目录的章节标题
                foreach (var dir in pendingDirs) {
                    InsertSection(strBuilder, dir.Name, dir.EffectiveDepth);
                }
                pendingDirs.Clear();

                InsertSection(strBuilder, entry.Info.Name, effectiveDepth);
                strBuilder.AppendLine(codeBlock);
            }
            return strBuilder.ToString();
        }

        /// <summary>
        /// 最近一次 Generate() 调用中发现的未替换占位符数量。供调用方决定退出码。
        /// </summary>
        public int UnresolvedPlaceholderCount => _unresolvedPlaceholderCount;

        /// <summary>
        /// 生成单个代码文件的代码块TeX
        /// </summary>
        /// <param name="codeFile">代码文件信息</param>
        /// <returns>返回生成的tex代码</returns>
        private string GenerateCodeBlock_File(FileInfo codeFile) {
            var rawExt = codeFile.Extension;
            // 检查文件类型是否在包含列表中（大小写无关匹配，O(1) 查找）
            if (!_includeFileTypes.Contains(rawExt)) {
                var extDisplay = rawExt.TrimStart('.');
                _logger.Debug($"文件类型 \"{extDisplay}\" 不在包含列表中，跳过文件 \"{codeFile.FullName}\"。");
                return string.Empty;
            }
            var content = File.ReadAllText(codeFile.FullName);
            content = ExpandTabs(content);

            var extKey = rawExt.StartsWith('.') ? rawExt[1..] : rawExt;
            // _languageMap 作为「是否走 minted 代码块」的单一判断：在映射中 → minted，否则 → 纯文本内嵌
            if (_languageMap.TryGetValue(extKey, out var language)) {
                var codeBlock = new StringBuilder(CODE_BLOCK_TEMPLATE);
                codeBlock.Replace("<<LANGUAGE>>", language);
                codeBlock.Replace("<<CODE>>", content);
                var rendered = codeBlock.ToString();
                foreach (var placeholder in TemplatePlaceholderScanner.FindUnresolved(rendered)) {
                    _logger.Error($"CodeBlock.tex 模板中存在未替换的占位符 \"{placeholder}\"（文件：{codeFile.Name}）。");
                    _unresolvedPlaceholderCount++;
                }
                return rendered;
            } else {
                return content;
            }
        }

        // 展开制表符
        private string ExpandTabs(string content) {
            if (string.IsNullOrEmpty(content)) return content;
            var sb = new StringBuilder(content.Length);
            int column = 0;
            foreach (char c in content) {
                if (c == '\t') {
                    // 防 tab_size ≤ 0 时除零；fallback 到 1（最小有效列宽）
                    var effectiveTabSize = _tabSize <= 0 ? 1 : _tabSize;
                    int spaces = effectiveTabSize - (column % effectiveTabSize);
                    sb.Append(' ', spaces);
                    column += spaces;
                } else if (c == '\n' || c == '\r') {
                    sb.Append(c);
                    column = 0;
                } else {
                    sb.Append(c);
                    column++;
                }
            }
            return sb.ToString();
        }


        /// <summary>
        /// 插入章节标题，根据深度选择合适的章节命令
        /// </summary>
        private void InsertSection(StringBuilder strBuilder, string sectionName, int depth) {
            if (depth < 0 || depth >= _sectionCommands.Length) {
                _logger.Error($"无效的章节深度：{depth}。无法插入章节 \"{sectionName}\"。");
                return;
            }
            var sectionCmd = _sectionCommands[depth];
            var displayName = _escapeSectionNames ? LatexEscaper.Escape(sectionName) : sectionName;
            strBuilder.AppendLine($"\\{sectionCmd}{{{displayName}}}");
            if (depth >= 3) {
                // 段落和子段落作为标题使用，后添加空行以增加可读性
                strBuilder.AppendLine(@"\textbf{ } \\");
            }
        }
    }
}
