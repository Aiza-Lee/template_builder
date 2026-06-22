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
		private readonly IXelatexRunner _xelatexRunner;
		private readonly StringBuilder _xelatexStderr = new();
		private int _unresolvedPlaceholderCount;

		public PdfBuilder(
			ILogger logger,
			BuildSubcommandOptions options,
			IConfigParser texConfigParser,
			IConfigParser programConfigParser,
			ManifestResourceManager resMgr,
			IXelatexRunner? xelatexRunner = null
		) {
			_logger = logger;
			_options = options;
			_texConfigParser = texConfigParser;
			_programConfigParser = programConfigParser;
			_resMgr = resMgr;
			_xelatexRunner = xelatexRunner ?? new XelatexRunner(logger);
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
				return ExitCodes.UnresolvedPlaceholders;
			}

			// 保存 TeX 文件
			var midTexFileInfo = SaveTexFile(mainTemplate.ToString());

			// 编译 TeX 文件为 PDF
			return CompileTexToPdf(midTexFileInfo);
		}

		private int CompileTexToPdf(FileInfo midTexFileInfo) {
			_logger.Info("Starting LaTeX compilation...");

			var passCount = Math.Clamp(_programConfigParser["BUILD_PASS_COUNT"].GetAsInt(), 1, 5);
			var timeoutSeconds = Math.Clamp(_programConfigParser["BUILD_TIMEOUT_SECONDS"].GetAsInt(), 0, 600);

			bool cleanupNeeded = true;

			for (int pass = 1; pass <= passCount; pass++) {
				_logger.Info($"Compilation pass #{pass}...");

				// 每次 pass 清空 buffer，使最终 dump 时只看到失败 pass 的 stderr。
				_xelatexStderr.Clear();

				var result = RunXelatex(midTexFileInfo, pass, timeoutSeconds);
				if (result.TimedOut) {
					_logger.Error($"xelatex pass #{pass} timed out after {timeoutSeconds}s. Aborting build.");
					FlushStderrAsError();
					return ExitCodes.XelatexFailure;
				}
				if (result.ExitCode != 0) {
					if (_options.OutputPdf.Exists) {
						_logger.Warning("xelatex returned a non-zero exit code, but the PDF was generated. Please check the compilation log for warnings or non-fatal errors.");
						cleanupNeeded = false; // 保留辅助文件以供调试
					} else {
						_logger.Error($"xelatex exited with code {result.ExitCode}. LaTeX compilation failed.");
						FlushStderrAsError();
						return ExitCodes.XelatexFailure;
					}
				}
			}
			_logger.Info("LaTeX compilation completed successfully.");
			if (cleanupNeeded)
				CleanupAuxiliaryFiles();
			return ExitCodes.Success;
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

		private XelatexResult RunXelatex(FileInfo midTexFileInfo, int pass, int timeoutSeconds) {
			var arguments = BuildXelatexArguments(midTexFileInfo);

			// 通过 IXelatexRunner 抽象 spawn xelatex；返回 XelatexResult 包含合并的 stderr 与超时标记。
			var result = _xelatexRunner.Run(AppContext.BaseDirectory, arguments, timeoutSeconds);

			// 给本 pass 的 stderr 打标签，便于在 dump 时区分归属。
			AppendLabeledStderr(_xelatexStderr, pass, result.Stderr);
			return result;
		}

		/// <summary>
		/// 构造 xelatex 命令行参数串。提取为 internal static 便于单测断言参数内容（无需 mock Process）。
		/// </summary>
		internal static string BuildXelatexArguments(FileInfo midTexFileInfo) {
			var sb = new StringBuilder();
			sb.Append("-shell-escape ");
			sb.Append("-interaction=nonstopmode ");
			sb.Append($"-jobname={Path.GetFileNameWithoutExtension(midTexFileInfo.Name)} ");
			sb.Append($"-output-directory \"{midTexFileInfo.DirectoryName}\" ");
			sb.Append($"\"{midTexFileInfo.FullName}\"");
			return sb.ToString();
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
		/// 生成 TeX 正文内容（暴露为 internal 以便单测断言占位符替换结果）。
		/// </summary>
		internal string GenerateTexContent_ForTest() {
			return GenerateTexContent().ToString();
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

			// PDF 元数据：keywords 数组 → "kw1, kw2, kw3"
			var keywords = string.Join(", ", _texConfigParser["METADATA_KEYWORDS"].GetAsStringArray());
			mainTemplate.Replace("<<METADATA_KEYWORDS>>", keywords);

			// Layout runtime: documentclass columns + TOC/body column toggles
			var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
			var tocInColumns = _texConfigParser["LAYOUT_TOC_IN_COLUMNS"].GetAsBool();
			mainTemplate.Replace("<<DOC_CLASS_COLUMNS>>", columns == 2 ? "twocolumn" : "");
			mainTemplate.Replace("<<LAYOUT_TOC_OPENING>>", columns == 2 ? (tocInColumns ? @"\onecolumn" : @"\twocolumn") : "");
			mainTemplate.Replace("<<LAYOUT_BODY_OPENING>>", columns == 2 && tocInColumns ? @"\twocolumn" : "");

			// CJK font block (runtime): 动态拼装 \setCJKmainfont{...}[...]，
			// BoldFont/ItalicFont 空值时跳过该选项（避免 LaTeX 非法语法 BoldFont=,）。
			mainTemplate.Replace("<<CJK_FONT_BLOCK>>", BuildCjkFontBlock());

			// TOC dot leaders (runtime): 默认 true → 空（LaTeX 自然有点引导），
			// false → \def\@dotsep{10000} 取消引导点。
			var tocDotLeaders = _texConfigParser["TOC_DOT_LEADERS"].GetAsBool(true);
			mainTemplate.Replace("<<TOC_DOT_LEADERS_LINE>>", tocDotLeaders ? "" : @"\def\@dotsep{10000}");

			ReplaceMainPlaceholders(mainTemplate);

			// 在 <<CONTENT>> 替换前扫描 Main.tex，避免误报尚未替换的 <<CONTENT>> 标记。
			foreach (var placeholder in TemplatePlaceholderScanner.FindUnresolved(mainTemplate.ToString())) {
				if (placeholder == "<<CONTENT>>") continue;
				_logger.Error($"Unresolved placeholder '{placeholder}' in Main.tex.");
				_unresolvedPlaceholderCount++;
			}

			int tabSize = _texConfigParser["CODE_TAB_SIZE"].GetAsInt();
			int sectionDepth = _texConfigParser["LAYOUT_SECTION_DEPTH"].GetAsInt();
			bool escapeSectionNames = _texConfigParser["LAYOUT_ESCAPE_SECTION_NAMES"].GetAsBool(true);
			string codeBlockTemplateContent = ResolveTemplateContent("CodeBlock.tex");
			var codeBlockGen = new CodeBlockGenerator(
				_logger, _programConfigParser, tabSize, _options.SourceDir,
				codeBlockTemplateContent, sectionDepth, escapeSectionNames);
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
		/// 动态拼装 CJK 主字体块。BoldFont/ItalicFont 为空字符串时跳过对应选项（避免
		/// LaTeX 收到 BoldFont=, 非法语法）。AutoFake* 始终输出（默认 true）。
		/// 提取为 private 方法以便未来加 unit test 时直接覆盖。
		/// </summary>
		private string BuildCjkFontBlock() {
			var main = _texConfigParser["GLOBAL_CJK_MAIN_FONT"].GetAsString();
			var bold = _texConfigParser["GLOBAL_CJK_MAIN_BOLD_FONT"].GetAsString();
			var italic = _texConfigParser["GLOBAL_CJK_MAIN_ITALIC_FONT"].GetAsString();
			var autoBold = _texConfigParser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true);
			var autoSlant = _texConfigParser["GLOBAL_CJK_AUTO_FAKE_SLANT"].GetAsBool(true);

			var sb = new StringBuilder();
			sb.Append($"\\setCJKmainfont{{{main}}}[\n");
			if (!string.IsNullOrEmpty(bold)) {
				sb.Append($"    BoldFont={bold},\n");
			}
			if (!string.IsNullOrEmpty(italic)) {
				sb.Append($"    ItalicFont={italic},\n");
			}
			sb.Append($"    AutoFakeBold={(autoBold ? "true" : "false")},\n");
			sb.Append($"    AutoFakeSlant={(autoSlant ? "true" : "false")}\n");
			sb.Append(']');
			return sb.ToString();
		}

		/// <summary>
		/// 需要做 LaTeX 转义的占位符键（用户可见的文本字段）。
		/// </summary>
		private static readonly IReadOnlySet<string> _keysToEscape = new HashSet<string>(StringComparer.Ordinal) {
			"AUTHOR", "SUBJECT", "TITLE_CONTENT", "TITLE_NOTE"
		};

		/// <summary>
		/// 替换 MainTeX 模板中的占位符。对用户可见的文本字段（标题/作者/备注）做 LaTeX
		/// 转义，防止 `_` / `%` / `&` 等字符破坏编译。可以通过
		/// <c>TEX.TITLE.ESCAPE_LATEX_SPECIALS=false</c> 关掉。
		/// </summary>
		/// <param name="content">要替换的模板内容</param>
		private void ReplaceMainPlaceholders(StringBuilder content) {
			var escapeEnabled = _texConfigParser["TITLE_ESCAPE_LATEX_SPECIALS"].GetAsBool();
			foreach (var kvp in _texConfigParser.GetAllConfigsAsString()) {
				var placeholder = $"##{kvp.Key}##";
				var value = escapeEnabled && _keysToEscape.Contains(kvp.Key)
					? LatexEscaper.Escape(kvp.Value)
					: kvp.Value;
				content.Replace(placeholder, value);
			}
		}
	}
}