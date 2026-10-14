using System;
using System.Reflection;

namespace Core {
    /// <summary>
    /// 全局统一版本与程序集信息服务。
    /// 从 AssemblyInformationalVersionAttribute 获取单一事实来源，
    /// 确保 CLI --version、TUI Banner、Docker 及构建产物版本完全一致。
    /// </summary>
    internal static class AppVersion {
        private static readonly Lazy<(string Display, string Full, string? Sha, bool IsPrerelease)> s_versionInfo = new(() => {
            var assembly = typeof(AppVersion).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(informational)) {
                var ver = assembly.GetName().Version;
                var fallback = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "2.0.0-rc.1";
                return (fallback, fallback, null, fallback.Contains('-'));
            }

            var plusIndex = informational.IndexOf('+');
            string display;
            string? sha;

            if (plusIndex >= 0) {
                display = informational[..plusIndex];
                sha = informational[(plusIndex + 1)..];
            } else {
                display = informational;
                sha = null;
            }

            var isPrerelease = display.Contains('-');
            return (display, informational, sha, isPrerelease);
        });

        /// <summary>
        /// 语义化展示版本号（如 "2.0.0-rc.1" 或 "2.0.0"），不含 commit sha。
        /// </summary>
        public static string DisplayVersion => s_versionInfo.Value.Display;

        /// <summary>
        /// 完整版本信息（如 "2.0.0-rc.1+a2ddf3a..."）。
        /// </summary>
        public static string FullVersion => s_versionInfo.Value.Full;

        /// <summary>
        /// 源码提交 SHA（若存在）。
        /// </summary>
        public static string? CommitSha => s_versionInfo.Value.Sha;

        /// <summary>
        /// 是否为预发行版本（含有 - 标签，如 -rc.1、-preview 等）。
        /// </summary>
        public static bool IsPrerelease => s_versionInfo.Value.IsPrerelease;
    }
}
