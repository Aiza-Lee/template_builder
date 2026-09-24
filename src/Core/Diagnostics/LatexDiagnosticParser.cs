using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core.Diagnostics {
    /// <summary>
    /// LaTeX 编译诊断解析结果。
    /// </summary>
    internal sealed record LatexDiagnosticResult(
        string? FailureReason,
        string? CulpritSourceFile,
        int? CulpritLineNumber,
        int? TexLineNumber,
        string? Recommendation,
        IReadOnlyList<string> ErrorContextLines,
        IReadOnlyList<string> AllErrors
    );

    /// <summary>
    /// 解析 XeLaTeX 的 stdout、stderr 与 .log 文件，提取错误原因、报错行号、定位用户源文件并提供操作建议。
    /// </summary>
    internal static class LatexDiagnosticParser {
        private static readonly Regex LineNumberRegex = new(@"\bl\.(\d+)\b", RegexOptions.Compiled);
        private static readonly Regex SourceFileMarkerRegex = new(@"%\s*\[TB_SOURCE_FILE:\s*([^\]]+)\]", RegexOptions.Compiled);

        public static LatexDiagnosticResult Parse(
            string? stdout,
            string? stderr,
            string? logFilePath = null,
            string? texFilePath = null
        ) {
            var rawLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(stdout)) {
                rawLines.AddRange(stdout.Split('\n').Select(l => l.TrimEnd('\r')));
            }
            if (!string.IsNullOrWhiteSpace(stderr)) {
                rawLines.AddRange(stderr.Split('\n').Select(l => l.TrimEnd('\r')));
            }

            // 如果 stdout/stderr 缺乏错误细节，且存在 log 文件，则读取 log 文件补充
            if (File.Exists(logFilePath)) {
                try {
                    var logLines = File.ReadAllLines(logFilePath);
                    // 仅当目前没找到有效错误标记时才优先使用 logLines，或者合并分析
                    if (!rawLines.Any(l => l.StartsWith('!') || l.Contains("Error:"))) {
                        rawLines.AddRange(logLines);
                    }
                } catch {
                    // 读取 log 失败忽略，降级使用现存输出
                }
            }

            var allErrors = new List<string>();
            var errorBlocks = new List<List<string>>();
            List<string>? currentBlock = null;

            for (int i = 0; i < rawLines.Count; i++) {
                var line = rawLines[i].Trim();
                if (line.StartsWith('!')) {
                    allErrors.Add(line);
                    currentBlock = new List<string> { rawLines[i] };
                    errorBlocks.Add(currentBlock);
                } else if (currentBlock != null) {
                    if (string.IsNullOrWhiteSpace(line) && currentBlock.Count > 10) {
                        currentBlock = null;
                    } else if (currentBlock.Count < 15) {
                        currentBlock.Add(rawLines[i]);
                    }
                }
            }

            // 选取最具代表性的错误作为核心失败原因（优先排除泛化的 Emergency stop / Fatal error）
            string? primaryReason = null;
            List<string>? bestBlock = null;

            foreach (var block in errorBlocks) {
                var firstLine = block.FirstOrDefault() ?? "";
                if (firstLine.Contains("Emergency stop", StringComparison.OrdinalIgnoreCase) ||
                    firstLine.Contains("Fatal error occurred", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                primaryReason = firstLine;
                bestBlock = block;
                break;
            }

            if (primaryReason == null && errorBlocks.Count > 0) {
                bestBlock = errorBlocks[0];
                primaryReason = bestBlock.FirstOrDefault();
            }

            var contextLines = bestBlock ?? (IReadOnlyList<string>)Array.Empty<string>();

            // 解析出错的 mid-output.tex 行号
            int? texLineNumber = null;
            foreach (var line in contextLines) {
                var match = LineNumberRegex.Match(line);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var num)) {
                    texLineNumber = num;
                    break;
                }
            }

            // 若在最佳上下文块中未找到行号，遍历所有原始行做兜底查找
            if (!texLineNumber.HasValue) {
                for (int i = 0; i < rawLines.Count; i++) {
                    if (rawLines[i].StartsWith('!')) {
                        // 在报错行后的 5 行内查找
                        for (int j = i; j < Math.Min(rawLines.Count, i + 6); j++) {
                            var match = LineNumberRegex.Match(rawLines[j]);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var num)) {
                                texLineNumber = num;
                                break;
                            }
                        }
                        if (texLineNumber.HasValue) break;
                    }
                }
            }

            // 根据 texLineNumber 反查用户源文件
            string? culpritSourceFile = null;
            int? culpritLineNumber = null;

            if (texLineNumber.HasValue && !string.IsNullOrEmpty(texFilePath) && File.Exists(texFilePath)) {
                try {
                    var texLines = File.ReadAllLines(texFilePath);
                    var targetLineIdx = Math.Min(texLineNumber.Value - 1, texLines.Length - 1);

                    int? codeStartLine = null;
                    for (int i = targetLineIdx; i >= 0; i--) {
                        var line = texLines[i];
                        if (codeStartLine == null && line.Contains("\\begin{minted}")) {
                            codeStartLine = i + 1; // 1-based
                        }

                        var match = SourceFileMarkerRegex.Match(line);
                        if (match.Success) {
                            culpritSourceFile = match.Groups[1].Value.Trim();
                            if (codeStartLine.HasValue && texLineNumber.Value > codeStartLine.Value) {
                                culpritLineNumber = texLineNumber.Value - codeStartLine.Value;
                            }
                            break;
                        }
                    }

                    if (culpritSourceFile == null) {
                        // 检查是否在导言区
                        var docBeginIdx = Array.FindIndex(texLines, l => l.Contains("\\begin{document}"));
                        if (docBeginIdx >= 0 && texLineNumber.Value <= docBeginIdx + 1) {
                            culpritSourceFile = "导言区宏包/字体配置 (preamble)";
                        }
                    }
                } catch {
                    // 读取 tex 失败忽略
                }
            }

            var recommendation = GenerateRecommendation(primaryReason, allErrors);

            return new LatexDiagnosticResult(
                primaryReason,
                culpritSourceFile,
                culpritLineNumber,
                texLineNumber,
                recommendation,
                contextLines,
                allErrors
            );
        }

        private static string GenerateRecommendation(string? primaryReason, IReadOnlyList<string> allErrors) {
            var combined = $"{primaryReason} {string.Join(" ", allErrors)}";

            if (combined.Contains("minted", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("shell-escape", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("pygmentize", StringComparison.OrdinalIgnoreCase)) {
                return "minted 代码高亮宏包需要 Python Pygments 支持。请确认已在配置中启用 shell-escape，且系统中已安装 Pygments（执行 pip install pygments 并在 PATH 中）。";
            }

            if (combined.Contains("fontspec", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("cannot be found", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("font", StringComparison.OrdinalIgnoreCase) && combined.Contains("not found", StringComparison.OrdinalIgnoreCase)) {
                return "TeX 找不到指定字体。请检查 template.json 中的字体设置，或将所需 .ttf/.otf 字体文件放置在项目 fonts/ 目录下以供自动加载。";
            }

            if (combined.Contains("Undefined control sequence", StringComparison.OrdinalIgnoreCase)) {
                return "代码或模板中引用了未定义的 LaTeX 控制命令。若源文件中包含特殊字符（如 %, _, &, $, #, ~, ^），请检查转义规则，或确认引入了所需宏包。";
            }

            if (combined.Contains(".sty' not found", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("File `", StringComparison.OrdinalIgnoreCase) && combined.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("LaTeX Error: File", StringComparison.OrdinalIgnoreCase)) {
                return "系统缺少编译所需的 LaTeX 宏包。请使用 TeX Live 管理器 (tlmgr install <宏包名>) 或系统包管理器安装完整的宏包套件（如 texlive-latex-extra）。";
            }

            if (combined.Contains("Missing $ inserted", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("forgotten $", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("Extra }", StringComparison.OrdinalIgnoreCase)) {
                return "LaTeX 语法检测到不匹配的数学符号或花括号。请检查代码正文或模板中是否存在孤立的 $ 符号或多余的花括号。";
            }

            if (combined.Contains("Emergency stop", StringComparison.OrdinalIgnoreCase)) {
                return "LaTeX 编译遇到严重致命错误紧急中止。请查看上方错误上下文及完整 mid-output.log 日志。";
            }

            return "请根据定位到的源文件及具体报错行排查语法错误，或查阅完整 mid-output.log 编译日志。";
        }
    }
}
