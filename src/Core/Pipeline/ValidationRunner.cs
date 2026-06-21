using System.IO;
using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
	/// <summary>
	/// validate 子命令的执行器：跑 7 项检查（不调 xelatex，不写盘）并返回退出码 + 报告。
	/// </summary>
	internal sealed class ValidationRunner(ILogger logger, ManifestResourceManager resMgr) {
		private readonly ILogger _logger = logger;
		private readonly ManifestResourceManager _resMgr = resMgr;

		/// <summary>
		/// 执行校验。捕获领域异常并按既定退出码返回。
		/// </summary>
		public int Run(ValidateSubcommandOptions options) {
			var checks = new List<ValidationCheck>();

			// Check 1: 源目录存在
			if (!options.SourceDir.Exists) {
				checks.Add(new ValidationCheck("source", "exists", false,
					$"Source directory not found: {options.SourceDir.FullName}"));
				return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
			}
			checks.Add(new ValidationCheck("source", "exists", true, options.SourceDir.FullName));

			// Check 2/3: 配置文件可解析 + 严格模式 key 白名单
			IConfigParser texParser;
			IConfigParser programParser;
			try {
				texParser = new ConfigParser("TEX", _logger, ConfigStrictness.Strict);
				programParser = new ConfigParser("PROGRAM", _logger, ConfigStrictness.Strict);
				var configJson = File.ReadAllText(options.ConfigFile.FullName);
				texParser.ParseConfigFile(configJson, options.ConfigFile.FullName);
				programParser.ParseConfigFile(configJson, options.ConfigFile.FullName);
				checks.Add(new ValidationCheck("config", "parses", true, options.ConfigFile.FullName));
			} catch (MalformedConfigException ex) {
				checks.Add(new ValidationCheck("config", "parses", false, ex.Message));
				return EmitReport(checks, ExitCodes.MalformedConfig, options.Format);
			} catch (UnknownConfigKeyException ex) {
				checks.Add(new ValidationCheck("config", "unknown_key", false, ex.Message));
				return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
			}

			// Check 4: 嵌入式资源齐全
			try {
				var mainTex = _resMgr.GetResourceInString("Templates.Main.tex");
				var codeBlockTex = _resMgr.GetResourceInString("Templates.CodeBlock.tex");
				checks.Add(new ValidationCheck("resources", "Main.tex", true, $"{mainTex.Length} chars"));
				checks.Add(new ValidationCheck("resources", "CodeBlock.tex", true, $"{codeBlockTex.Length} chars"));
			} catch (MissingEmbeddedResourceException ex) {
				checks.Add(new ValidationCheck("resources", "embedded", false, ex.Message));
				return EmitReport(checks, ExitCodes.MissingEmbeddedResource, options.Format);
			}

			// Check 5: 源码树能走通 + 章节深度 ≤ 4 (即 5 层 = section/subsection/.../subparagraph)
			var ignorePatterns = programParser["IGNORE_PATTERNS"].GetAsStringArray();
			int maxDepth = -1;
			try {
				foreach (var entry in SourceTreeWalker.Walk(options.SourceDir, ignorePatterns, _logger)) {
					if (entry.Depth > maxDepth) maxDepth = entry.Depth;
				}
				checks.Add(new ValidationCheck("source", "walk", true,
					$"max depth = {maxDepth}"));
				if (maxDepth >= 5) {
					checks.Add(new ValidationCheck("source", "depth", false,
						$"Section depth {maxDepth} exceeds maximum supported (4)"));
				} else {
					checks.Add(new ValidationCheck("source", "depth", true,
						$"{maxDepth} ≤ 4"));
				}
			} catch (Exception ex) {
				checks.Add(new ValidationCheck("source", "walk", false, ex.Message));
				return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
			}

			// Check 6: Main.tex 中 ##KEY## 都能在 TEX 段找到映射
			var missingPlaceholders = new List<string>();
			try {
				var mainTexContent = ResolveTemplateContent("Main.tex", options.TemplateDir);
				var texKeys = new HashSet<string>(
					texParser.GetAllConfigsAsString().Select(kvp => kvp.Key),
					StringComparer.Ordinal);
				var referenced = TemplatePlaceholderScanner.FindMainPlaceholders(mainTexContent)
					.Distinct(StringComparer.Ordinal)
					.ToList();
				missingPlaceholders = referenced.Where(p => !texKeys.Contains(p)).ToList();
				if (missingPlaceholders.Count == 0) {
					checks.Add(new ValidationCheck("placeholders", "##KEY##", true,
						$"{referenced.Count} placeholders all resolve"));
				} else {
					foreach (var m in missingPlaceholders) {
						checks.Add(new ValidationCheck("placeholders", $"##{m}##", false,
							$"Referenced in Main.tex but not defined in TEX config"));
					}
				}
			} catch (Exception ex) {
				checks.Add(new ValidationCheck("placeholders", "##KEY##", false, ex.Message));
			}

			// Check 7: xelatex 在 PATH 上（可选）
			if (options.CheckXelatex) {
				var found = CheckXelatexOnPath();
				checks.Add(new ValidationCheck("environment", "xelatex", found,
					found ? "found on PATH" : "xelatex not found on PATH"));
			}

			// Exit code priority: 3 (placeholder missing) > 6 (other soft failures)
			// 2/3/4/5 already short-circuited above, so only 3 vs 6 remains here.
			var errorCount = checks.Count(c => !c.Ok);
			int exitCode;
			if (errorCount == 0) {
				exitCode = ExitCodes.Success;
			} else if (missingPlaceholders.Count > 0) {
				exitCode = ExitCodes.UnresolvedPlaceholders;
			} else {
				exitCode = ExitCodes.ValidationFailed;
			}
			return EmitReport(checks, exitCode, options.Format);
		}

		/// <summary>
		/// 按 Format 输出报告并返回退出码。
		/// </summary>
		private int EmitReport(IReadOnlyList<ValidationCheck> checks, int exitCode, string format) {
			var errorCount = checks.Count(c => !c.Ok);
			var report = new ValidationReport(
				OverallOk: errorCount == 0,
				ErrorCount: errorCount,
				WarningCount: 0,
				Checks: checks
			);
			var output = format == "json" ? report.ToJson() : report.ToText();
			if (exitCode == 0) {
				_logger.Info(output);
			} else {
				_logger.Error(output);
			}
			return exitCode;
		}

		/// <summary>
		/// 解析模板内容：优先用 TemplateDir 下的文件，否则落回嵌入资源。
		/// </summary>
		private string ResolveTemplateContent(string fileName, DirectoryInfo? templateDir) {
			if (templateDir != null) {
				var overridePath = Path.Combine(templateDir.FullName, fileName);
				if (File.Exists(overridePath)) {
					return File.ReadAllText(overridePath);
				}
			}
			return _resMgr.GetResourceInString("Templates." + fileName);
		}

		/// <summary>
		/// 在 PATH 上查找 xelatex 可执行文件（Windows 优先 xelatex.exe）。
		/// </summary>
		internal static bool CheckXelatexOnPath() {
			var pathEnv = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrEmpty(pathEnv)) return false;
			var exeName = OperatingSystem.IsWindows() ? "xelatex.exe" : "xelatex";
			foreach (var dir in pathEnv.Split(Path.PathSeparator)) {
				if (string.IsNullOrWhiteSpace(dir)) continue;
				try {
					var candidate = Path.Combine(dir.Trim(), exeName);
					if (File.Exists(candidate)) return true;
				} catch {
					// 非法路径条目（罕见），跳过
				}
			}
			return false;
		}
	}
}
