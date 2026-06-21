using System.Diagnostics;
using System.Text;
using Utils;

namespace Core {
	/// <summary>
	/// 构建最终pdf文件的类
	/// </summary>
	internal class PdfBuilder {
		private readonly ILogger _logger;
		private readonly BuildSubcommandOptions _options;
		private readonly IConfigParser _texConfigParser;
		private readonly IConfigParser _programConfigParser;
		private readonly ManifestResourceManager _resMgr;
		private readonly StringBuilder _xelatexStderr = new();
		private int _unresolvedPlaceholderCount;

		/// <summary>
		/// 退出码常量转发到 <see cref="ExitCodes"/>，保留旧名以兼容既有调用方。
		/// </summary>
		public const int ExitSuccess = ExitCodes.Success;
		public const int ExitXelatexFailure = ExitCodes.XelatexFailure;
		public const int ExitUnresolvedPlaceholders = ExitCodes.UnresolvedPlaceholders;

		public PdfBuilder(
			ILogger logger,
			BuildSubcommandOptions options,
			IConfigParser texConfigParser,
			IConfigParser programConfigParser,
			ManifestResourceManager resMgr
		) {
			_logger = logger;
			_options = options;
			_texConfigParser = texConfigParser;
			_programConfigParser = programConfigParser;
			_resMgr = resMgr;
		}

		/// <summary>
		/// 执行构建命令。生成tex文件内容，并编译为pdf，输出到配置中的路径。
		/// </summary>
		/// <returns>退出码（0 成功；1 xelatex 失败；3 模板存在未替换占位符）</returns>
		public int Build() {
			_logger.Info("Build process started...");
			_unresolvedPlaceholderCount = 0;

			// 生成 TeX 正文内容
			var mainTemplate = GenerateTexContent();
			if (_unresolvedPlaceholderCount > 0) {
				return ExitUnresolvedPlaceholders;
			}

			// 保存 TeX 文件
			var midTexFileInfo = SaveTexFile(mainTemplate.ToString());

			// 编译 TeX 文件为 PDF
			return CompileTexToPdf(midTexFileInfo);
		}

		private int CompileTexToPdf(FileInfo midTexFileInfo) {
			_logger.Info("Starting LaTeX compilation...");

			const int requiredCompilations = 2;

			bool cleanupNeeded = true;

			for (int pass = 1; pass <= requiredCompilations; pass++) {
				_logger.Info($"Compilation pass #{pass}...");

				// 每次 pass 清空 buffer，使最终 dump 时只看到失败 pass 的 stderr。
				_xelatexStderr.Clear();

				int exitCode = RunXelatex(midTexFileInfo, pass);
				if (exitCode != 0) {
					if (_options.OutputPdf.Exists) {
						_logger.Warning("xelatex returned a non-zero exit code, but the PDF was generated. Please check the compilation log for warnings or non-fatal errors.");
						cleanupNeeded = false; // 保留辅助文件以供调试
					} else {
						_logger.Error($"xelatex exited with code {exitCode}. LaTeX compilation failed.");
						FlushStderrAsError();
						return ExitXelatexFailure;
					}
				}
			}
			_logger.Info("LaTeX compilation completed successfully.");
			if (cleanupNeeded)
				CleanupAuxiliaryFiles();
			return ExitSuccess;
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

		private int RunXelatex(FileInfo midTexFileInfo, int pass) {

			StringBuilder arguments = new();
			// arguments.Append("-8bit ");
			arguments.Append("-shell-escape ");
			arguments.Append("-interaction=nonstopmode ");
			arguments.Append($"-jobname={Path.GetFileNameWithoutExtension(_options.OutputPdf.Name)} ");
			arguments.Append($"-output-directory \"{_options.OutputPdf.Directory!.FullName}\" ");
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

			// 给本 pass 的 stderr 打标签，便于在 dump 时区分归属。
			AppendLabeledStderr(_xelatexStderr, pass, stderrBuffer.ToString());
			return xelatex.ExitCode;
		}

		/// <summary>
		/// 给一次 xelatex pass 的 stderr 打上 `--- pass N stderr ---` 标签并追加到目标 buffer。提取为 internal static 便于测试。
		/// </summary>
		internal static void AppendLabeledStderr(StringBuilder buffer, int pass, string stderr) {
			buffer.AppendLine($"--- pass {pass} stderr ---");
			buffer.Append(stderr);
		}

		/// <summary>
		/// 清理辅助文件
		/// </summary>
		private void CleanupAuxiliaryFiles() {
			var baseName = Path.GetFileNameWithoutExtension(_options.OutputPdf.Name);
			var outputDir = _options.OutputPdf.Directory!.FullName;
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
			return new FileInfo(SaveTexFile(texContent, _options.OutputPdf.Directory!.FullName));
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
		/// 生成 TeX 正文内容
		/// </summary>
		private StringBuilder GenerateTexContent() {
			string mainTemplateContent = ResolveTemplateContent("Main.tex");
			var mainTemplate = new StringBuilder(mainTemplateContent);

			// 设置 minted 的输出目录
			var outputDir = _options.OutputPdf.Directory!.FullName.Replace("\\", "/");
			mainTemplate.Replace("<<MINTED_OUTPUTDIR>>", outputDir);

			ReplaceMainPlaceholders(mainTemplate);

			// 在 <<CONTENT>> 替换前扫描 Main.tex，避免误报尚未替换的 <<CONTENT>> 标记。
			foreach (var placeholder in TemplatePlaceholderScanner.FindUnresolved(mainTemplate.ToString())) {
				if (placeholder == "<<CONTENT>>") continue;
				_logger.Error($"Unresolved placeholder '{placeholder}' in Main.tex.");
				_unresolvedPlaceholderCount++;
			}

			int tabSize = _texConfigParser["CODE_TAB_SIZE"].GetAsInt();
			string codeBlockTemplateContent = ResolveTemplateContent("CodeBlock.tex");
			var codeBlockGen = new CodeBlockGenerator(_logger, _programConfigParser, tabSize, _options.SourceDir, codeBlockTemplateContent);
			string codeBlocks = codeBlockGen.Generate();
			_unresolvedPlaceholderCount += codeBlockGen.UnresolvedPlaceholderCount;

			// 插入正文内容，生成最终的 TeX 内容
			mainTemplate.Replace("<<CONTENT>>", codeBlocks);

			return mainTemplate;
		}

		/// <summary>
		/// 解析模板内容：优先使用 TemplateDir 下的文件，否则落回嵌入资源。
		/// </summary>
		private string ResolveTemplateContent(string fileName) {
			if (_options.TemplateDir != null) {
				var overridePath = Path.Combine(_options.TemplateDir.FullName, fileName);
				if (File.Exists(overridePath)) {
					_logger.Info($"Using external template: {overridePath}");
					return File.ReadAllText(overridePath);
				}
			}
			return _resMgr.GetResourceInString("Templates." + fileName);
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