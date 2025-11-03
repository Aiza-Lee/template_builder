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

			_texConfigParser.ParseConfigFile(File.ReadAllText(CommandInfoHelper.ConfigurationFileInfo.FullName));
			_programConfigParser.ParseConfigFile(File.ReadAllText(CommandInfoHelper.ConfigurationFileInfo.FullName));
		}

		/// <summary>
		/// 执行构建命令。生成tex文件内容，并编译为pdf，输出到配置中的路径。
		/// </summary>
		public void Build() {
			_logger.Info("Build process started...");
			// 加载用户配置
			LoadUserConfig();

			// 生成 TeX 正文内容
			string texContent = GenerateTexContent().ToString();

			// 保存 TeX 文件
			var midTexFileInfo = SaveTexFile(texContent);

			// 编译 TeX 文件为 PDF
			CompileTexToPdf(midTexFileInfo);
		}

		private void CompileTexToPdf(FileInfo midTexFileInfo) {
			_logger.Info("Starting LaTeX compilation...");

			const int requiredCompilations = 2;

			bool cleanupNeeded = true;

			for (int pass = 1; pass <= requiredCompilations; pass++) {
				_logger.Info($"Compilation pass #{pass}...");

				int exitCode = RunXelatex(midTexFileInfo);
				if (exitCode != 0) {
					if (CommandInfoHelper.OutputFileInfo.Exists) {
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
				CleanupAuxiliaryFiles();
		}

		private int RunXelatex(FileInfo midTexFileInfo) {

			StringBuilder arguments = new();
			arguments.Append("-interaction=nonstopmode ");
			arguments.Append($"-jobname={Path.GetFileNameWithoutExtension(CommandInfoHelper.OutputFileInfo.Name)} ");
			arguments.Append($"-output-directory \"{CommandInfoHelper.OutputFileInfo.Directory!.FullName}\" ");
			arguments.Append($"\"{midTexFileInfo.FullName}\"");

			using var xelatex = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "xelatex",
					Arguments = arguments.ToString(),
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = AppContext.BaseDirectory
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
		private void CleanupAuxiliaryFiles() {
			var extensionsToDelete = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };

			var baseName = Path.GetFileNameWithoutExtension(CommandInfoHelper.OutputFileInfo.Name);
			var outputDir = CommandInfoHelper.OutputFileInfo.Directory!.FullName;

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
		private static FileInfo SaveTexFile(string texContent) {
			var outputDir = CommandInfoHelper.OutputFileInfo.Directory!;
			var fileInfo = new FileInfo(Path.Combine(outputDir.FullName, "mid-output.tex"));
			File.WriteAllText(fileInfo.FullName, texContent);
			return fileInfo;
		}

		/// <summary>
		/// 加载用户配置
		/// </summary>
		private void LoadUserConfig() {
			try {
				var configJson = File.ReadAllText(CommandInfoHelper.ConfigurationFileInfo.FullName);
				_texConfigParser.ParseConfigFile(configJson ?? string.Empty);
			} catch (Exception ex) {
				_logger.Error($"Failed to load user configuration: {ex.Message}");
			}
		}

		/// <summary>
		/// 生成 TeX 正文内容
		/// </summary>
		private StringBuilder GenerateTexContent() {
			var resMgr = new ManifestResourceManager(_logger);
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