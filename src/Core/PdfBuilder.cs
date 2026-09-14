using System.Text;
using System.Text.RegularExpressions;
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
            _logger.Info("构建流程已启动...");
            _unresolvedPlaceholderCount = 0;

            // 清理上次遗留的 build/ 中间文件，避免源目录扫描时产生噪声 warning
            PurgeBuildDir();

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

        /// <summary>
        /// 构建前清理 build/ 子目录及其中的旧中间文件。
        /// 确保源目录扫描器不会将 build 目录误认为源码目录产生幽灵章节或噪声 warning。
        /// build/ 不存在时静默跳过。
        /// </summary>
        private void PurgeBuildDir() {
            var buildDir = Path.Combine(_options.SourceDir.FullName, "build");
            if (!Directory.Exists(buildDir)) return;
            try {
                Directory.Delete(buildDir, recursive: true);
            } catch (Exception ex) {
                _logger.Warning($"构建前清理 {buildDir} 失败：{ex.Message}");
            }
        }

        private int CompileTexToPdf(FileInfo midTexFileInfo) {
            _logger.Info("开始 LaTeX 编译...");

            var passCount = Math.Clamp(_programConfigParser["BUILD_PASS_COUNT"].GetAsInt(), 1, 5);
            var timeoutSeconds = Math.Clamp(_programConfigParser["BUILD_TIMEOUT_SECONDS"].GetAsInt(), 0, 600);

            bool cleanupNeeded = true;

            for (int pass = 1; pass <= passCount; pass++) {
                _logger.Info($"第 {pass} 轮编译...");

                // 每次 pass 清空 buffer，使最终 dump 时只看到失败 pass 的 stderr。
                _xelatexStderr.Clear();

                var result = RunXelatex(midTexFileInfo, pass, timeoutSeconds);
                if (result.TimedOut) {
                    _logger.Error($"xelatex 第 {pass} 轮在 {timeoutSeconds} 秒后超时，中止构建。");
                    FlushStderrAsError();
                    return ExitCodes.XelatexFailure;
                }
                if (result.ExitCode != 0) {
                    // xelatex 把 PDF 写到 <src>/build/<jobname>.pdf；用户指定的是 <src>/build/mid-output.pdf
                    var buildDirPdf = new FileInfo(midTexFileInfo.FullName.Replace(".tex", ".pdf"));
                    if (buildDirPdf.Exists) {
                        _logger.Warning("xelatex 返回了非零退出码，但 PDF 已生成。请检查编译日志中的警告或非致命错误。");
                        cleanupNeeded = false; // 保留辅助文件以供调试
                    } else {
                        if (result.ExitCode == -1 && !result.TimedOut || result.Stderr.Contains("[failed to start:")) {
                            _logger.Error("排查指引：未检测到 xelatex 命令或无法启动。请确认已安装 TeX 发行版（如 TeX Live、MacTeX 或 MiKTeX），并将 xelatex 所在 bin 目录添加至系统环境变量 PATH。");
                        }
                        _logger.Error($"xelatex 退出码 {result.ExitCode}，LaTeX 编译失败。");
                        FlushStderrAsError();
                        return ExitCodes.XelatexFailure;
                    }
                }
            }
            _logger.Info("LaTeX 编译已成功完成。");

            // 把 <src>/build/<jobname>.pdf 拷贝到用户 -o 指定路径
            var generatedPdf = new FileInfo(midTexFileInfo.FullName.Replace(".tex", ".pdf"));
            if (generatedPdf.Exists) {
                try {
                    if (_options.OutputPdf.Directory is { } parent && !parent.Exists) {
                        parent.Create();
                    }
                    File.Copy(generatedPdf.FullName, _options.OutputPdf.FullName, overwrite: true);
                    _logger.Info($"PDF 已写入 \"{_options.OutputPdf.FullName}\"。");
                } catch (Exception ex) {
                    _logger.Error($"复制 PDF 到 \"{_options.OutputPdf.FullName}\" 失败：{ex.Message}");
                    return ExitCodes.XelatexFailure;
                }
            }

            if (cleanupNeeded)
                CleanupAuxiliaryFiles();
            return ExitCodes.Success;
        }

        /// <summary>
        /// 编译失败时把累积的 stderr 一次性以 Error 级别输出。
        /// </summary>
        private void FlushStderrAsError() {
            if (_xelatexStderr.Length == 0) return;
            _logger.Error("--- xelatex 标准错误输出 ---");
            foreach (var line in _xelatexStderr.ToString().Split('\n')) {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0) {
                    _logger.Error(trimmed);
                }
            }
        }

        private XelatexResult RunXelatex(FileInfo midTexFileInfo, int pass, int timeoutSeconds) {
            var arguments = BuildXelatexArguments(midTexFileInfo);

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

        /// <summary>
        /// 构造 xelatex 命令行参数串。提取为 internal static 便于单测断言参数内容（无需 mock Process）。
        /// </summary>
        /// <remarks>
        /// -output-directory 指向 midTexFileInfo 所在目录（即 <sourceDir>/build/），
        /// 与 SaveTexFile 的输出位置一致。LaTeX 中间文件（.aux/.log/.toc/.out/.nav/.snm）
        /// 与 minted 缓存（_minted/）落到该子目录，不污染源目录。
        /// PDF 由 PdfBuilder.Build 后续阶段从 `<sourceDir>/build/<jobname>.pdf` 拷贝到 -o 指定路径。
        /// </remarks>
        internal static string BuildXelatexArguments(FileInfo midTexFileInfo) {
            var sb = new StringBuilder();
            sb.Append("-shell-escape ");
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
        /// 清理辅助文件。中间文件位于 <sourceDir>/build/ 而非用户 -o 目录（见 SaveTexFile / BuildXelatexArguments）。
        /// </summary>
        private void CleanupAuxiliaryFiles() {
            var baseName = "mid-output"; // mid-output.tex 是固定名，与 jobname 无关
            var outputDir = Path.Combine(_options.SourceDir.FullName, "build");
            Cleanup(outputDir, baseName, _logger);
            // 同时删 _minted/ 缓存目录（minted 输出；保留可加速下次 build，但默认删以保持 build/ 干净）
            var mintedDir = Path.Combine(outputDir, "_minted");
            if (Directory.Exists(mintedDir)) {
                try { Directory.Delete(mintedDir, recursive: true); } catch (Exception ex) {
                    _logger.Warning($"删除 {mintedDir} 失败：{ex.Message}");
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
                    File.Delete(filePath);
                    logger.Debug($"Deleted auxiliary file: {filePath}");
                } catch (Exception ex) {
                    logger.Warning($"删除 {filePath} 失败：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 保存 TeX 文件
        /// </summary>
        private FileInfo SaveTexFile(string texContent) {
            // 把 mid-output.tex 写到 <src>/build/（与 xelatex -output-directory 同目录），
            // 让源目录保持干净。build/ 目录不存在时自动创建。
            var buildDir = Path.Combine(_options.SourceDir.FullName, "build");
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

            // Layout runtime: documentclass columns + TOC/body column toggles
            var columns = _texConfigParser["LAYOUT_COLUMNS"].GetAsInt();
            var tocInColumns = _texConfigParser["LAYOUT_TOC_IN_COLUMNS"].GetAsBool();
            mainTemplate.Replace("<<DOC_CLASS_COLUMNS>>", columns == 2 ? "twocolumn" : "");
            mainTemplate.Replace("<<LAYOUT_TOC_OPENING>>", columns == 2 ? (tocInColumns ? @"\onecolumn" : @"\twocolumn") : "");
            mainTemplate.Replace("<<LAYOUT_BODY_OPENING>>", columns == 2 && tocInColumns ? @"\twocolumn" : "");

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
                    _logger.Info($"使用外部模板：{overridePath}");
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
            // 默认：build/ 子目录（xelatex -output-directory 指向此处），_minted/ 与 .aux/.log 并列
            var buildDir = Path.Combine(_options.SourceDir.FullName, "build");
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
            var escapeEnabled = _texConfigParser["TITLE_ESCAPE_LATEX_SPECIALS"].GetAsBool();
            // 1. 构造 key→value 字典（对 4 个用户文本字段做 LaTeX 转义）
            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in _texConfigParser.GetAllConfigsAsString()) {
                lookup[key] = escapeEnabled && _keysToEscape.Contains(key)
                    ? LatexEscaper.Escape(value)
                    : value;
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