using System.IO;
using Core.Models;
using Core.SourceTree;
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
            new(true, ExitCodes.Success, string.Format(Core.Localization.I18n.T("Preflight.Ok", "源文件预检通过（共匹配 {0} 个有效源文件，最大章节深度 {1}）"), matchedFiles, maxDepth), null, totalFiles, matchedFiles, maxDepth);

        public static SourcePreflightResult Failure(int exitCode, string message, string recommendation, int totalFiles = 0, int matchedFiles = 0, int maxDepth = 0, string? deepestPath = null) =>
            new(false, exitCode, message, recommendation, totalFiles, matchedFiles, maxDepth, deepestPath);
    }

    /// <summary>
    /// 统一源文件预检器：
    /// 负责在构建或校验前对源目录的存在性、空目录、全过滤状态及章节最大深度进行综合研判，
    /// 消除空跑假阳性（空目录退出 0）并统一 validate 与 build 的准入规则。
    /// 复用统一源码树计划（SourceTreePlan），保证预检、TUI 内容预览与构建生成所依据的集合完全一致。
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
                    string.Format(Core.Localization.I18n.T("Preflight.SourceDirNotFound", "未找到源目录：\"{0}\""), sourceDir.FullName),
                    Core.Localization.I18n.T("Preflight.CheckSourceDirPath", "请检查源目录路径是否正确，或指定存在的源目录。")
                );
            }

            try {
                _ = sourceDir.GetFileSystemInfos();
            } catch (Exception ex) {
                return SourcePreflightResult.Failure(
                    ExitCodes.InvalidArguments,
                    string.Format(Core.Localization.I18n.T("Preflight.AccessSourceDirFailed", "访问源目录失败：{0}"), ex.Message),
                    Core.Localization.I18n.T("Preflight.CheckSourceDirPermission", "请检查源目录访问权限。")
                );
            }

            SourceTreePlan plan;
            try {
                plan = SourceTreePlan.Build(sourceDir, config, sectionDepth: MaxSupportedDepth + 1, logger);
            } catch (Exception ex) {
                return SourcePreflightResult.Failure(
                    ExitCodes.InvalidArguments,
                    string.Format(Core.Localization.I18n.T("Preflight.WalkSourceTreeFailed", "遍历源码树失败：{0}"), ex.Message),
                    Core.Localization.I18n.T("Preflight.CheckStructureAndPermission", "请检查源码目录结构与读取权限。")
                );
            }

            // 1. 物理文件总数为空
            if (plan.TotalPhysicalFiles == 0) {
                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    string.Format(Core.Localization.I18n.T("Preflight.SourceDirEmpty", "源目录 \"{0}\" 为空，未找到任何可排版的文件。"), sourceDir.FullName),
                    Core.Localization.I18n.T("Preflight.AddSourceFilesTip", "请在源目录中添加待编译的源码或文本文件后再执行操作。"),
                    totalFiles: 0,
                    matchedFiles: 0
                );
            }

            // 2. 所有文件均被过滤排除（无有效收录文件）
            if (plan.IncludedFiles.Count == 0) {
                var includeFileTypes = config.ProgramParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
                var incSummary = string.Join(", ", includeFileTypes.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.StartsWith('.') ? t : "." + t));
                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    string.Format(Core.Localization.I18n.T("Preflight.AllFilesFiltered", "源目录 \"{0}\" 包含 {1} 个文件，但根据当前过滤规则未匹配到任何有效源文件。"), sourceDir.FullName, plan.TotalPhysicalFiles),
                    string.Format(Core.Localization.I18n.T("Preflight.CheckIncludeTypesAndIgnore", "请检查配置中的 PROGRAM.include_file_types（当前配置：{0}）和 PROGRAM.ignore_patterns，确保目标文件的扩展名在白名单中且未被模式排除。"), incSummary),
                    totalFiles: plan.TotalPhysicalFiles,
                    matchedFiles: 0,
                    maxDepth: plan.MaxDepth
                );
            }

            // 3. 实际收录文件的章节嵌套深度超限（实际收录文件的真实深度 >= 5）
            if (plan.MaxDepth > MaxSupportedDepth) {
                var deepestFile = plan.IncludedFiles
                    .Where(f => f.Depth == plan.MaxDepth)
                    .OrderBy(f => f.RelativePath, NaturalStringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                var deepestPath = deepestFile?.File.FullName;

                return SourcePreflightResult.Failure(
                    ExitCodes.ValidationFailed,
                    string.Format(Core.Localization.I18n.T("Preflight.DirectoryTooDeep", "源码目录嵌套层级过深（检测到深度 {0}，最大允许深度为 {1}，对应 LaTeX \\subparagraph）。超深路径：\"{2}\""), plan.MaxDepth, MaxSupportedDepth, deepestPath ?? string.Empty),
                    string.Format(Core.Localization.I18n.T("Preflight.AdjustStructureTip", "建议调整源码目录结构使嵌套深度不超过 {0} 层（深度 0~{1}），或在 PROGRAM.ignore_patterns 中排除该深层目录。"), MaxSupportedDepth + 1, MaxSupportedDepth),
                    totalFiles: plan.TotalPhysicalFiles,
                    matchedFiles: plan.IncludedFiles.Count,
                    maxDepth: plan.MaxDepth,
                    deepestPath: deepestPath
                );
            }

            return SourcePreflightResult.Ok(plan.TotalPhysicalFiles, plan.IncludedFiles.Count, plan.MaxDepth);
        }
    }
}
