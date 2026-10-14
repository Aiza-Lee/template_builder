using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Core.Diagnostics;
using Core.Models;
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

        public ProjectConfiguration? ProjectConfig { get; }

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

        public PdfBuilder(
            ILogger logger,
            BuildSubcommandOptions options,
            ProjectConfiguration projectConfig,
            ManifestResourceManager resMgr,
            IXelatexRunner? xelatexRunner = null
        ) : this(logger, options, projectConfig.TexParser, projectConfig.ProgramParser, resMgr, xelatexRunner) {
            ProjectConfig = projectConfig;
        }

        /// <summary>
        /// 执行构建命令并返回退出码。保持与旧调用点兼容。
        /// </summary>
        /// <returns>退出码（0 成功；1 xelatex 失败；3 模板存在未替换占位符）</returns>
        public int Build() => ExecuteBuild().ExitCode;

        public BuildResult? LastBuildResult { get; private set; }

        /// <summary>
        /// 执行构建流程并返回完整的结构化构建结果模型（包含状态、耗时、产物、诊断与警告）。
        /// </summary>
        public BuildResult ExecuteBuild() {
            var stopwatch = Stopwatch.StartNew();
            _logger.Info(Core.Localization.I18n.T("Build.Started", "构建流程已启动..."));
            _unresolvedPlaceholderCount = 0;

            // 清理上次遗留的中间文件，只清理属于本工具的产物，绝不碰用户文件
            PurgeWorkDir();

            // 生成 TeX 正文内容
            var mainTemplate = GenerateTexContent();
            if (_unresolvedPlaceholderCount > 0) {
                stopwatch.Stop();
                var msg = string.Format(Core.Localization.I18n.T("Build.UnresolvedPlaceholdersCount", "模板存在 {0} 个未被替换的占位符"), _unresolvedPlaceholderCount);
                var failure = BuildResult.CreateFailure(
                    ExitCodes.UnresolvedPlaceholders,
                    BuildStatus.UnresolvedPlaceholders,
                    msg,
                    diagnostics: [msg],
                    duration: stopwatch.Elapsed
                );
                LastBuildResult = failure;
                return failure;
            }

            // 保存 TeX 文件
            var midTexFileInfo = SaveTexFile(mainTemplate.ToString());

            // 编译 TeX 文件为 PDF
            var result = CompileTexToPdf(midTexFileInfo, stopwatch);
            LastBuildResult = result;
            return result;
        }

        /// <summary>
        /// 获取专属构建工作目录（位于 <sourceDir>/.template_builder/build/）。
        /// 隔离 LaTeX 中间文件与 minted 缓存，防止污染源目录或意外删除用户在 sourceDir/build 下的自建文件。
        /// </summary>
        internal string GetWorkDirectory() => GetWorkDirectory(_options.SourceDir);

        internal static string GetWorkDirectory(DirectoryInfo sourceDir) =>
            Path.Combine(sourceDir.FullName, ".template_builder", "build");

        private static readonly HashSet<string> KnownToolAuxExtensions = new(StringComparer.OrdinalIgnoreCase) {
            ".tex", ".aux", ".log", ".toc", ".out", ".nav", ".snm", ".aex", ".w18", ".synctex.gz", ".pdf"
        };

        internal static bool IsExternalLinkOrSymlink(string dirPath, string rootPath) {
            try {
                var dirInfo = new DirectoryInfo(dirPath);
                if (!dirInfo.Exists) return false;
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) || !string.IsNullOrEmpty(dirInfo.LinkTarget)) {
                    var target = dirInfo.LinkTarget;
                    if (string.IsNullOrEmpty(target)) return true; // 无法解析目标时保守处理为外部链接
                    if (!Path.IsPathRooted(target)) {
                        target = Path.GetFullPath(Path.Combine(dirInfo.Parent?.FullName ?? "", target));
                    }
                    var fullTarget = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return !fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            } catch {
                return true;
            }
        }

        /// <summary>
        /// 构建前清理专属工作目录中的旧中间文件。
        /// 严格遵循文件保护原则：仅清理可确认归属于本工具的文件和目录，
        /// 绝不触碰用户在源目录 build/ 或其他任何目录中自建的文件，并检查工作目录链接安全性。
        /// </summary>
        private void PurgeWorkDir() {
            var workDir = GetWorkDirectory();
            var sourceRoot = _options.SourceDir.FullName;
            var dotTbDir = Path.Combine(sourceRoot, ".template_builder");

            // 检查 .template_builder 及 build 目录是否为指向源目录外部的符号链接
            if (IsExternalLinkOrSymlink(dotTbDir, sourceRoot) || IsExternalLinkOrSymlink(workDir, sourceRoot)) {
                _logger.Warning(string.Format(Core.Localization.I18n.T("Build.SymlinkWorkdirSkipCleanup", "工作目录存在指向源目录外部的符号链接，为保护用户文件，跳过清理：{0}"), workDir));
                return;
            }

            CleanToolArtifacts(workDir);
        }

        private void CleanToolArtifacts(string dir) {
            if (!Directory.Exists(dir)) return;
            try {
                foreach (var file in Directory.EnumerateFiles(dir, "mid-output.*")) {
                    var ext = Path.GetExtension(file);
                    // 仅清理明确归属于本工具的辅助文件扩展名，防止误删用户自定义同名文件（如 mid-output.cpp）
                    if (!KnownToolAuxExtensions.Contains(ext)) {
                        continue;
                    }
                    var fi = new FileInfo(file);
                    if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                        continue;
                    }
                    try { File.Delete(file); } catch (Exception ex) {
                        _logger.Warning(string.Format(Core.Localization.I18n.T("Build.CleanOldAuxFailed", "清理旧中间文件 {0} 失败：{1}"), file, ex.Message));
                    }
                }
                foreach (var subDir in Directory.EnumerateDirectories(dir, "_minted*")) {
                    var dirName = Path.GetFileName(subDir);
                    // 仅清理工具专属的 minted 缓存目录，防止误删用户自建目录
                    if (!dirName.Equals("_minted", StringComparison.OrdinalIgnoreCase) &&
                        !dirName.Equals("_minted-mid-output", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    var sdi = new DirectoryInfo(subDir);
                    if (sdi.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                        continue;
                    }
                    try { Directory.Delete(subDir, recursive: true); } catch (Exception ex) {
                        _logger.Warning(string.Format(Core.Localization.I18n.T("Build.CleanOldMintedFailed", "清理旧 minted 目录 {0} 失败：{1}"), subDir, ex.Message));
                    }
                }
            } catch (Exception ex) {
                _logger.Warning(string.Format(Core.Localization.I18n.T("Build.CleanMidDirFailed", "清理中间目录 {0} 失败：{1}"), dir, ex.Message));
            }
        }

        private BuildResult CompileTexToPdf(FileInfo midTexFileInfo, Stopwatch stopwatch) {
            _logger.Info(Core.Localization.I18n.T("Build.StartingLatex", "开始 LaTeX 编译..."));

            var passCount = Math.Clamp(_programConfigParser["BUILD_PASS_COUNT"].GetAsInt(), 1, 5);
            var timeoutSeconds = Math.Clamp(_programConfigParser["BUILD_TIMEOUT_SECONDS"].GetAsInt(), 0, 600);

            bool cleanupNeeded = true;
            var diagnostics = new List<string>();
            var warnings = new List<string>();
            var logFilePath = Path.ChangeExtension(midTexFileInfo.FullName, ".log");

            int passesCompleted = 0;
            for (int pass = 1; pass <= passCount; pass++) {
                _logger.Info(string.Format(Core.Localization.I18n.T("Build.PassProgress", "第 {0} 轮编译（共 {1} 轮）..."), pass, passCount));

                // 每次 pass 清空 buffer，使最终 dump 时只看到失败 pass 的 stderr。
                _xelatexStderr.Clear();

                var result = RunXelatex(midTexFileInfo, pass, timeoutSeconds);
                passesCompleted = pass;

                if (result.TimedOut) {
                    stopwatch.Stop();
                    var errMsg = string.Format(Core.Localization.I18n.T("Build.XelatexTimeout", "xelatex 第 {0} 轮在 {1} 秒后超时，中止构建。"), pass, timeoutSeconds);
                    _logger.Error(errMsg);
                    FlushStderrAsError();
                    diagnostics.Add(errMsg);
                    return BuildResult.CreateFailure(
                        ExitCodes.XelatexFailure,
                        BuildStatus.Timeout,
                        errMsg,
                        diagnostics: diagnostics,
                        logPath: File.Exists(logFilePath) ? logFilePath : null,
                        duration: stopwatch.Elapsed,
                        passesCompleted: passesCompleted
                    );
                }
                if (result.ExitCode != 0) {
                    stopwatch.Stop();
                    var buildDirPdf = new FileInfo(Path.ChangeExtension(midTexFileInfo.FullName, ".pdf"));
                    string? diagnosticPdfPath = null;
                    if (buildDirPdf.Exists && buildDirPdf.Length > 0 && IsValidPdfHeader(buildDirPdf.FullName)) {
                        diagnosticPdfPath = buildDirPdf.FullName;
                        _logger.Warning(string.Format(Core.Localization.I18n.T("Build.PartialPdfDetected", "LaTeX 编译未完全成功（退出码 {0}），检测到半成品 PDF 产物：\"{1}\"。"), result.ExitCode, diagnosticPdfPath));
                        _logger.Warning(Core.Localization.I18n.T("Build.PartialPdfPreserved", "该文件已被保留作为诊断产物供排查，绝不覆盖/发布为最终目标 PDF 文件。"));
                    }

                    if (result.ExitCode == -1 && !result.TimedOut || result.Stderr.Contains("[failed to start:")) {
                        var hint = Core.Localization.I18n.T("Build.XelatexNotFoundHint", "排查指引：未检测到 xelatex 命令或无法启动。请确认已安装 TeX 发行版（如 TeX Live、MacTeX 或 MiKTeX），并将 xelatex 所在 bin 目录添加至系统环境变量 PATH。");
                        _logger.Error(hint);
                        diagnostics.Add(hint);
                    }

                    // 调用 LatexDiagnosticParser 进行结构化分析
                    var diag = LatexDiagnosticParser.Parse(result.Stdout, result.Stderr, logFilePath, midTexFileInfo.FullName);
                    _logger.Error(string.Format(Core.Localization.I18n.T("Build.DiagHeader", "--- LaTeX 编译错误诊断 (第 {0} 轮) ---"), pass));
                    if (!string.IsNullOrEmpty(diag.FailureReason)) {
                        var reasonText = string.Format(Core.Localization.I18n.T("Build.DiagReason", "原因：{0}"), diag.FailureReason);
                        _logger.Error(reasonText);
                        diagnostics.Add(reasonText);
                    }
                    if (!string.IsNullOrEmpty(diag.CulpritSourceFile)) {
                        var loc = diag.CulpritLineNumber.HasValue
                            ? $"{diag.CulpritSourceFile}:{diag.CulpritLineNumber.Value}"
                            : diag.CulpritSourceFile;
                        var locText = string.Format(Core.Localization.I18n.T("Build.DiagLocation", "定位源文件：{0}"), loc);
                        _logger.Error(locText);
                        diagnostics.Add(locText);
                    }
                    if (!string.IsNullOrEmpty(diag.Recommendation)) {
                        var recText = string.Format(Core.Localization.I18n.T("Build.DiagRecommendation", "排查建议：{0}"), diag.Recommendation);
                        _logger.Error(recText);
                        diagnostics.Add(recText);
                    }
                    if (diag.ErrorContextLines.Count > 0) {
                        _logger.Error(Core.Localization.I18n.T("Build.DiagContext", "--- 错误上下文 ---"));
                        foreach (var line in diag.ErrorContextLines.Take(8)) {
                            _logger.Error(line);
                        }
                    }

                    var errMsg = string.Format(Core.Localization.I18n.T("Build.XelatexFailedWithCode", "xelatex 退出码 {0}，LaTeX 编译失败。"), result.ExitCode);
                    _logger.Error(errMsg);
                    if (File.Exists(logFilePath)) {
                        _logger.Error(string.Format(Core.Localization.I18n.T("Build.LogLocated", "完整日志位于 \"{0}\""), logFilePath));
                    }
                    FlushStderrAsError();
                    diagnostics.Add(errMsg);

                    return BuildResult.CreateFailure(
                        ExitCodes.XelatexFailure,
                        BuildStatus.Failed,
                        errMsg,
                        diagnostics: diagnostics,
                        logPath: File.Exists(logFilePath) ? logFilePath : null,
                        duration: stopwatch.Elapsed,
                        passesCompleted: passesCompleted,
                        diagnosticPdfPath: diagnosticPdfPath,
                        culpritSourceFile: diag.CulpritSourceFile,
                        culpritLineNumber: diag.CulpritLineNumber,
                        failureReason: diag.FailureReason,
                        recommendation: diag.Recommendation
                    );
                } else {
                    _logger.Info(string.Format(Core.Localization.I18n.T("Build.PassCompleted", "第 {0} 轮编译完成。"), pass));
                }
            }

            // 编译完成，检验产物有效性
            var generatedPdf = new FileInfo(Path.ChangeExtension(midTexFileInfo.FullName, ".pdf"));
            if (!generatedPdf.Exists || generatedPdf.Length == 0 || !IsValidPdfHeader(generatedPdf.FullName)) {
                stopwatch.Stop();
                var errMsg = Core.Localization.I18n.T("Build.FailedToGenerateValidPdf", "LaTeX 编译未能生成有效的 PDF 产物，保留原产物（若存在）。");
                _logger.Error(errMsg);
                var diag = LatexDiagnosticParser.Parse(null, null, logFilePath, midTexFileInfo.FullName);
                if (!string.IsNullOrEmpty(diag.FailureReason)) {
                    var reasonText = string.Format(Core.Localization.I18n.T("Build.DiagReason", "原因：{0}"), diag.FailureReason);
                    _logger.Error(reasonText);
                    diagnostics.Add(reasonText);
                }
                if (!string.IsNullOrEmpty(diag.Recommendation)) {
                    var recText = string.Format(Core.Localization.I18n.T("Build.DiagRecommendation", "排查建议：{0}"), diag.Recommendation);
                    _logger.Error(recText);
                    diagnostics.Add(recText);
                }
                FlushStderrAsError();
                diagnostics.Add(errMsg);
                return BuildResult.CreateFailure(
                    ExitCodes.XelatexFailure,
                    BuildStatus.Failed,
                    errMsg,
                    diagnostics: diagnostics,
                    logPath: File.Exists(logFilePath) ? logFilePath : null,
                    duration: stopwatch.Elapsed,
                    passesCompleted: passesCompleted,
                    culpritSourceFile: diag.CulpritSourceFile,
                    culpritLineNumber: diag.CulpritLineNumber,
                    failureReason: diag.FailureReason,
                    recommendation: diag.Recommendation
                );
            }

            _logger.Info(Core.Localization.I18n.T("Build.Succeeded", "LaTeX 编译已成功完成。"));

            // 发布产物：使用临时文件过渡以保证原子写入，发布失败绝不损坏原有 PDF
            var publishResult = PublishOutputPdf(generatedPdf, _options.OutputPdf);
            if (publishResult != ExitCodes.Success) {
                stopwatch.Stop();
                var errMsg = string.Format(Core.Localization.I18n.T("Build.PublishFailed", "发布 PDF 到 \"{0}\" 失败，保留原有文件（若存在）。"), _options.OutputPdf.FullName);
                diagnostics.Add(errMsg);
                return BuildResult.CreateFailure(
                    publishResult,
                    BuildStatus.Failed,
                    errMsg,
                    diagnostics: diagnostics,
                    logPath: File.Exists(logFilePath) ? logFilePath : null,
                    duration: stopwatch.Elapsed,
                    passesCompleted: passesCompleted
                );
            }

            if (cleanupNeeded)
                CleanupAuxiliaryFiles();

            stopwatch.Stop();
            return BuildResult.CreateSuccess(
                _options.OutputPdf,
                midTexFileInfo.FullName,
                File.Exists(logFilePath) ? logFilePath : null,
                passesCompleted,
                stopwatch.Elapsed,
                warnings
            );
        }

        internal int PublishOutputPdf(FileInfo generatedPdf, FileInfo targetPdf) {
            try {
                if (targetPdf.Directory is { } parent && !parent.Exists) {
                    parent.Create();
                }

                var targetDir = targetPdf.DirectoryName ?? AppContext.BaseDirectory;
                var tempTargetPath = Path.Combine(targetDir, $".tmp-{Guid.NewGuid():N}.pdf");

                File.Copy(generatedPdf.FullName, tempTargetPath, overwrite: true);

                var tempInfo = new FileInfo(tempTargetPath);
                if (!tempInfo.Exists || tempInfo.Length != generatedPdf.Length) {
                    throw new IOException(Core.Localization.I18n.T("Build.TempVerifyFailed", "目标临时文件写入校验未通过。"));
                }

                File.Move(tempTargetPath, targetPdf.FullName, overwrite: true);
                _logger.Info(string.Format(Core.Localization.I18n.T("Build.PdfWritten", "PDF 已写入 \"{0}\"。"), targetPdf.FullName));
                return ExitCodes.Success;
            } catch (Exception ex) {
                _logger.Error(string.Format(Core.Localization.I18n.T("Build.PublishErrorWithEx", "发布 PDF 到 \"{0}\" 失败：{1}，保留原有文件（若存在）。"), targetPdf.FullName, ex.Message));
                return ExitCodes.XelatexFailure;
            }
        }

        internal static bool IsValidPdfHeader(string filePath) {
            try {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Span<byte> header = stackalloc byte[5];
                var bytesRead = fs.Read(header);
                if (bytesRead < 5) return false;
                return header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F' && header[4] == (byte)'-';
            } catch {
                return false;
            }
        }

        /// <summary>
        /// 编译失败时把 stderr 中含 "!" 或 "Error:" 的行以 Error 级别输出，并附上完整日志路径提示。
        /// </summary>
        private void FlushStderrAsError() {
            if (_xelatexStderr.Length == 0) return;

            var errorLines = _xelatexStderr.ToString()
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Length > 0 && (l.Contains('!') || l.Contains("Error:")))
                .ToList();

            if (errorLines.Count > 0) {
                _logger.Error(Core.Localization.I18n.T("Build.StderrSummaryHeader", "--- xelatex 错误摘要 ---"));
                foreach (var line in errorLines) {
                    _logger.Error(line);
                }
            }

            var logPath = Path.Combine(GetWorkDirectory(), "mid-output.log");
            _logger.Error(string.Format(Core.Localization.I18n.T("Build.LogLocated", "完整日志位于 \"{0}\""), logPath));
        }

        private XelatexResult RunXelatex(FileInfo midTexFileInfo, int pass, int timeoutSeconds) {
            string? shellEscapeSetting = _texConfigParser.TryGetConfigValue("CODE_SHELL_ESCAPE", out var shellVal)
                ? shellVal.GetAsString()
                : null;
            var shellMode = ResolveShellEscapeFlag(shellEscapeSetting);
            var arguments = BuildXelatexArguments(midTexFileInfo, shellMode);

            // 通过 IXelatexRunner 抽象 spawn xelatex；以 midTexFileInfo 所在目录（<sourceDir>/build/）为子进程工作目录，
            // 确保 minted 等宏包执行 shell-escape 临时测试文件（如 touch mid-output.aex）可正常在构建子目录写入。
            var workingDir = midTexFileInfo.DirectoryName ?? AppContext.BaseDirectory;
            var extraFontDirs = ResolveExtraFontDirs();
            var result = _xelatexRunner.Run(workingDir, arguments, timeoutSeconds, extraFontDirs);

            // 给本 pass 的 stderr 打标签，便于在 dump 时区分归属。
            AppendLabeledStderr(_xelatexStderr, pass, result.Stderr);
            return result;
        }

        /// <summary>
        /// 收集免安装字体搜索目录，供注入到 xelatex 子进程的 OSFONTDIR 环境变量。
        /// 扫描顺序：源目录下的 fonts/、当前工作目录下的 fonts/、系统/容器 /fonts，以及用户配置指定的 custom_font_dirs。
        /// </summary>
        internal List<string> ResolveExtraFontDirs() {
            var dirs = new List<string>();

            // 1. 扫描源目录下的 fonts/ 目录
            var sourceFontsDir = Path.Combine(_options.SourceDir.FullName, "fonts");
            if (Directory.Exists(sourceFontsDir)) {
                dirs.Add(sourceFontsDir);
            }

            // 2. 扫描当前工作目录下的 fonts/ 目录
            var cwdFontsDir = Path.GetFullPath("fonts");
            if (Directory.Exists(cwdFontsDir)) {
                dirs.Add(cwdFontsDir);
            }

            // 3. 扫描系统/容器通用挂载目录 /fonts
            if (Directory.Exists("/fonts")) {
                dirs.Add("/fonts");
            }

            // 4. 用户在配置 TEX.global.custom_font_dirs 中指定的额外目录
            var customDirs = _texConfigParser["GLOBAL_CUSTOM_FONT_DIRS"].GetAsStringArray();
            foreach (var customDir in customDirs) {
                if (string.IsNullOrWhiteSpace(customDir)) continue;
                var fullPath = Path.IsPathRooted(customDir)
                    ? customDir
                    : Path.GetFullPath(Path.Combine(_options.SourceDir.FullName, customDir));
                if (Directory.Exists(fullPath)) {
                    dirs.Add(fullPath);
                }
            }

            return dirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static string ResolveShellEscapeFlag(string? setting) {
            if (string.IsNullOrWhiteSpace(setting)) {
                return "-shell-escape";
            }
            var trimmed = setting.Trim();
            if (trimmed.Equals("restricted", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-shell-restricted", StringComparison.OrdinalIgnoreCase)) {
                return "-shell-restricted";
            }
            if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-no-shell-escape", StringComparison.OrdinalIgnoreCase)) {
                return "-no-shell-escape";
            }
            return trimmed.StartsWith('-') ? trimmed : "-" + trimmed;
        }

        /// <summary>
        /// 构造 xelatex 命令行参数串。提取为 internal static 便于单测断言参数内容（无需 mock Process）。
        /// </summary>
        /// <remarks>
        /// -output-directory 指向 midTexFileInfo 所在目录（即 <sourceDir>/build/），
        /// 与 SaveTexFile 的输出位置一致。LaTeX 中间文件（.aux/.log/.toc/.out/.nav/.snm）
        /// 与 minted 缓存（_minted/）落到该子目录，不污染源目录。
        /// PDF 由 PdfBuilder.Build 后续阶段从 `<sourceDir>/build/<jobname>.pdf` 拷贝到 -o 指定路径。
        /// </remarks>
        internal static string BuildXelatexArguments(FileInfo midTexFileInfo, string? shellEscapeMode = null) {
            var sb = new StringBuilder();
            var shellFlag = string.IsNullOrWhiteSpace(shellEscapeMode) ? "-shell-escape" : shellEscapeMode.Trim();
            if (!shellFlag.StartsWith('-')) {
                shellFlag = "-" + shellFlag;
            }
            sb.Append($"{shellFlag} ");
            sb.Append("-interaction=nonstopmode ");
            // 错误信息显示源文件行号（仅影响日志格式，不改输出内容/速度）
            sb.Append("-file-line-error ");
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
        /// 清理辅助文件。中间文件位于专属工作目录而非用户 -o 目录（见 SaveTexFile / BuildXelatexArguments）。
        /// </summary>
        private void CleanupAuxiliaryFiles() {
            var baseName = "mid-output"; // mid-output.tex 是固定名，与 jobname 无关
            var outputDir = GetWorkDirectory();
            var sourceRoot = _options.SourceDir.FullName;
            var dotTbDir = Path.Combine(sourceRoot, ".template_builder");
            if (IsExternalLinkOrSymlink(dotTbDir, sourceRoot) || IsExternalLinkOrSymlink(outputDir, sourceRoot)) {
                _logger.Warning(string.Format(Core.Localization.I18n.T("Build.SymlinkWorkdirSkipCleanup", "工作目录存在指向源目录外部的符号链接，为保护用户文件，跳过清理：{0}"), outputDir));
                return;
            }
            Cleanup(outputDir, baseName, _logger);
            // 同时删 _minted/ 缓存目录（minted 输出；保留可加速下次 build，但默认删以保持工作目录干净）
            var mintedDir = Path.Combine(outputDir, "_minted");
            if (Directory.Exists(mintedDir)) {
                var sdi = new DirectoryInfo(mintedDir);
                if (!sdi.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    try { Directory.Delete(mintedDir, recursive: true); } catch (Exception ex) {
                        _logger.Warning(string.Format(Core.Localization.I18n.T("Build.DeleteMintedFailed", "删除 {0} 失败：{1}"), mintedDir, ex.Message));
                    }
                }
            }
        }

        /// <summary>
        /// 清理 LaTeX 编译产生的中间文件。提取为 internal static 以便测试。
        /// </summary>
        internal static void Cleanup(string outputDir, string baseName, ILogger logger) {
            var extensionsToDelete = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm", ".aex", ".w18" };

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
                    var fi = new FileInfo(filePath);
                    if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                        logger.Warning(string.Format(Core.Localization.I18n.T("Build.SkipSymlinkAuxFile", "跳过符号链接辅助文件：{0}"), filePath));
                        return;
                    }
                    File.Delete(filePath);
                    logger.Debug($"Deleted auxiliary file: {filePath}");
                } catch (Exception ex) {
                    logger.Warning(string.Format(Core.Localization.I18n.T("Build.DeleteAuxFailed", "删除 {0} 失败：{1}"), filePath, ex.Message));
                }
            }
        }

        /// <summary>
        /// 保存 TeX 文件
        /// </summary>
        private FileInfo SaveTexFile(string texContent) {
            // 把 mid-output.tex 写到专属工作目录（与 xelatex -output-directory 同目录），
            // 让源目录保持干净。目录不存在时自动创建。
            var buildDir = GetWorkDirectory();
            var sourceRoot = _options.SourceDir.FullName;
            var dotTbDir = Path.Combine(sourceRoot, ".template_builder");
            if (IsExternalLinkOrSymlink(dotTbDir, sourceRoot) || IsExternalLinkOrSymlink(buildDir, sourceRoot)) {
                throw new InvalidOperationException(string.Format(Core.Localization.I18n.T("Build.WorkdirSymlinkRefuseWrite", "工作目录为指向源目录外部的符号链接：\"{0}\"，为保护系统文件已拒绝写入。"), buildDir));
            }
            Directory.CreateDirectory(buildDir);
            return new FileInfo(SaveTexFile(texContent, buildDir));
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

            // 设置 minted 的输出目录（可被 TEX.code.minted_outputdir 覆盖，CI 缓存用）
            mainTemplate.Replace("<<MINTED_OUTPUTDIR>>", ResolveMintedOutputDir());

            // PDF 元数据：keywords 数组 → "kw1, kw2, kw3"
            var keywords = string.Join(", ", _texConfigParser["METADATA_KEYWORDS"].GetAsStringArray());
            mainTemplate.Replace("<<METADATA_KEYWORDS>>", keywords);

            // Layout runtime: documentclass columns
            var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
            mainTemplate.Replace("<<DOC_CLASS_COLUMNS>>", columns == 2 ? "twocolumn" : "");

            // 封面与目录独立模块及列数自适应同步
            mainTemplate.Replace("<<COVER_PAGE_BLOCK>>", BuildCoverBlock());
            mainTemplate.Replace("<<TOC_BLOCK>>", BuildTocBlock());
            mainTemplate.Replace("<<LAYOUT_BODY_OPENING>>", BuildBodyOpening());

            // 保持对旧自定义模板中 <<LAYOUT_TOC_OPENING>> 的兼容
            mainTemplate.Replace("<<LAYOUT_TOC_OPENING>>", "");

            // ctex fontset 选项处理：auto / 空 → 留空（由 ctex 自动决定）；显式指定时注入 ,fontset={fontset}
            var fontset = _texConfigParser["DOCCLASS_FONTSET"].GetAsString();
            if (!string.IsNullOrWhiteSpace(fontset) && !fontset.Equals("auto", StringComparison.OrdinalIgnoreCase)) {
                mainTemplate.Replace("<<DOC_CLASS_FONTSET>>", $",fontset={fontset}");
            } else {
                mainTemplate.Replace("<<DOC_CLASS_FONTSET>>", "");
            }

            // CJK font block (runtime): 动态拼装 \setCJKmainfont{...}[...]，
            // BoldFont/ItalicFont 空值时跳过该选项（避免 LaTeX 非法语法 BoldFont=,）。
            mainTemplate.Replace("<<CJK_FONT_BLOCK>>", BuildCjkFontBlock());

            // TOC dot leaders (runtime): 默认 true → 空（LaTeX 自然有点引导），
            // false → \def\@dotsep{10000} 取消引导点。
            var tocDotLeaders = _texConfigParser["TOC_DOT_LEADERS"].GetAsBool(true);
            mainTemplate.Replace("<<TOC_DOT_LEADERS_LINE>>", tocDotLeaders ? "" : @"\def\@dotsep{10000}");

            // Typesetting parskip (runtime): 默认 false → 空；true → \usepackage{parskip}
            // 段间垂直空白替代段首缩进
            var parskipEnabled = _texConfigParser["TYPESETTING_PARSKIP_ENABLED"].GetAsBool(false);
            mainTemplate.Replace("<<TYPESETTING_PARSKIP_LINE>>", parskipEnabled ? @"\usepackage{parskip}" : "");

            // Geometry column rule (runtime): 默认 false → 空；true → \setlength{\columnseprule}{0.4pt}
            var columnRule = _texConfigParser["GEOMETRY_COLUMN_RULE"].GetAsBool(false);
            mainTemplate.Replace("<<GEOMETRY_COLUMN_RULE_LINE>>", columnRule ? @"\setlength{\columnseprule}{0.4pt}" : "");

            // Minted extra options (runtime): 动态拼装非空选项，避免空值在 pgfkeys / minted v3 下语法错误
            mainTemplate.Replace("<<MINTED_EXTRA_OPTIONS>>", BuildMintedExtraOptions());

            ReplaceMainPlaceholders(mainTemplate);

            // 在 <<CONTENT>> 替换前扫描 Main.tex，避免误报尚未替换的 <<CONTENT>> 标记。
            foreach (var placeholder in TemplatePlaceholderScanner.FindUnresolved(mainTemplate.ToString())) {
                if (placeholder == "<<CONTENT>>") continue;
                _logger.Error(string.Format(Core.Localization.I18n.T("Build.UnreplacedPlaceholder", "Main.tex 中存在未替换的占位符 \"{0}\"。"), placeholder));
                _unresolvedPlaceholderCount++;
            }

            int tabSize = _texConfigParser["CODE_TAB_SIZE"].GetAsInt();
            int sectionDepth = _texConfigParser["LAYOUT_SECTION_DEPTH"].GetAsInt();
            bool escapeSectionNames = _texConfigParser["LAYOUT_ESCAPE_SECTION_NAMES"].GetAsBool(true);
            string codeBlockTemplateContent = ResolveTemplateContent("CodeBlock.tex");
            var codeBlockGen = new CodeBlockGenerator(
                _logger, _programConfigParser, tabSize, _options.SourceDir,
                codeBlockTemplateContent, sectionDepth, escapeSectionNames,
                _options.SampleFileLimit);
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
                    _logger.Info(string.Format(Core.Localization.I18n.T("Build.UsingExternalTemplate", "使用外部模板：{0}"), overridePath));
                    return File.ReadAllText(overridePath);
                }
            }
            return _resMgr.GetResourceInString("Templates." + fileName);
        }

        /// <summary>
        /// 计算 minted outputdir。优先用 <c>TEX.code.minted_outputdir</c> 覆盖；
        /// 空字符串回退到 build/ 子目录（与 mid-output.tex 同目录），确保 _minted/
        /// 缓存与其他中间文件集中在一处，不污染源目录。返回值已统一使用正斜杠。
        /// CI 配合 actions/cache 用：把 _minted/ 放到稳定路径跨 run 复用。
        /// </summary>
        private string ResolveMintedOutputDir() {
            var mintedOverride = _texConfigParser["CODE_MINTED_OUTPUTDIR"].GetAsString();
            if (!string.IsNullOrEmpty(mintedOverride))
                return mintedOverride.Replace("\\", "/");
            // 默认：专属工作目录（xelatex -output-directory 指向此处），_minted/ 与 .aux/.log 并列
            var buildDir = GetWorkDirectory();
            return buildDir.Replace("\\", "/");
        }

        /// <summary>
        /// 动态拼装 CJK 主字体块，带智能优雅回退链（用户指定 -> SimSun -> Noto Serif CJK SC -> FandolSong）。
        /// 当 cjk_main_font 为空或 "auto" 时返回空字符串，完全交由 ctex 原生 fontset 处理。
        /// BoldFont/ItalicFont 为空字符串时跳过对应选项。AutoFake* 始终输出（默认 true）。
        /// </summary>
        internal string BuildCjkFontBlock() {
            var main = _texConfigParser["GLOBAL_CJK_MAIN_FONT"].GetAsString();
            if (string.IsNullOrWhiteSpace(main) || main.Equals("auto", StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }

            var bold = _texConfigParser["GLOBAL_CJK_MAIN_BOLD_FONT"].GetAsString();
            var italic = _texConfigParser["GLOBAL_CJK_MAIN_ITALIC_FONT"].GetAsString();
            var autoBold = _texConfigParser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true);
            var autoSlant = _texConfigParser["GLOBAL_CJK_AUTO_FAKE_SLANT"].GetAsBool(true);

            var opts = new StringBuilder();
            opts.Append("[\n");
            if (!string.IsNullOrEmpty(bold)) {
                opts.Append($"\t\tBoldFont={bold},\n");
            }
            if (!string.IsNullOrEmpty(italic)) {
                opts.Append($"\t\tItalicFont={italic},\n");
            }
            opts.Append($"\t\tAutoFakeBold={(autoBold ? "true" : "false")},\n");
            opts.Append($"\t\tAutoFakeSlant={(autoSlant ? "true" : "false")}\n");
            opts.Append("\t]");
            var optionsBlock = opts.ToString();

            // 智能回退候选链：优先用户指定字体，随后尝试 Windows 经典宋体、开源思源宋体和 TeX 标配 Fandol
            var candidates = new List<string> { main, "SimSun", "Noto Serif CJK SC", "FandolSong" }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = "";
            for (int i = candidates.Count - 1; i >= 0; i--) {
                var font = candidates[i];
                var branch = $"\\setCJKmainfont{{{font}}}{optionsBlock}";
                if (string.IsNullOrEmpty(result)) {
                    result = $"\\IfFontExistsTF{{{font}}}{{\n\t{branch}\n}}{{}}";
                } else {
                    result = $"\\IfFontExistsTF{{{font}}}{{\n\t{branch}\n}}{{\n\t{result}\n}}";
                }
            }
            return result;
        }

        /// <summary>
        /// 动态拼装 minted 的额外可选参数。对空值跳过该键，避免 pgfkeys 报错（例如 numberstyle=,）。
        /// </summary>
        private string BuildMintedExtraOptions() {
            var lines = new List<string>();
            var escapeinside = _texConfigParser["CODE_ESCAPEINSIDE"].GetAsString();
            if (!string.IsNullOrEmpty(escapeinside)) {
                lines.Add($"\tescapeinside={escapeinside},");
            }
            var xleftmargin = _texConfigParser["CODE_XLEFTMARGIN"].GetAsString();
            if (!string.IsNullOrEmpty(xleftmargin)) {
                lines.Add($"\txleftmargin={xleftmargin},");
            }
            var xrightmargin = _texConfigParser["CODE_XRIGHTMARGIN"].GetAsString();
            if (!string.IsNullOrEmpty(xrightmargin)) {
                lines.Add($"\txrightmargin={xrightmargin},");
            }
            var numberstyle = _texConfigParser["CODE_NUMBERSTYLE"].GetAsString();
            if (!string.IsNullOrEmpty(numberstyle)) {
                lines.Add($"\tnumberstyle={numberstyle},");
            }
            return lines.Count > 0 ? string.Join("\n", lines) + "\n" : "";
        }

        /// <summary>
        /// 当用户未显式配置标题时，从项目源目录名合理推导标题。
        /// </summary>
        internal string ResolveEffectiveTitle() {
            var configured = _texConfigParser["TITLE_CONTENT"].GetAsString();
            if (!string.IsNullOrWhiteSpace(configured)) {
                return configured;
            }

            var dir = _options.SourceDir;
            var name = dir.Name;
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == "..") {
                try {
                    name = new DirectoryInfo(dir.FullName).Name;
                } catch {
                    name = null;
                }
            }
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == "..") {
                name = "Code Template";
            }
            return name;
        }

        /// <summary>
        /// 动态构造独立封面页 TeX 块。若 TITLE_ENABLE_COVER=false，返回空字符串，彻底消除空白页缺陷。
        /// </summary>
        internal string BuildCoverBlock() {
            var enableCover = _texConfigParser["TITLE_ENABLE_COVER"].GetAsBool(false);
            if (!enableCover) return string.Empty;

            var sb = new StringBuilder();
            var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
            if (columns == 2) {
                sb.AppendLine(@"	\onecolumn");
            }
            sb.AppendLine(@"	\maketitle");
            var note = _texConfigParser["TITLE_NOTE"].GetAsString();
            if (!string.IsNullOrWhiteSpace(note)) {
                sb.AppendLine(@"	\begin{##TITLE_ALIGNMENT##}");
                sb.AppendLine(@"		##TITLE_NOTE##");
                sb.AppendLine(@"	\end{##TITLE_ALIGNMENT##}");
            }
            sb.AppendLine(@"	\clearpage");
            return sb.ToString();
        }

        /// <summary>
        /// 动态构造文档目录 TeX 块。若 TOC_ENABLE_TOC=false，返回空字符串。
        /// </summary>
        internal string BuildTocBlock() {
            var enableToc = _texConfigParser["TOC_ENABLE_TOC"].GetAsBool(true);
            if (!enableToc) return string.Empty;

            var enableCover = _texConfigParser["TITLE_ENABLE_COVER"].GetAsBool(false);
            var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
            var tocInColumns = _texConfigParser["LAYOUT_TOC_IN_COLUMNS"].GetAsBool(false);
            var tocDotLeaders = _texConfigParser["TOC_DOT_LEADERS"].GetAsBool(true);

            var sb = new StringBuilder();
            if (columns == 2) {
                if (tocInColumns) {
                    if (!enableCover) {
                        sb.AppendLine(@"	\onecolumn");
                    }
                } else {
                    if (enableCover) {
                        sb.AppendLine(@"	\twocolumn");
                    }
                }
            }

            sb.AppendLine(@"	\renewcommand{\contentsname}{##TOC_TITLE##}");
            if (!tocDotLeaders) {
                sb.AppendLine(@"	\def\@dotsep{10000}");
            }
            sb.AppendLine(@"	\tableofcontents");
            sb.AppendLine(@"	\clearpage");
            return sb.ToString();
        }

        /// <summary>
        /// 动态判断正文开始前的列数切换。在双栏排版下，若前面封面或目录使文档进入单栏模式，则在此恢复 \twocolumn。
        /// </summary>
        internal string BuildBodyOpening() {
            var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
            if (columns != 2) return string.Empty;

            var enableCover = _texConfigParser["TITLE_ENABLE_COVER"].GetAsBool(false);
            var enableToc = _texConfigParser["TOC_ENABLE_TOC"].GetAsBool(true);
            var tocInColumns = _texConfigParser["LAYOUT_TOC_IN_COLUMNS"].GetAsBool(false);

            bool isCurrentlyOneColumn;
            if (enableToc) {
                isCurrentlyOneColumn = tocInColumns;
            } else {
                isCurrentlyOneColumn = enableCover;
            }

            return isCurrentlyOneColumn ? "\t\\twocolumn\n" : string.Empty;
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
        /// <remarks>
        /// 重构：原实现对 ~60 个 key 各做一次 <c>StringBuilder.Replace</c>，
        /// 每次 O(N) 扫描，整体 O(K·N)。新实现编译一个正则
        /// <c>##[A-Z0-9_]+##</c>，单次扫描完成，O(N)。<br/>
        /// 兼容原"值含 ##OTHER## 时递归替换"语义：用 <c>do-while</c> 循环
        /// <c>Regex.Replace</c>，每次循环展开一层，直到文本不再变化或达到安全上限 10 次。
        /// </remarks>
        /// <param name="content">要替换的模板内容</param>
        private void ReplaceMainPlaceholders(StringBuilder content) {
            var escapeEnabled = _texConfigParser["TITLE_ESCAPE_LATEX_SPECIALS"].GetAsBool(true);
            // 1. 构造 key→value 字典（对 4 个用户文本字段做 LaTeX 转义）
            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in _texConfigParser.GetAllConfigsAsString()) {
                var effectiveValue = value;
                if (key == "TITLE_CONTENT" && string.IsNullOrWhiteSpace(effectiveValue)) {
                    effectiveValue = ResolveEffectiveTitle();
                }
                lookup[key] = escapeEnabled && _keysToEscape.Contains(key)
                    ? LatexEscaper.Escape(effectiveValue)
                    : effectiveValue;
            }
            // 2. 多次 Regex.Replace 直到稳定（兼容值含 ##OTHER## 的递归替换）
            var text = content.ToString();
            string prev;
            int safety = 0;
            do {
                prev = text;
                text = _placeholderRegex.Replace(text, m => {
                    var key = m.Groups[1].Value;
                    return lookup.TryGetValue(key, out var v) ? v : m.Value;
                });
            } while (text != prev && ++safety < 10);
            // 3. 写回 StringBuilder
            content.Clear();
            content.Append(text);
        }

        // ##KEY## 占位符正则：KEY 限定为大写/数字/下划线
        // （ConfigParser 路径展开规则保证所有 config key 都匹配此模式）。
        private static readonly Regex _placeholderRegex = new(
            @"##([A-Z0-9_]+)##",
            RegexOptions.Compiled);
    }
}