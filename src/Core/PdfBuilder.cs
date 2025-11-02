using System.Diagnostics;
using System.Text;
using Utils;

namespace Core {
	/// <summary>
	/// 构建最终pdf文件的类
	/// </summary>
	internal class PdfBuilder {
		private readonly ILogger _logger;
		private readonly IConfigParser _texConfigParser;
		private readonly IConfigParser _programConfigParser;


		public PdfBuilder(ILogger logger) {
			_logger = logger;
			_texConfigParser = new ConfigParser("TEX", logger);
			_programConfigParser = new ConfigParser("PROGRAM", logger);

			_texConfigParser.ParseConfigFile(File.ReadAllText(FilePaths.GetUserConfigFileInfo().FullName));
			_programConfigParser.ParseConfigFile(File.ReadAllText(FilePaths.GetUserConfigFileInfo().FullName));
		}

		public void Build() {
			_logger.Info("Build process started...");

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

			var outputFileName = _programConfigParser["OUTPUT_FILE_NAME"].GetAsString();

			const int requiredCompilations = 2;

			bool cleanupNeeded = true;

			for (int pass = 1; pass <= requiredCompilations; pass++) {
				_logger.Info($"Compilation pass #{pass}...");

				int exitCode = RunXelatex(outputDir, outputTexPath, outputFileName);
				if (exitCode != 0) {
					if (File.Exists(Path.Combine(outputDir, $"{outputFileName}.pdf"))) {
						_logger.Warning("xelatex returned a non-zero exit code, but the PDF was generated. Please check the compilation log for warnings or non-fatal errors.");
						cleanupNeeded = false; // 保留辅助文件以供调试
					} else {
						_logger.Error($"xelatex exited with code {exitCode}. LaTeX compilation failed.");
						return;
					}
				}
			}
			_logger.Info("LaTeX compilation completed successfully.");
			if (cleanupNeeded)
				CleanupAuxiliaryFiles(outputDir, outputFileName);
		}

		private int RunXelatex(string outputDir, string outputTexPath, string outputFileName) {
			StringBuilder arguments = new();
			arguments.Append("-interaction=nonstopmode ");
			arguments.Append($"-jobname={outputFileName} ");
			arguments.Append($"-output-directory \"{outputDir}\" ");
			arguments.Append($"\"{outputTexPath}\"");

			using var xelatex = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "xelatex",
					Arguments = arguments.ToString(),
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = outputDir
				},
			};

			xelatex.OutputDataReceived += (_, args) => {
				if (args.Data != null) {
					_logger.Debug(args.Data);
				}
			};

			xelatex.ErrorDataReceived += (_, args) => {
				if (args.Data != null) {
					_logger.Error(args.Data);
				}
			};

			if (!xelatex.Start()) {
				_logger.Error("Failed to start xelatex process.");
				return -1;
			}

			xelatex.BeginOutputReadLine();
			xelatex.BeginErrorReadLine();

			xelatex.WaitForExit();

			return xelatex.ExitCode;
		}

		/// <summary>
		/// 清理辅助文件
		/// </summary>
		private void CleanupAuxiliaryFiles(string outputDir, string baseName) {
			var extensionsToDelete = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };

			foreach (var ext in extensionsToDelete) {
				var filePath = Path.Combine(outputDir, baseName + ext);
				_logger.Debug($"Attempting to delete auxiliary file: {filePath}");
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
			try {
				var configJson = File.ReadAllText(FilePaths.GetUserConfigFileInfo().FullName);
				_texConfigParser.ParseConfigFile(configJson ?? string.Empty);
			} catch (Exception ex) {
				_logger.Error($"Failed to load user configuration: {ex.Message}");
			}
		}

		/// <summary>
		/// 生成 TeX 正文内容
		/// </summary>
		private StringBuilder GenerateTexContent(ManifestResourceManager resMgr) {
			var mainTemplate = new StringBuilder(resMgr.GetResourceInString("Templates.Main.tex"));
			ReplaceMainPlaceholders(mainTemplate);

			var codeBlocks = new CodeBlockGenerator(_logger, _programConfigParser).Generate();

			// 插入正文内容，生成最终的 TeX 内容
			mainTemplate.Replace("##CONTENT##", codeBlocks);
			return mainTemplate;
		}

		/// <summary>
		/// 替换 MainTeX 模板中的占位符
		/// </summary>
		/// <param name="content">要替换的模板内容</param>
		private void ReplaceMainPlaceholders(StringBuilder content) {
			foreach (var kvp in _texConfigParser.GetAllConfigsAsString()) {
				var placeholder = $"##{kvp.Key}##";
				content.Replace(placeholder, kvp.Value);
			}
		}
	}
}