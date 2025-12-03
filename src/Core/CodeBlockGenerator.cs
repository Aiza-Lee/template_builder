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

		private readonly string CODE_BLOCK_TEMPLATE = string.Empty;
		private readonly string[] INCLUDE_FILE_TYPES = [];
		private readonly string[] SUB_DIRECTORY_NAMES = [
			"section", "subsection", "subsubsection",
			"paragraph", "subparagraph"
		];
		/// <summary>
		/// 作为代码块处理的文件扩展名列表
		/// </summary>
		private readonly HashSet<string> CODE_LANGUAGES_EXTENSIONS = [
			"cpp", "c", "hpp", "h", "cs", "rs", "ts", "js", "java",
			"py", "rb", "go", "php", "html", "css", "xml", "json",
			"sh", "bat", "ps1", "swift", "kt", "m", "sql", "yaml", "yml"
		];
		/// <summary>
		/// 文件扩展名到Listings语言的映射
		/// </summary>
		private static readonly IReadOnlyDictionary<string, string> EXTENSION_TO_LANGUAGE =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
				{ "c", "C" }, { "cpp", "C++" }, { "h", "C" }, { "hpp", "C++" },
				{ "cs", "[Sharp]C" }, { "java", "Java" }, { "py", "Python" },
				{ "rb", "Ruby" }, { "go", "Go" }, { "php", "PHP" }, { "html", "HTML" },
				{ "css", "CSS" }, { "xml", "XML" }, { "json", "JSON" }, { "js", "JavaScript" },
				{ "ts", "TypeScript" }, { "sh", "bash" }, { "bat", "PlainText" },
				{ "ps1", "PowerShell" }, { "swift", "Swift" }, { "kt", "Kotlin" },
				{ "rs", "Rust" }, { "m", "[Objective]C" }, { "sql", "SQL" },
				{ "yaml", "YAML" }, { "yml", "YAML" }, { "tex", "[LaTeX]TeX" },
				{ "txt", "PlainText" }
			};
		private const string DEFAULT_LANGUAGE = "PlainText";
		private readonly HashSet<string> _warnedExtensions = new(StringComparer.OrdinalIgnoreCase);
		private static readonly IReadOnlyDictionary<char, string> LATEX_ESCAPES = new Dictionary<char, string> {
			{ '\\', @"\textbackslash{}" },
			{ '{', @"\{" }, { '}', @"\}" },
			{ '#', @"\#" }, { '$', @"\$" },
			{ '%', @"\%" }, { '&', @"\&" },
			{ '_', @"\_" }, { '^', @"\^{}" },
			{ '~', @"\textasciitilde{}" }
		};

		public CodeBlockGenerator(ILogger logger, IConfigParser programConfigParser) {
			_logger = logger;
			_sourceDirInfo = CommandInfoHelper.SourceFilesDirectoryInfo;
			_programConfigParser = programConfigParser;

			var resMgr = new ManifestResourceManager(_logger);

			CODE_BLOCK_TEMPLATE = resMgr.GetResourceInString("Templates.CodeBlock.tex");
			INCLUDE_FILE_TYPES = _programConfigParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
		}


		/// <summary>
		/// 生成代码块TeX
		/// </summary>
		/// <returns></returns>
		public string Generate() {
			var sourceDirInfo = CommandInfoHelper.SourceFilesDirectoryInfo;
			if (!sourceDirInfo.Exists) {
				Directory.CreateDirectory(sourceDirInfo.FullName);
				_logger.Warning($"Source directory '{sourceDirInfo.FullName}' does not exist. Created the directory. Please add source files and rebuild.");
				return string.Empty;
			}
			return GenerateCodeBlock_Directory(new(), sourceDirInfo).ToString();
		}

		/// <summary>
		/// 递归生成目录下所有代码文件的代码块TeX
		/// </summary>
		/// <param name="strBuilder">字符串构建器</param>
		/// <param name="codeDir">目录信息</param>
		/// <param name="depth">当前深度</param>
		/// <returns>返回生成的TeX代码（StringBuilder）</returns>
		private StringBuilder GenerateCodeBlock_Directory(
			StringBuilder strBuilder,
			DirectoryInfo codeDir,
			int depth = 0
		) {
			if (depth >= SUB_DIRECTORY_NAMES.Length) {
				_logger.Warning($"Directory nesting exceeds supported depth at '{codeDir.FullName}'. Skipping deeper levels.");
				return strBuilder;
			}

			// 处理子目录
			foreach (var subDir in codeDir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)) {
				InsertSection(strBuilder, subDir.Name, depth);
				GenerateCodeBlock_Directory(strBuilder, subDir, depth + 1);
			}
			// 处理当前目录下的文件
			foreach (var codeFile in codeDir.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) {
				var codeBlock = GenerateCodeBlock_File(codeFile);
				// 如果不在包含的文件类型列表中，则跳过
				if (!string.IsNullOrEmpty(codeBlock)) {
					InsertSection(strBuilder, codeFile.Name, depth);
					strBuilder.AppendLine(codeBlock);
				}
			}
			return strBuilder;
		}

		/// <summary>
		/// 生成单个代码文件的代码块TeX
		/// </summary>
		/// <param name="codeFile">代码文件信息</param>
		/// <returns>返回生成的tex代码</returns>
		private string GenerateCodeBlock_File(FileInfo codeFile) {
			var extension = codeFile.Extension.TrimStart('.').ToLowerInvariant();
			// _logger.Info($"Processing file: {codeFile.FullName} with language: {language}");
			// _logger.Info($"Included file types: {string.Join(", ", _includeFileTypes)}");
			// 检查文件类型是否在包含列表中
			if (Array.IndexOf(INCLUDE_FILE_TYPES, "." + extension) == -1) {
				_logger.Warning($"File type '{extension}' is not in the include list. Skipping file '{codeFile.FullName}'.");
				return string.Empty;
			}
			var content = File.ReadAllText(codeFile.FullName);

			if (CODE_LANGUAGES_EXTENSIONS.Contains(extension)) {
				var codeBlock = new StringBuilder(CODE_BLOCK_TEMPLATE);
				var listingsLanguage = ResolveListingsLanguage(extension, codeFile.Name);
				codeBlock.Replace("##LANGUAGE_DIRECTIVE##", BuildLanguageDirective(listingsLanguage));
				codeBlock.Replace("##CODE##", content);
				return codeBlock.ToString();
			} else {
				return content;
			}
		}

		private string ResolveListingsLanguage(string extension, string fileName) {
			if (EXTENSION_TO_LANGUAGE.TryGetValue(extension, out var mappedLanguage)) {
				return mappedLanguage;
			}
			if (_warnedExtensions.Add(extension)) {
				_logger.Warning($"File '{fileName}' uses extension '.{extension}' which has no dedicated listings language. Rendering as plain text.");
			}
			return DEFAULT_LANGUAGE;
		}

		private static string BuildLanguageDirective(string listingsLanguage) {
			return $@"\lstset{{language={listingsLanguage}}}";
		}

		/// <summary>
		/// 插入章节标题，根据深度选择合适的章节命令
		/// </summary>
		private void InsertSection(StringBuilder strBuilder, string sectionName, int depth) {
			if (depth < 0 || depth >= SUB_DIRECTORY_NAMES.Length) {
				_logger.Error($"Invalid section depth: {depth}. Cannot insert section '{sectionName}'.");
				return;
			}
			strBuilder.AppendLine($"\\{SUB_DIRECTORY_NAMES[depth]}{{{EscapeLatexText(sectionName)}}}");
			if (depth >= 3) {
				// 段落和子段落作为标题使用，后添加空行以增加可读性
				strBuilder.AppendLine(@"\textbf{ } \\");
			}
		}

		/// <summary>
		/// 转义LaTeX特殊字符
		/// </summary>
		/// <param name="text">输入的文本字符串</param>
		/// <returns>转义后的LaTeX安全字符串</returns>
		private static string EscapeLatexText(string text) {
			if (string.IsNullOrEmpty(text)) {
				return string.Empty;
			}
			var sb = new StringBuilder(text.Length);
			foreach (var ch in text) {
				if (LATEX_ESCAPES.TryGetValue(ch, out var replacement)) {
					sb.Append(replacement);
				} else if (char.IsControl(ch)) {
					continue;
				} else {
					sb.Append(ch);
				}
			}
			return sb.ToString();
		}
	}
}
