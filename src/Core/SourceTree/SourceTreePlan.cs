using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Utils;

namespace Core.SourceTree {
    /// <summary>
    /// 文件或目录的收录状态。
    /// </summary>
    internal enum SourceInclusionStatus {
        /// <summary>已纳入排版与编译</summary>
        Included,
        /// <summary>未纳入排版（被白名单、模式或层级规则排除）</summary>
        Excluded
    }

    /// <summary>
    /// 章节树与文件清单预览过滤模式。
    /// </summary>
    internal enum SourceTreeFilterMode {
        /// <summary>展示完整章节树（含收录与排除项）</summary>
        All,
        /// <summary>仅展示已纳入构建的源文件及章节顺序</summary>
        IncludedOnly,
        /// <summary>仅展示被排除项及详细原因</summary>
        ExcludedOnly
    }

    /// <summary>
    /// 文件或目录未纳入构建的结构化原因。
    /// </summary>
    internal enum SourceExclusionReason {
        None,
        /// <summary>以 "." 开头的隐藏文件或目录</summary>
        Hidden,
        /// <summary>命中 PROGRAM.ignore_patterns 排除通配规则</summary>
        IgnorePattern,
        /// <summary>扩展名未在 PROGRAM.include_file_types 白名单中</summary>
        ExtensionNotIncluded,
        /// <summary>目录层级超过 LaTeX 支持的最大深度 4</summary>
        DepthExceeded,
        /// <summary>空目录或内部所有子项均已被排除</summary>
        EmptyDirectory
    }

    /// <summary>
    /// 已纳入构建的源码文件计划项。
    /// </summary>
    internal sealed record SourceFilePlan(
        FileInfo File,
        string RelativePath,
        string DisplayTitle,
        int Depth,
        int EffectiveDepth,
        string SectionCommand,
        string EffectiveLanguage,
        bool IsRawLatex,
        int OrderWeight
    );

    /// <summary>
    /// 被排除项详细记录。
    /// </summary>
    internal sealed record ExcludedItemPlan(
        FileSystemInfo Info,
        string RelativePath,
        bool IsDirectory,
        int Depth,
        SourceExclusionReason Reason,
        string Explanation,
        string? MatchedPattern = null,
        string? RecognizedLanguage = null
    );

    /// <summary>
    /// 章节树节点（包含目录与文件），用于构建前预览呈现与 LaTeX 章节生成。
    /// </summary>
    internal sealed class SourceNodePlan {
        public FileSystemInfo Info { get; }
        public string RelativePath { get; }
        public bool IsDirectory { get; }
        public int Depth { get; }
        public int EffectiveDepth { get; set; }
        public string DisplayTitle { get; }
        public int OrderWeight { get; }

        public SourceInclusionStatus Status { get; set; }
        public SourceExclusionReason ExclusionReason { get; set; }
        public string? ExclusionExplanation { get; set; }

        // 文件特有属性
        public string? EffectiveLanguage { get; set; }
        public string? SectionCommand { get; set; }
        public bool IsRawLatex { get; set; }
        public bool IsRecognizedLanguage { get; set; }

        public List<SourceNodePlan> Children { get; } = new();

        public SourceNodePlan(
            FileSystemInfo info,
            string relativePath,
            bool isDirectory,
            int depth,
            string displayTitle,
            int orderWeight
        ) {
            Info = info;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Depth = depth;
            DisplayTitle = displayTitle;
            OrderWeight = orderWeight;
        }

        public bool HasIncludedDescendants =>
            Children.Any(c => c.Status == SourceInclusionStatus.Included || c.HasIncludedDescendants);
    }

    /// <summary>
    /// 统一源码树计划（SSOT 单一真实源）：
    /// 供 TUI 构建前内容预览、SourcePreflight 预检及 CodeBlockGenerator 代码生成共用，
    /// 确保预览呈现、准入判定与实际编译排版 100% 走完全相同的文件清单、排序权重与章节标题。
    /// </summary>
    internal sealed class SourceTreePlan {
        public const int MaxSupportedDepth = 4;

        private static readonly string[] s_allSectionCommands = [
            "section", "subsection", "subsubsection", "paragraph", "subparagraph"
        ];

