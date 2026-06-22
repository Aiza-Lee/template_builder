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
		private readonly string[] INCLUDE_FILE_TYPES = [];
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
		/// </summary>
		private static readonly Dictionary<string, string> _defaultExtMap = new(StringComparer.OrdinalIgnoreCase) {
			{ "cpp", "cpp" }, { "hpp", "cpp" }, { "h", "c" },
			{ "cs", "csharp" }, { "rs", "rust" }, { "ts", "typescript" },
			{ "js", "javascript" }, { "py", "python" }, { "rb", "ruby" },
			{ "go", "go" }, { "php", "php" }, { "html", "html" },
			{ "css", "css" }, { "xml", "xml" }, { "json", "json" },
			{ "sh", "bash" }, { "bat", "batch" }, { "ps1", "powershell" },
			{ "swift", "swift" }, { "kt", "kotlin" }, { "m", "objective-c" },
			{ "sql", "sql" }, { "yaml", "yaml" }, { "yml", "yaml" }
		};

		/// <summary>
		/// 作为代码块处理的文件扩展名列表（白名单由用户配置 PROGRAM.include_file_types）。
		/// </summary>
		private readonly HashSet<string> CODE_LANGUAGES_EXTENSIONS = [
			"cpp", "c", "hpp", "h", "cs", "rs", "ts", "js", "java",
			"py", "rb", "go", "php", "html", "css", "xml", "json",
			"sh", "bat", "ps1", "swift", "kt", "m", "sql", "yaml", "yml"
		];
		private const string DEFAULT_LANGUAGE = "PlainText";

		// === Round 3a: 实例字段（替代原 readonly 字段）===
		/// <summary>合并默认 + 用户覆盖后的扩展名→语言映射。</summary>
		private readonly Dictionary<string, string> _languageMap;
		/// <summary>截取自 <see cref="_allSectionCommands"/> 的前 N 项（深度 [1,5]）。</summary>
		private readonly string[] _sectionCommands;
		/// <summary>章节名是否走 LatexEscaper；用户可在 TEX.layout.escape_section_names 关闭。</summary>
		private readonly bool _escapeSectionNames;
		private readonly HashSet<string> _warnedExtensions = new(StringComparer.OrdinalIgnoreCase);

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
			INCLUDE_FILE_TYPES = _programConfigParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
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
		/// 非 . 前缀 / 字段空）；非法条目通过 ILogger 一次性 warn（与 _warnedExtensions 同风格）。
		/// </summary>
		private void ParseAndApplyOverride(string pair) {
			var idx = pair.IndexOf(':');
			if (idx <= 0 || idx >= pair.Length - 1) return; // 缺冒号或空字段，静默跳过
			var ext = pair[..idx].Trim();
			var lang = pair[(idx + 1)..].Trim();
			if (!ext.StartsWith('.')) {
				if (_warnedExtensions.Add($"override:{pair}")) {
					_logger.Warning($"code_language_overrides entry '{pair}' has no '.' prefix on extension. Skipping.");
				}
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
				_logger.Warning($"Source directory '{sourceDirInfo.FullName}' does not exist. Created the directory. Please add source files and rebuild.");
				return string.Empty;
			}

			var strBuilder = new StringBuilder();
			var lastIndex = _sectionCommands.Length - 1;
			foreach (var entry in SourceTreeWalker.Walk(sourceDirInfo, IGNORE_PATTERNS, _logger)) {
				// Round 3a: 深度超过章节层级时 clamp 到最后一层（不报错不跳过）。
				var effectiveDepth = entry.Depth >= _sectionCommands.Length ? lastIndex : entry.Depth;
				if (entry.IsDirectory) {
					InsertSection(strBuilder, entry.Info.Name, effectiveDepth);
					continue;
				}
				var codeBlock = GenerateCodeBlock_File((FileInfo)entry.Info);
				if (string.IsNullOrEmpty(codeBlock)) continue;
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
			var extension = codeFile.Extension.TrimStart('.').ToLowerInvariant();
			// 检查文件类型是否在包含列表中
			if (Array.IndexOf(INCLUDE_FILE_TYPES, "." + extension) == -1) {
				_logger.Warning($"File type '{extension}' is not in the include list. Skipping file '{codeFile.FullName}'.");
				return string.Empty;
			}
			var content = File.ReadAllText(codeFile.FullName);
			content = ExpandTabs(content);

			if (CODE_LANGUAGES_EXTENSIONS.Contains(extension)) {
				var codeBlock = new StringBuilder(CODE_BLOCK_TEMPLATE);
				var language = ResolveLanguage(extension, codeFile.Name);
				codeBlock.Replace("<<LANGUAGE>>", language);
				codeBlock.Replace("<<CODE>>", content);
				var rendered = codeBlock.ToString();
				foreach (var placeholder in TemplatePlaceholderScanner.FindUnresolved(rendered)) {
					_logger.Error($"Unresolved placeholder '{placeholder}' in CodeBlock.tex (file: {codeFile.Name}).");
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
					int spaces = _tabSize - (column % _tabSize);
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

		private string ResolveLanguage(string extension, string fileName) {
			if (_languageMap.TryGetValue(extension, out var mappedLanguage)) {
				return mappedLanguage;
			}
			if (_warnedExtensions.Add(extension)) {
				_logger.Warning($"File '{fileName}' uses extension '.{extension}' which has no dedicated listings language. Rendering as plain text.");
			}
			return DEFAULT_LANGUAGE;
		}

		/// <summary>
		/// 插入章节标题，根据深度选择合适的章节命令
		/// </summary>
		private void InsertSection(StringBuilder strBuilder, string sectionName, int depth) {
			if (depth < 0 || depth >= _sectionCommands.Length) {
				_logger.Error($"Invalid section depth: {depth}. Cannot insert section '{sectionName}'.");
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
