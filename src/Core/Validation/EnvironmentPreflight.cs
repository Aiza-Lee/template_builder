using System.IO;
using Core.Models;
using Core.Pipeline;

namespace Core.Validation {
    /// <summary>
    /// 环境预检结果。
    /// </summary>
    public sealed record EnvironmentPreflightResult(
        bool IsValid,
        IReadOnlyList<ValidationCheck> Checks
    );

    /// <summary>
    /// 统一环境依赖预检器：
    /// 预检 xelatex 编译器、pygmentize 语法高亮引擎及本地字体目录的存在性与可用性。
    /// </summary>
    internal static class EnvironmentPreflight {
        /// <summary>
        /// 执行环境与字体预检。
        /// </summary>
        public static EnvironmentPreflightResult Inspect(
            ProjectConfiguration config,
            DirectoryInfo sourceDir,
            bool checkXelatex
        ) {
            var checks = new List<ValidationCheck>();

            if (checkXelatex) {
                var xelatexFound = CheckXelatexOnPath();
                checks.Add(new ValidationCheck("environment", "xelatex", xelatexFound,
                    xelatexFound ? "已在 PATH 中找到" : "未在 PATH 中找到 xelatex（请安装 TeX Live / MacTeX / MiKTeX 并配置 PATH）"));

                var pygmentizeFound = CheckPygmentizeOnPath();
                checks.Add(new ValidationCheck("environment", "pygmentize", pygmentizeFound,
                    pygmentizeFound ? "已在 PATH 中找到" : "未在 PATH 中找到 pygmentize（minted 宏包语法高亮依赖，可通过 pip install Pygments 安装）"));
            }

            // 自定义字体目录检查
            var customDirs = config.TexParser["GLOBAL_CUSTOM_FONT_DIRS"].GetAsStringArray();
            foreach (var customDir in customDirs) {
                if (string.IsNullOrWhiteSpace(customDir)) continue;
                var fullPath = Path.IsPathRooted(customDir)
                    ? Path.GetFullPath(customDir)
                    : Path.GetFullPath(Path.Combine(sourceDir.FullName, customDir));
                var exists = Directory.Exists(fullPath);
                checks.Add(new ValidationCheck("fonts", customDir, exists,
                    exists ? $"字体目录已确认：{fullPath}" : $"未找到指定的自定义字体目录：{fullPath}（请检查路径或创建该目录）"));
            }

            var allOk = checks.All(c => c.Ok);
            return new EnvironmentPreflightResult(allOk, checks);
        }

        /// <summary>
        /// 在 PATH 上查找指定命令的可执行文件。
        /// </summary>
        public static bool CheckExecutableOnPath(string executableName) {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return false;
            var exeNames = OperatingSystem.IsWindows()
                ? [executableName + ".exe", executableName + ".cmd", executableName + ".bat", executableName]
                : new[] { executableName };
            foreach (var dir in pathEnv.Split(Path.PathSeparator)) {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try {
                    foreach (var name in exeNames) {
                        var candidate = Path.Combine(dir.Trim(), name);
                        if (File.Exists(candidate)) return true;
                    }
                } catch {
                    // 忽略异常路径
                }
            }
            return false;
        }

        public static bool CheckXelatexOnPath() => CheckExecutableOnPath("xelatex");

        public static bool CheckPygmentizeOnPath() => CheckExecutableOnPath("pygmentize");
    }
}
