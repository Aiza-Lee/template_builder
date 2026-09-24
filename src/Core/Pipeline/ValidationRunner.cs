using System.IO;
using Core.Models;
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
                    $"未找到源目录：{options.SourceDir.FullName}"));
                return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
            }
            checks.Add(new ValidationCheck("source", "exists", true, options.SourceDir.FullName));

            // Check 2: 配置文件存在
            if (!options.ConfigFile.Exists) {
                checks.Add(new ValidationCheck("config", "exists", false,
                    $"未找到配置文件：{options.ConfigFile.FullName}"));
                return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
            }

            // Check 2/3: 配置文件可解析 + 严格模式 key 白名单
            ProjectConfiguration projectConfig;
            try {
                projectConfig = new ProjectConfiguration(_logger, ConfigStrictness.Strict);
                var configJson = File.ReadAllText(options.ConfigFile.FullName);
                projectConfig.LoadProjectConfig(configJson, options.ConfigFile.FullName);
                checks.Add(new ValidationCheck("config", "parses", true, options.ConfigFile.FullName));
            } catch (MalformedConfigException ex) {
                checks.Add(new ValidationCheck("config", "parses", false, ex.Message));
                return EmitReport(checks, ExitCodes.MalformedConfig, options.Format);
            } catch (UnknownConfigKeyException ex) {
                checks.Add(new ValidationCheck("config", "unknown_key", false, ex.Message));
                return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
            }

            // Check 3.5: 统一配置值合法性校验（类型、枚举、范围、正则约束及 LaTeX 尺寸）
            var configValidation = Core.Validation.ConfigValidator.Validate(projectConfig);
            if (!configValidation.IsValid) {
                foreach (var err in configValidation.Errors) {
                    checks.Add(new ValidationCheck("config", err.PropertyPath, false,
                        $"{err.Message}（期望：{err.Expected}；建议：{err.Recommendation}）"));
                }
                return EmitReport(checks, ExitCodes.InvalidArguments, options.Format);
            }
            checks.Add(new ValidationCheck("config", "schema_validation", true, "类型、取值范围与枚举约束全部符合要求"));

            // Check 4: 嵌入式资源齐全
            try {
                var mainTex = _resMgr.GetResourceInString("Templates.Main.tex");
                var codeBlockTex = _resMgr.GetResourceInString("Templates.CodeBlock.tex");
                checks.Add(new ValidationCheck("resources", "Main.tex", true, $"{mainTex.Length} 字符"));
                checks.Add(new ValidationCheck("resources", "CodeBlock.tex", true, $"{codeBlockTex.Length} 字符"));
            } catch (MissingEmbeddedResourceException ex) {
                checks.Add(new ValidationCheck("resources", "embedded", false, ex.Message));
                return EmitReport(checks, ExitCodes.MissingEmbeddedResource, options.Format);
            }

            // Check 5: 源码预检（存在性、空目录、全过滤状态及章节最大深度 ≤ 4）
            var preflight = Core.Validation.SourcePreflight.Inspect(options.SourceDir, projectConfig, _logger);
            if (!preflight.IsValid) {
                checks.Add(new ValidationCheck("source", "preflight", false,
                    $"{preflight.Message}（建议：{preflight.Recommendation}）"));
                return EmitReport(checks, preflight.ExitCode, options.Format);
            }
            checks.Add(new ValidationCheck("source", "walk", true,
                $"匹配 {preflight.MatchedFiles} 个有效源文件（共发现 {preflight.TotalFiles} 个物理文件），最大深度 = {preflight.MaxDepth}"));
            checks.Add(new ValidationCheck("source", "depth", true,
                $"{preflight.MaxDepth} ≤ 4"));

            // Check 6: Main.tex 中 ##KEY## 都能在 TEX 段找到映射
            var missingPlaceholders = new List<string>();
            try {
                var mainTexContent = ResolveTemplateContent("Main.tex", options.TemplateDir);
                var texKeys = new HashSet<string>(
                    projectConfig.TexParser.GetAllConfigsAsString().Select(kvp => kvp.Key),
                    StringComparer.Ordinal);
                var referenced = TemplatePlaceholderScanner.FindMainPlaceholders(mainTexContent)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                missingPlaceholders = referenced.Where(p => !texKeys.Contains(p)).ToList();
                if (missingPlaceholders.Count == 0) {
                    checks.Add(new ValidationCheck("placeholders", "##KEY##", true,
                        $"{referenced.Count} 个占位符全部成功解析"));
                } else {
                    foreach (var m in missingPlaceholders) {
                        checks.Add(new ValidationCheck("placeholders", $"##{m}##", false,
                            $"在 Main.tex 中被引用但未在 TEX 配置中定义"));
                    }
                }
            } catch (Exception ex) {
                checks.Add(new ValidationCheck("placeholders", "##KEY##", false, ex.Message));
            }

            // Check 7: 外部环境依赖检查（字体目录 + xelatex/pygmentize 可选）
            var envResult = Core.Validation.EnvironmentPreflight.Inspect(projectConfig, options.SourceDir, options.CheckXelatex);
            foreach (var envCheck in envResult.Checks) {
                checks.Add(envCheck);
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
        /// 在 PATH 上查找指定命令的可执行文件。保留以兼容外部单测。
        /// </summary>
        internal static bool CheckExecutableOnPath(string executableName) =>
            Core.Validation.EnvironmentPreflight.CheckExecutableOnPath(executableName);

        /// <summary>
        /// 在 PATH 上查找 xelatex 可执行文件。
        /// </summary>
        internal static bool CheckXelatexOnPath() =>
            Core.Validation.EnvironmentPreflight.CheckXelatexOnPath();

        /// <summary>
        /// 在 PATH 上查找 pygmentize 可执行文件。
        /// </summary>
        internal static bool CheckPygmentizeOnPath() =>
            Core.Validation.EnvironmentPreflight.CheckPygmentizeOnPath();
    }
}