        public static readonly Dictionary<string, string> DefaultLanguageMap = new(StringComparer.OrdinalIgnoreCase) {
            { "c", "c" }, { "cpp", "cpp" }, { "cc", "cpp" }, { "cxx", "cpp" },
            { "hpp", "cpp" }, { "hxx", "cpp" }, { "h", "c" }, { "java", "java" },
            { "cs", "csharp" }, { "rs", "rust" }, { "ts", "typescript" },
            { "js", "javascript" }, { "py", "python" }, { "rb", "ruby" },
            { "go", "go" }, { "php", "php" }, { "html", "html" },
            { "css", "css" }, { "xml", "xml" }, { "json", "json" },
            { "sh", "bash" }, { "bat", "batch" }, { "ps1", "powershell" },
            { "swift", "swift" }, { "kt", "kotlin" }, { "m", "objective-c" },
            { "sql", "sql" }, { "yaml", "yaml" }, { "yml", "yaml" },
            { "txt", "text" }, { "text", "text" }, { "log", "text" },
            { "md", "markdown" }, { "markdown", "markdown" }
        };

        public DirectoryInfo SourceDir { get; }
        public int TotalPhysicalFiles { get; }
        public int TotalPhysicalDirectories { get; }
        public IReadOnlyList<SourceFilePlan> IncludedFiles { get; }
        public IReadOnlyList<ExcludedItemPlan> ExcludedFiles { get; }
        public IReadOnlyList<ExcludedItemPlan> ExcludedDirectories { get; }
        public IReadOnlyList<SourceNodePlan> RootNodes { get; }
        public int MaxDepth { get; }

        private SourceTreePlan(
            DirectoryInfo sourceDir,
            int totalPhysicalFiles,
            int totalPhysicalDirectories,
            IReadOnlyList<SourceFilePlan> includedFiles,
            IReadOnlyList<ExcludedItemPlan> excludedFiles,
            IReadOnlyList<ExcludedItemPlan> excludedDirectories,
            IReadOnlyList<SourceNodePlan> rootNodes,
            int maxDepth
        ) {
            SourceDir = sourceDir;
            TotalPhysicalFiles = totalPhysicalFiles;
            TotalPhysicalDirectories = totalPhysicalDirectories;
            IncludedFiles = includedFiles;
            ExcludedFiles = excludedFiles;
            ExcludedDirectories = excludedDirectories;
            RootNodes = rootNodes;
            MaxDepth = maxDepth;
        }

        public static Dictionary<string, string> ParseTitleOverrides(IEnumerable<string>? rawOverrides) {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (rawOverrides == null) return dict;
            foreach (var raw in rawOverrides) {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var idx = raw.IndexOf(':');
                if (idx <= 0 || idx >= raw.Length - 1) continue;
                var key = raw[..idx].Trim().Replace('\\', '/');
                var val = raw[(idx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val)) {
                    dict[key] = val;
                }
            }
            return dict;
        }

        public static Dictionary<string, int> ParseOrderOverrides(IEnumerable<string>? rawOverrides, ILogger? logger = null) {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (rawOverrides == null) return dict;
            foreach (var raw in rawOverrides) {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var idx = raw.LastIndexOf(':');
                if (idx <= 0 || idx >= raw.Length - 1) continue;
                var key = raw[..idx].Trim().Replace('\\', '/');
                var valStr = raw[(idx + 1)..].Trim();
                if (int.TryParse(valStr, out int weight)) {
                    dict[key] = weight;
                } else {
                    logger?.Warning($"section_order_overrides 条目 \"{raw}\" 权重不是有效整数，已忽略。");
                }
            }
            return dict;
        }

        public static Dictionary<string, string> BuildLanguageMap(IEnumerable<string>? rawOverrides, ILogger? logger = null) {
            var map = new Dictionary<string, string>(DefaultLanguageMap, StringComparer.OrdinalIgnoreCase);
            if (rawOverrides == null) return map;
            foreach (var pair in rawOverrides) {
                if (string.IsNullOrWhiteSpace(pair)) continue;
                var idx = pair.IndexOf(':');
                if (idx <= 0 || idx >= pair.Length - 1) continue;
                var ext = pair[..idx].Trim();
                var lang = pair[(idx + 1)..].Trim();
                if (!ext.StartsWith('.')) {
                    logger?.Warning($"code_language_overrides 条目 \"{pair}\" 的扩展名缺少 \".\" 前缀，已跳过。");
                    continue;
                }
                map[ext[1..]] = lang;
            }
            return map;
        }

        public static string ResolveTitle(string name, string relativePath, IReadOnlyDictionary<string, string> titleOverrides) {
            if (titleOverrides.TryGetValue(relativePath, out var title1)) return title1;
            if (titleOverrides.TryGetValue(name, out var title2)) return title2;
            return name;
        }

