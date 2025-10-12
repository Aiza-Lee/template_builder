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

		private string _codeBlockTemplate = string.Empty;
		private string[] _includeFileTypes = [];
		private static readonly string[] SubDirectoryName = [
			"section", "subsection", "subsubsection",
			"paragraph", "subparagraph"
		];

		public CodeBlockGenerator(ILogger logger, IConfigParser programConfigParser) {
			_logger = logger;
			_sourceDir = new(FilePaths.GetConfigInPath(programConfigParser, "CODE_SOURCE_DIRECTORY"));
			_programConfigParser = programConfigParser;

			var resMgr = new ManifestResourceManager(_logger);

			_codeBlockTemplate = resMgr.GetResourceInString("Templates.CodeBlock.tex");
			_includeFileTypes = _programConfigParser.QueryConfig("INCLUDE_FILE_TYPES");
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
			if (depth >= SubDirectoryName.Length) {
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
			// 检查文件类型是否在包含列表中
			if (Array.IndexOf(_includeFileTypes, "." + language) == -1) {
				_logger.Warning($"File type '{language}' is not in the include list. Skipping file '{codeFile.FullName}'.");
				return string.Empty;
			}
			var codeContent = File.ReadAllText(codeFile.FullName);
			var codeBlock = new StringBuilder(_codeBlockTemplate);

			// note: cpp -> c++
			if (language == "cpp") { language = "c++"; }

			codeBlock.Replace("##LANGUAGE##", language);
			codeBlock.Replace("##CODE##", codeContent);
			return codeBlock.ToString();
		}

		/// <summary>
		/// 插入章节标题
		/// </summary>
		private void InsertSection(StringBuilder strBuilder, string sectionName, int depth) {
			if (depth < 0 || depth >= SubDirectoryName.Length) {
				_logger.Error($"Invalid section depth: {depth}. Cannot insert section '{sectionName}'.");
				return;
			}
			strBuilder.AppendLine($"\\{SubDirectoryName[depth]}{{{sectionName}}}");
		}
	}
}