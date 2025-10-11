using System.Diagnostics;
using System.Text;
using Utils;

namespace Core {
	internal class PdfBuilder {
		private readonly ILogger _logger;
		private readonly IConfigParser _texConfigParser;
		private readonly IConfigParser _programConfigParser;

		private string _codeBlockTemplate = string.Empty;
		private string[] _includeFileTypes = [];

		private static readonly string[] SubDirectoryName = [
			"Section", "Subsection", "Subsubsection",
			"Paragraph", "Subparagraph"
		];

		public PdfBuilder(ILogger logger) {
			_logger = logger;
			_texConfigParser = new ConfigParser("TEX", logger);
			_programConfigParser = new ConfigParser("PROGRAM", logger);
		}

		public void Build() {
			var resMgr = new ManifestResourceManager(_logger);

			// 加载用户配置
			LoadUserConfig(resMgr);

			// 生成 TeX 正文内容
			string texContent = GenerateTexContent(resMgr).ToString();

			// 保存 TeX 文件
			SaveTexFile(texContent, out string outputDir, out string outputTexPath);

			// 编译 TeX 文件为 PDF
			CompileTexToPdf(outputDir, outputTexPath);
		}

		private void CompileTexToPdf(string outputDir, string outputTexPath) {
			_logger.Info("Starting LaTeX compilation...");

			const int maxRetries = 2;

			for (int attempt = 1; attempt <= maxRetries; attempt++) {
				_logger.Info($"Compilation attempt #{attempt}...");


			}
		}
		private void RunXelatex(string outputDir, string outputTexPath) {
			_logger.Info("Running xelatex...");

			using var xelatex = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "xelatex",
					Arguments = $"-interaction=nonstopmode -output-directory \"{outputDir}\" \"{outputTexPath}\"",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = outputDir
				}
			};

			var outputLines = new List<string>();
			var errorLines = new List<string>();

			xelatex.OutputDataReceived += (_, args) => {
				if (args.Data != null) {
					outputLines.Add(args.Data);
					_logger.Debug(args.Data);
				}
			};

			xelatex.ErrorDataReceived += (_, args) => {
				if (args.Data != null) {
					errorLines.Add(args.Data);
					_logger.Error(args.Data);
				}
			};

			if (!xelatex.Start()) {
				_logger.Error("Failed to start xelatex process.");
				return;
			}

			xelatex.BeginOutputReadLine();
			xelatex.BeginErrorReadLine();

			xelatex.WaitForExit();
		}

	private void CleanupAuxiliaryFiles(string outputDir, string baseName) {
		var extensionsToDelete = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };
		
		foreach (var ext in extensionsToDelete) {
			var filePath = Path.Combine(outputDir, baseName + ext);
			if (File.Exists(filePath)) {
				try {
					File.Delete(filePath);
					_logger.Debug($"Deleted auxiliary file: {filePath}");
				} catch (Exception ex) {
					_logger.Warning($"Failed to delete {filePath}: {ex.Message}");
				}
			}
		}
	}

		/// <summary>
		/// 保存 TeX 文件
		/// </summary>
		private void SaveTexFile(string texContent, out string outputDir, out string outputTexPath) {
			// 输出 TeX 文件
			outputDir = FilePaths.GetConfigInPath(_programConfigParser, "BUILD_DIRECTORY");
			Directory.CreateDirectory(outputDir);
			outputTexPath = Path.Combine(outputDir, "mid-output.tex");
			File.WriteAllText(outputTexPath, texContent);
		}

		/// <summary>
		/// 加载用户配置
		/// </summary>
		private void LoadUserConfig(ManifestResourceManager resMgr) {
			var configJson = resMgr.GetContentInString(FilePaths.GetUserConfigFileInfo());
			_texConfigParser.ParseConfigFile(configJson ?? string.Empty);
			_codeBlockTemplate = resMgr.GetResourceInString("Templates.CodeBlock.tex");
			_includeFileTypes = _programConfigParser.QueryConfig("INCLUDE_FILE_TYPES");
		}

		/// <summary>
		/// 生成 TeX 正文内容
		/// </summary>
		private StringBuilder GenerateTexContent(ManifestResourceManager resMgr) {
			var mainTemplate = new StringBuilder(resMgr.GetResourceInString("Templates.Main.tex"));
			ReplaceMainPlaceholders(mainTemplate);

			var codeSourceDir = FilePaths.GetConfigInPath(_programConfigParser, "CODE_SOURCE_DIRECTORY");
			var codeBlocks = GenerateCodeBlocks(new(codeSourceDir));

			// 插入正文内容，生成最终的 TeX 内容
			mainTemplate.Replace("##CONTENT##", codeBlocks);
			return mainTemplate;
		}

		/// <summary>
		/// 替换 MainTeX 模板中的占位符
		/// </summary>
		/// <param name="content">要替换的模板内容</param>
		private void ReplaceMainPlaceholders(StringBuilder content) {
			foreach (var kvp in _texConfigParser.GetAllConfigs()) {
				var placeholder = $"##{kvp.Key}##";
				content.Replace(placeholder, kvp.Value[0]);
			}
		}

		private string GenerateCodeBlocks(DirectoryInfo codeSourceDir) {
			return GenerateCodeBlock_Directory(new(), codeSourceDir).ToString();
		}

		private string GenerateCodeBlock_File(FileInfo codeFile) {
			var language = codeFile.Extension.TrimStart('.').ToLower();
			// 检查文件类型是否在包含列表中
			if (Array.IndexOf(_includeFileTypes, language) == -1) {
				_logger.Warning($"File type '{language}' is not in the include list. Skipping file '{codeFile.FullName}'.");
				return string.Empty;
			}
			var codeContent = File.ReadAllText(codeFile.FullName);
			var codeBlock = new StringBuilder(_codeBlockTemplate);
			codeBlock.Replace("##LANGUAGE##", language);
			codeBlock.Replace("##CODE##", codeContent);
			return codeBlock.ToString();
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