        public static int ResolveOrderWeight(string name, string relativePath, IReadOnlyDictionary<string, int> orderOverrides) {
            if (orderOverrides.TryGetValue(relativePath, out var w1)) return w1;
            if (orderOverrides.TryGetValue(name, out var w2)) return w2;
            return int.MaxValue;
        }

        public static SourceTreePlan Build(
            DirectoryInfo sourceDir,
            IConfigParser programParser,
            int sectionDepth = 5,
            ILogger? logger = null
        ) {
            var includeFileTypesRaw = programParser["INCLUDE_FILE_TYPES"].GetAsStringArray();
            var includeExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in includeFileTypesRaw) {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                includeExts.Add(ext.StartsWith('.') ? ext : "." + ext);
            }

            var ignorePatterns = programParser["IGNORE_PATTERNS"].GetAsStringArray();
            var titleOverrides = ParseTitleOverrides(programParser["SECTION_TITLE_OVERRIDES"].GetAsStringArray());
            var orderOverrides = ParseOrderOverrides(programParser["SECTION_ORDER_OVERRIDES"].GetAsStringArray(), logger);
            var languageMap = BuildLanguageMap(programParser["CODE_LANGUAGE_OVERRIDES"].GetAsStringArray(), logger);

            var matcher = BuildMatcher(ignorePatterns);

            int totalFiles = 0;
            int totalDirs = 0;
            var excludedFiles = new List<ExcludedItemPlan>();
            var excludedDirs = new List<ExcludedItemPlan>();
            var includedFiles = new List<SourceFilePlan>();

            var effectiveSectionDepth = Math.Clamp(sectionDepth, 1, s_allSectionCommands.Length);
            var maxDepthIndex = effectiveSectionDepth - 1;

            var rootNodes = WalkDirectory(
                sourceDir,
                sourceDir,
                depth: 0,
                matcher,
                ignorePatterns,
                includeExts,
                titleOverrides,
                orderOverrides,
                languageMap,
                maxDepthIndex,
                ref totalFiles,
                ref totalDirs,
                logger
            );

            foreach (var node in TraverseNodes(rootNodes)) {
                if (node.IsDirectory) {
                    if (node.Status == SourceInclusionStatus.Excluded) {
                        excludedDirs.Add(new ExcludedItemPlan(
                            node.Info,
                            node.RelativePath,
                            true,
                            node.Depth,
                            node.ExclusionReason,
                            node.ExclusionExplanation ?? "未收录"
                        ));
                    }
                } else {
                    if (node.Status == SourceInclusionStatus.Included) {
                        includedFiles.Add(new SourceFilePlan(
                            (FileInfo)node.Info,
                            node.RelativePath,
                            node.DisplayTitle,
                            node.Depth,
                            node.EffectiveDepth,
                            node.SectionCommand ?? "section",
                            node.EffectiveLanguage ?? "text",
                            node.IsRawLatex,
                            node.OrderWeight
                        ));
                    } else {
                        excludedFiles.Add(new ExcludedItemPlan(
                            node.Info,
                            node.RelativePath,
                            false,
                            node.Depth,
                            node.ExclusionReason,
                            node.ExclusionExplanation ?? "未收录",
                            RecognizedLanguage: node.IsRecognizedLanguage ? node.EffectiveLanguage : null
                        ));
                    }
                }
            }

            int maxDepth = includedFiles.Count > 0 ? includedFiles.Max(f => f.Depth) : 0;

            return new SourceTreePlan(
                sourceDir,
                totalFiles,
                totalDirs,
                includedFiles,
                excludedFiles,
                excludedDirs,
                rootNodes,
                maxDepth
            );
        }

        public static SourceTreePlan Build(
            DirectoryInfo sourceDir,
            Core.Models.ProjectConfiguration config,
            int sectionDepth = 5,
            ILogger? logger = null
        ) => Build(sourceDir, config.ProgramParser, sectionDepth, logger);

