using System.Text;
using Utils;

namespace Core {
	/// <summary>
	/// 生成代码块TeX的类
	/// </summary>
	internal class CodeBlockGenerator {
		private readonly ILogger _logger;
		private readonly DirectoryInfo _sourceDir;
		private readonly IConfigParser _programConfigParser;

		private readonly string CODE_BLOCK_TEMPLATE = string.Empty;
		private readonly string[] INCLUDE_FILE_TYPES = [];
		private readonly string[] SUB_DIRECTORY_NAMES = [
			"section", "subsection", "subsubsection",
			"paragraph", "subparagraph"
		];
		private readonly HashSet<string> CODE_LANGUAGES_EXTENSIONS = [
			"cpp", "c", "hpp", "h", "cs", "rs", "ts", "js", "java",
			"py", "rb", "go", "php", "html", "css", "xml", "json",
			"sh", "bat", "ps1", "swift", "kt", "m", "sql", "yaml", "yml"
		];
		private readonly Dictionary<string, string> EXTENSION_TO_LANGUAGE = new() {
			{ "cpp", "c++" }, { "hpp", "c++" }, { "h", "c" },
			{ "cs", "c#" }, { "rs", "rust" }, { "ts", "typescript" },
			{ "js", "javascript" }, { "py", "python" }, { "rb", "ruby" },
			{ "go", "go" }, { "php", "php" }, { "html", "html" },
			{ "css", "css" }, { "xml", "xml" }, { "json", "json" },
			{ "sh", "bash" }, { "bat", "batch" }, { "ps1", "powershell" },
			{ "swift", "swift" }, { "kt", "kotlin" }, { "m", "objective-c" },
			{ "sql", "sql" }, { "yaml", "yaml" }, { "yml", "yaml" }
		};

		public CodeBlockGenerator(ILogger logger, IConfigParser programConfigParser) {
			_logger = logger;
			_sourceDir = new(FilePaths.GetConfigInPath(programConfigParser, "CODE_SOURCE_DIRECTORY"));
			_programConfigParser = programConfigParser;

			var resMgr = new ManifestResourceManager(_logger);

			CODE_BLOCK_TEMPLATE = resMgr.GetResourceInString("Templates.CodeBlock.tex");
			INCLUDE_FILE_TYPES = _programConfigParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
		}


		public string Generate() {
			if (!_sourceDir.Exists) {
				Directory.CreateDirectory(_sourceDir.FullName);
				_logger.Warning($"Source directory '{_sourceDir.FullName}' does not exist. Created the directory. Please add source files and rebuild.");
				return string.Empty;
			}
			return GenerateCodeBlock_Directory(new(), _sourceDir).ToString();
		}

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
			foreach (var subDir in codeDir.GetDirectories()) {
				InsertSection(strBuilder, subDir.Name, depth);
				GenerateCodeBlock_Directory(strBuilder, subDir, depth + 1);
			}
			// 处理当前目录下的文件
			foreach (var codeFile in codeDir.GetFiles()) {
				var codeBlock = GenerateCodeBlock_File(codeFile);
				// 如果不在包含的文件类型列表中，则跳过
				if (!string.IsNullOrEmpty(codeBlock)) {
					InsertSection(strBuilder, codeFile.Name, depth);
					strBuilder.AppendLine(codeBlock);
				}
			}
			return strBuilder;
		}

		private string GenerateCodeBlock_File(FileInfo codeFile) {
			var language = codeFile.Extension.TrimStart('.').ToLower();
			// _logger.Info($"Processing file: {codeFile.FullName} with language: {language}");
			// _logger.Info($"Included file types: {string.Join(", ", _includeFileTypes)}");
			// 检查文件类型是否在包含列表中
			if (Array.IndexOf(INCLUDE_FILE_TYPES, "." + language) == -1) {
				_logger.Warning($"File type '{language}' is not in the include list. Skipping file '{codeFile.FullName}'.");
				return string.Empty;
			}
			var content = File.ReadAllText(codeFile.FullName);

			if (CODE_LANGUAGES_EXTENSIONS.Contains(language)) {
				var codeBlock = new StringBuilder(CODE_BLOCK_TEMPLATE);
				if (EXTENSION_TO_LANGUAGE.TryGetValue(language, out string? value)) {
					language = value;
				}
				codeBlock.Replace("##LANGUAGE##", language);
				codeBlock.Replace("##CODE##", content);
				return codeBlock.ToString();
			} else {
				return content;
			}
		}

		/// <summary>
		/// 插入章节标题
		/// </summary>
		private void InsertSection(StringBuilder strBuilder, string sectionName, int depth) {
			if (depth < 0 || depth >= SUB_DIRECTORY_NAMES.Length) {
				_logger.Error($"Invalid section depth: {depth}. Cannot insert section '{sectionName}'.");
				return;
			}
			strBuilder.AppendLine($"\\{SUB_DIRECTORY_NAMES[depth]}{{{sectionName}}}");
			if (depth >= 3) {
				// 段落和子段落作为标题使用，后添加空行以增加可读性
				strBuilder.AppendLine(@"\textbf{ } \\");
			}
		}
	}
}