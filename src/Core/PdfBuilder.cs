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
		private readonly StringBuilder _xelatexStderr = new();


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
			_xelatexStderr.Clear();

			for (int pass = 1; pass <= requiredCompilations; pass++) {
				_logger.Info($"Compilation pass #{pass}...");

				int exitCode = RunXelatex(midTexFileInfo);
				if (exitCode != 0) {
					if (CommandInfoHelper.OutputFileInfo.Exists) {
						_logger.Warning("xelatex returned a non-zero exit code, but the PDF was generated. Please check the compilation log for warnings or non-fatal errors.");
						cleanupNeeded = false; // 保留辅助文件以供调试
					} else {
						_logger.Error($"xelatex exited with code {exitCode}. LaTeX compilation failed.");
						FlushStderrAsError();
						return;
					}
				}
			}
			_logger.Info("LaTeX compilation completed successfully.");
			if (cleanupNeeded)
				CleanupAuxiliaryFiles();
		}

		/// <summary>
		/// 编译失败时把累积的 stderr 一次性以 Error 级别输出。
		/// </summary>
		private void FlushStderrAsError() {
			if (_xelatexStderr.Length == 0) return;
			_logger.Error("--- xelatex stderr ---");
			foreach (var line in _xelatexStderr.ToString().Split('\n')) {
				var trimmed = line.TrimEnd('\r');
				if (trimmed.Length > 0) {
					_logger.Error(trimmed);
				}
			}
		}

		private int RunXelatex(FileInfo midTexFileInfo) {

			StringBuilder arguments = new();
			// arguments.Append("-8bit ");
			arguments.Append("-shell-escape ");
			arguments.Append("-interaction=nonstopmode ");
			arguments.Append($"-jobname={Path.GetFileNameWithoutExtension(CommandInfoHelper.OutputFileInfo.Name)} ");
			arguments.Append($"-output-directory \"{CommandInfoHelper.OutputFileInfo.Directory!.FullName}\" ");
			arguments.Append($"\"{midTexFileInfo.FullName}\"");

			// 把 stderr 累积到缓存中，避免 minted / hyperref 的无害提示刷屏；
			// 仅在编译失败且未生成 PDF 时再把它作为 Error 一次性输出。
			var stderrBuffer = new StringBuilder();

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
					// 先按 Debug 缓冲，便于 -v 时观察；调用方在编译失败时再决定是否升级
					_logger.Debug(args.Data);
					stderrBuffer.AppendLine(args.Data);
				}
			};

			if (!xelatex.Start()) {
				_logger.Error("Failed to start xelatex process.");
				return -1;
			}

			xelatex.BeginOutputReadLine();
			xelatex.BeginErrorReadLine();

			xelatex.WaitForExit();

			// 把缓存的 stderr 存到一个可以跨 pass 累积的位置（通过实例字段）
			_xelatexStderr.Append(stderrBuffer);
			return xelatex.ExitCode;
		}

		/// <summary>
		/// 清理辅助文件
		/// </summary>
		private void CleanupAuxiliaryFiles() {
			var baseName = Path.GetFileNameWithoutExtension(CommandInfoHelper.OutputFileInfo.Name);
			var outputDir = CommandInfoHelper.OutputFileInfo.Directory!.FullName;
			Cleanup(outputDir, baseName, _logger);
		}

		/// <summary>
		/// 清理 LaTeX 编译产生的中间文件。提取为 internal static 以便测试。
		/// </summary>
		internal static void Cleanup(string outputDir, string baseName, ILogger logger) {
			var extensionsToDelete = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };

			foreach (var ext in extensionsToDelete) {
				TryDelete(Path.Combine(outputDir, baseName + ext), logger);
			}
			// mid-output.tex 是本工具生成的中间文件，按 jobname 无关的固定名存放
			TryDelete(Path.Combine(outputDir, "mid-output.tex"), logger);
		}

		private static void TryDelete(string filePath, ILogger logger) {
			logger.Debug($"Attempting to delete auxiliary file: {filePath}");
			if (File.Exists(filePath)) {
				try {
					File.Delete(filePath);
					logger.Debug($"Deleted auxiliary file: {filePath}");
				} catch (Exception ex) {
					logger.Warning($"Failed to delete {filePath}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 保存 TeX 文件
		/// </summary>
		private FileInfo SaveTexFile(string texContent) {
			return new FileInfo(SaveTexFile(texContent, CommandInfoHelper.OutputFileInfo.Directory!.FullName));
		}

		/// <summary>
		/// 写出 TeX 文件到指定目录，返回写入文件的绝对路径。提取为 internal static 以便测试。
		/// </summary>
		internal static string SaveTexFile(string texContent, string outputDir) {
			var filePath = Path.Combine(outputDir, "mid-output.tex");
			File.WriteAllText(filePath, texContent);
			return filePath;
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
	
			// 设置 minted 的输出目录
			var outputDir = CommandInfoHelper.OutputFileInfo.Directory!.FullName.Replace("\\", "/");
			mainTemplate.Replace("<<MINTED_OUTPUTDIR>>", outputDir);

			ReplaceMainPlaceholders(mainTemplate);

			int tabSize = _texConfigParser["CODE_TAB_SIZE"].GetAsInt();
			var codeBlocks = new CodeBlockGenerator(_logger, _programConfigParser, tabSize).Generate();

			// 插入正文内容，生成最终的 TeX 内容
			mainTemplate.Replace("<<CONTENT>>", codeBlocks);
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