        private static List<SourceNodePlan> WalkDirectory(
            DirectoryInfo root,
            DirectoryInfo currentDir,
            int depth,
            Matcher matcher,
            IReadOnlyList<string> ignorePatterns,
            HashSet<string> includeExts,
            IReadOnlyDictionary<string, string> titleOverrides,
            IReadOnlyDictionary<string, int> orderOverrides,
            IReadOnlyDictionary<string, string> languageMap,
            int maxDepthIndex,
            ref int totalFiles,
            ref int totalDirs,
            ILogger? logger
        ) {
            var nodes = new List<SourceNodePlan>();

            // 1. 处理子目录
            DirectoryInfo[] subDirs;
            try {
                subDirs = currentDir.GetDirectories();
            } catch (Exception ex) {
                logger?.Warning($"无法读取目录 \"{currentDir.FullName}\": {ex.Message}");
                return nodes;
            }

            foreach (var subDir in subDirs) {
                var relPath = Path.GetRelativePath(root.FullName, subDir.FullName).Replace('\\', '/');
                var displayTitle = ResolveTitle(subDir.Name, relPath, titleOverrides);
                var orderWeight = ResolveOrderWeight(subDir.Name, relPath, orderOverrides);
                var dirNode = new SourceNodePlan(subDir, relPath, isDirectory: true, depth, displayTitle, orderWeight);

                if (subDir.Name.StartsWith('.')) {
                    dirNode.Status = SourceInclusionStatus.Excluded;
                    dirNode.ExclusionReason = SourceExclusionReason.Hidden;
                    dirNode.ExclusionExplanation = "以 \".\" 开头的隐藏目录";
                    nodes.Add(dirNode);
                    continue;
                }

                totalDirs++;

                if (depth == 0 && subDir.Name.Equals("build", StringComparison.OrdinalIgnoreCase)) {
                    dirNode.Status = SourceInclusionStatus.Excluded;
                    dirNode.ExclusionReason = SourceExclusionReason.IgnorePattern;
                    dirNode.ExclusionExplanation = "匹配排除模式 (build 默认输出目录)";
                    nodes.Add(dirNode);
                    continue;
                }

                if (IsIgnored(relPath, matcher, out var matchedPat)) {
                    dirNode.Status = SourceInclusionStatus.Excluded;
                    dirNode.ExclusionReason = SourceExclusionReason.IgnorePattern;
                    dirNode.ExclusionExplanation = $"命中排除规则 (模式：{matchedPat ?? "ignore_patterns"})";
                    nodes.Add(dirNode);
                    continue;
                }

                var children = WalkDirectory(
                    root,
                    subDir,
                    depth + 1,
                    matcher,
                    ignorePatterns,
                    includeExts,
                    titleOverrides,
                    orderOverrides,
                    languageMap,
                    maxDepthIndex,
                    ref totalFiles,
                    ref totalDirs,
                    logger
                );
                dirNode.Children.AddRange(children);

                if (!dirNode.HasIncludedDescendants) {
                    dirNode.Status = SourceInclusionStatus.Excluded;
                    dirNode.ExclusionReason = SourceExclusionReason.EmptyDirectory;
                    dirNode.ExclusionExplanation = "空目录或内部所有子项均已被排除";
                } else {
                    dirNode.Status = SourceInclusionStatus.Included;
                    var effectiveDepth = Math.Min(depth, maxDepthIndex);
                    dirNode.EffectiveDepth = effectiveDepth;
                    dirNode.SectionCommand = s_allSectionCommands[effectiveDepth];
                }

                nodes.Add(dirNode);
            }

            // 2. 处理文件
            FileInfo[] files;
            try {
                files = currentDir.GetFiles();
            } catch (Exception ex) {
                logger?.Warning($"无法读取目录中的文件 \"{currentDir.FullName}\": {ex.Message}");
                return nodes;
            }

            foreach (var file in files) {
                var relPath = Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');
                var displayTitle = ResolveTitle(file.Name, relPath, titleOverrides);
                var orderWeight = ResolveOrderWeight(file.Name, relPath, orderOverrides);
                var fileNode = new SourceNodePlan(file, relPath, isDirectory: false, depth, displayTitle, orderWeight);

                if (file.Name.StartsWith('.')) {
                    fileNode.Status = SourceInclusionStatus.Excluded;
                    fileNode.ExclusionReason = SourceExclusionReason.Hidden;
                    fileNode.ExclusionExplanation = "以 \".\" 开头的隐藏文件";
                    nodes.Add(fileNode);
                    continue;
                }

                totalFiles++;

                if (IsIgnored(relPath, matcher, out var matchedPat)) {
                    fileNode.Status = SourceInclusionStatus.Excluded;
                    fileNode.ExclusionReason = SourceExclusionReason.IgnorePattern;
                    fileNode.ExclusionExplanation = $"命中排除规则 (模式：{matchedPat ?? "ignore_patterns"})";
                    nodes.Add(fileNode);
                    continue;
                }

                var rawExt = file.Extension;
                var extKey = rawExt.StartsWith('.') ? rawExt[1..] : rawExt;
                bool isRecognizedLang = languageMap.TryGetValue(extKey, out var lang);
                fileNode.IsRecognizedLanguage = isRecognizedLang;

                if (!includeExts.Contains(rawExt)) {
                    fileNode.Status = SourceInclusionStatus.Excluded;
                    fileNode.ExclusionReason = SourceExclusionReason.ExtensionNotIncluded;
                    if (isRecognizedLang) {
                        fileNode.ExclusionExplanation = $"扩展名 \"{rawExt}\" 未在包含白名单中（系统已支持该语言 \"{lang}\"，可在 include_file_types 中添加）";
                    } else {
                        fileNode.ExclusionExplanation = $"扩展名 \"{rawExt}\" 未在 include_file_types 包含白名单中";
                    }
                    nodes.Add(fileNode);
                    continue;
                }

                // 纳入排版（深度超过命令级数时自动 clamp 到最后一层）
                fileNode.Status = SourceInclusionStatus.Included;
                var effectiveDepth = Math.Min(depth, maxDepthIndex);
                fileNode.EffectiveDepth = effectiveDepth;
                fileNode.SectionCommand = s_allSectionCommands[effectiveDepth];

                bool isExplicitRawLatex = false;
                if (isRecognizedLang) {
                    if (string.Equals(lang, "raw", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(lang, "latex-raw", StringComparison.OrdinalIgnoreCase)) {
                        isExplicitRawLatex = true;
                    }
                } else if (string.Equals(extKey, "tex", StringComparison.OrdinalIgnoreCase)) {
                    isExplicitRawLatex = true;
                }

                fileNode.IsRawLatex = isExplicitRawLatex;
                fileNode.EffectiveLanguage = isExplicitRawLatex
                    ? "raw LaTeX"
                    : (!string.IsNullOrWhiteSpace(lang) ? lang : "text");

                nodes.Add(fileNode);
            }

            // 3. 排序：子目录与文件统一先按自定义权重，再按自然字母序
            return nodes
                .OrderBy(n => n.IsDirectory ? 0 : 1) // 目录优先
                .ThenBy(n => n.OrderWeight)
                .ThenBy(n => n.Info.Name, NaturalStringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsIgnored(string relativePath, Matcher matcher, out string? matchedPattern) {
            matchedPattern = null;
            var matchResult = matcher.Match(relativePath);
            return !matchResult.HasMatches;
        }

        private static Matcher BuildMatcher(IReadOnlyList<string> ignorePatterns) {
            var matcher = new Matcher();
            matcher.AddInclude("**/*");
            matcher.AddInclude("*");
            foreach (var pattern in ignorePatterns) {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                matcher.AddExclude(pattern);
                if (!pattern.Contains('/')) {
                    matcher.AddExclude($"**/{pattern}");
                    matcher.AddExclude($"{pattern}/**");
                    matcher.AddExclude($"**/{pattern}/**");
                } else if (pattern.EndsWith("/**")) {
                    matcher.AddExclude(pattern[..^3]);
                }
            }
            return matcher;
        }

        /// <summary>
        /// 按照最终 LaTeX 输出顺序获取所有包含有效内容的项（供 CodeBlockGenerator 遍历）。
        /// 保证生成与预览走完全一致的流式结构。
        /// </summary>
        public IEnumerable<(SourceNodePlan Node, bool IsDirectory)> GetInOrderEntries() {
            var pendingDirs = new List<SourceNodePlan>();
            foreach (var entry in TraverseNodes(RootNodes)) {
                if (entry.IsDirectory) {
                    if (entry.Status == SourceInclusionStatus.Included) {
                        yield return (entry, true);
                    }
                } else if (entry.Status == SourceInclusionStatus.Included) {
                    yield return (entry, false);
                }
            }
        }

        private static IEnumerable<SourceNodePlan> TraverseNodes(IEnumerable<SourceNodePlan> nodes) {
            foreach (var node in nodes) {
                yield return node;
                if (node.IsDirectory && node.Children.Count > 0) {
                    foreach (var child in TraverseNodes(node.Children)) {
                        yield return child;
                    }
                }
            }
        }
    }
}
