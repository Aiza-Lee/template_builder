using System.IO;
using Core.Models;
using Utils;

namespace Core.Validation {
    /// <summary>
    /// 源文件预检结果。
    /// </summary>
    /// <param name="IsValid">预检是否通过</param>
    /// <param name="ExitCode">若失败对应的规范退出码（如 ExitCodes.ValidationFailed 或 InvalidArguments）</param>
    /// <param name="Message">状态或错误详细描述</param>
    /// <param name="Recommendation">针对失败的可操作修复建议</param>
    /// <param name="TotalFiles">源目录下发现的物理文件总数（不含隐藏项）</param>
    /// <param name="MatchedFiles">白名单与过滤规则匹配后实际纳入排版的源文件总数</param>
    /// <param name="MaxDepth">实际匹配到的最深章节嵌套层级</param>
    /// <param name="DeepestPath">触及最深层级的文件或目录绝对路径</param>
    public sealed record SourcePreflightResult(
        bool IsValid,
        int ExitCode,
        string Message,
        string? Recommendation = null,
        int TotalFiles = 0,
        int MatchedFiles = 0,
        int MaxDepth = 0,
        string? DeepestPath = null
    ) {
        public static SourcePreflightResult Ok(int totalFiles, int matchedFiles, int maxDepth) =>
            new(true, ExitCodes.Success, $"源文件预检通过（共匹配 {matchedFiles} 个有效源文件，最大章节深度 {maxDepth}）", null, totalFiles, matchedFiles, maxDepth);

        public static SourcePreflightResult Failure(int exitCode, string message, string recommendation, int totalFiles = 0, int matchedFiles = 0, int maxDepth = 0, string? deepestPath = null) =>
            new(false, exitCode, message, recommendation, totalFiles, matchedFiles, maxDepth, deepestPath);
    }

    /// <summary>
    /// 统一源文件预检器：
    /// 负责在构建或校验前对源目录的存在性、空目录、全过滤状态及章节最大深度进行综合研判，
    /// 消除空跑假阳性（空目录退出 0）并统一 validate 与 build 的准入规则。
    /// </summary>
    internal static class SourcePreflight {
        /// <summary>
        /// LaTeX 规范下标准 article 类支持的最大章节深度（0=section, 1=subsection, 2=subsubsection, 3=paragraph, 4=subparagraph）。
        /// 深度超过 4（即嵌套层级达到 6 层及以上）时无法生成对应的 LaTeX 章节命令。
        /// </summary>
        public const int MaxSupportedDepth = 4;

        /// <summary>
        /// 执行源目录与文件准入预检。
        /// </summary>
        public static SourcePreflightResult Inspect(
            DirectoryInfo sourceDir,
            ProjectConfiguration config,
            ILogger? logger = null
        ) {
            if (!sourceDir.Exists) {
                return SourcePreflightResult.Failure(
                    ExitCodes.InvalidArguments,
                    $"未找到源目录：\"{sourceDir.FullName}\"",
                    "请检查源目录路径是否正确，或指定存在的源目录。"
                );
            }

            var includeFileTypes = config.ProgramParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
            var includeExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in includeFileTypes) {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                includeExts.Add(ext.StartsWith('.') ? ext : "." + ext);
            }

            var ignorePatterns = config.ProgramParser["IGNORE_PATTERNS"].GetAsStringArray();

            // 1. 统计物理文件总数（排除以 . 开头的隐藏项与根目录下的 build 目录）
            int totalFiles = 0;
            try {
                totalFiles = CountPhysicalFiles(sourceDir);
            } catch (Exception ex) {
                return SourcePreflightResult.Failure(
                    ExitCodes.InvalidArguments,
                    $"访问源目录失败：{ex.Message}",
                    "请检查源目录访问权限。"
                );
            }

            if (totalFiles == 0) {
                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    $"源目录 \"{sourceDir.FullName}\" 为空，未找到任何可排版的文件。",
                    "请在源目录中添加待编译的源码或文本文件后再执行操作。",
                    totalFiles: 0,
                    matchedFiles: 0
                );
            }

            // 2. 遍历源码树，统计匹配到的白名单文件数与最大深度
            int matchedFiles = 0;
            int maxDepth = -1;
            string? deepestPath = null;

            try {
                foreach (var entry in SourceTreeWalker.Walk(sourceDir, ignorePatterns, logger)) {
                    if (entry.Depth > maxDepth) {
                        maxDepth = entry.Depth;
                        deepestPath = entry.Info.FullName;
                    }

                    if (!entry.IsDirectory) {
                        var fileInfo = (FileInfo)entry.Info;
                        if (includeExts.Contains(fileInfo.Extension)) {
                            matchedFiles++;
                        }
                    }
                }
            } catch (Exception ex) {
                return SourcePreflightResult.Failure(
                    ExitCodes.InvalidArguments,
                    $"遍历源码树失败：{ex.Message}",
                    "请检查源码目录结构与读取权限。"
                );
            }

            // 3. 章节深度超限（深度 >= 5）
            if (maxDepth > MaxSupportedDepth) {
                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    $"源码目录嵌套层级过深（检测到深度 {maxDepth}，最大允许深度为 {MaxSupportedDepth}，对应 LaTeX \\subparagraph）。超深路径：\"{deepestPath}\"",
                    $"建议调整源码目录结构使嵌套深度不超过 {MaxSupportedDepth + 1} 层（深度 0~{MaxSupportedDepth}），或在 PROGRAM.ignore_patterns 中排除该深层目录。",
                    totalFiles: totalFiles,
                    matchedFiles: matchedFiles,
                    maxDepth: maxDepth,
                    deepestPath: deepestPath
                );
            }

            // 4. 所有文件均被过滤排除
            if (matchedFiles == 0) {
                var incSummary = string.Join(", ", includeExts);
                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    $"源目录 \"{sourceDir.FullName}\" 包含 {totalFiles} 个文件，但根据当前过滤规则未匹配到任何有效源文件。",
                    $"请检查配置中的 PROGRAM.include_file_types（当前配置：{incSummary}）和 PROGRAM.ignore_patterns，确保目标文件的扩展名在白名单中且未被模式排除。",
                    totalFiles: totalFiles,
                    matchedFiles: 0,
                    maxDepth: maxDepth,
                    deepestPath: deepestPath
                );
            }

            return SourcePreflightResult.Ok(totalFiles, matchedFiles, maxDepth);
        }

        private static int CountPhysicalFiles(DirectoryInfo dir, int currentDepth = 0) {
            int count = 0;
            foreach (var sub in dir.GetDirectories()) {
                if (sub.Name.StartsWith('.')) continue;
                if (currentDepth == 0 && sub.Name.Equals("build", StringComparison.OrdinalIgnoreCase)) continue;
                count += CountPhysicalFiles(sub, currentDepth + 1);
            }
            foreach (var f in dir.GetFiles()) {
                if (f.Name.StartsWith('.')) continue;
                count++;
            }
            return count;
        }
    }
}
