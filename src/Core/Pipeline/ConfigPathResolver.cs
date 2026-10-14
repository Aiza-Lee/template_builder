using System;
using System.IO;
using Core.Localization;
using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
    /// <summary>
    /// 处理 `-c` 选项的路径回退：当用户给出的路径不存在时，回到由
    /// <c>configOption.DefaultValueFactory</c> 计算出的默认路径（用户配置目录下的
    /// <c>config.json</c>，首次运行从嵌入资源拷贝）。同时报告该路径最终是否
    /// 由用户显式提供，以决定 <see cref="ConfigStrictness"/>。
    /// </summary>
    internal sealed class ConfigPathResolver(ILogger logger) {
        private readonly ILogger _logger = logger;

        /// <summary>
        /// 解析 `-c` 选项。
        /// </summary>
        /// <param name="requested">用户提供（或默认值工厂产出）的配置文件路径</param>
        /// <param name="userProvidedAtCli">命令行 token 中是否含 <c>-c</c> / <c>--config</c></param>
        /// <param name="defaultFallback">当 <paramref name="requested"/> 不存在时回退的路径</param>
        /// <returns>已存在的配置文件路径，以及最终是否仍视为「用户提供」（影响严格模式）</returns>
        public (FileInfo ConfigFile, bool UserProvided) Resolve(
            FileInfo? requested,
            bool userProvidedAtCli,
            FileInfo? defaultFallback
        ) => Resolve(requested, userProvidedAtCli, defaultFallback != null ? () => defaultFallback : null);

        /// <summary>
        /// 解析 `-c` 选项（支持惰性求值回退工厂，避免提前触发默认配置生成）。
        /// </summary>
        public (FileInfo ConfigFile, bool UserProvided) Resolve(
            FileInfo? requested,
            bool userProvidedAtCli,
            Func<FileInfo>? defaultFallbackFactory
        ) {
            if (requested != null && requested.Exists) {
                _logger.Info(string.Format(I18n.T("Info.UsingConfigFile", "使用配置文件：\"{0}\"。"), requested.FullName));
                return (requested, userProvidedAtCli);
            }

            if (userProvidedAtCli) {
                var requestedPath = requested?.FullName ?? "<null>";
                throw new InvalidArgumentException(string.Format(I18n.T("Error.SpecifiedConfigNotFound", "指定的配置文件不存在：\"{0}\""), requestedPath));
            }

            if (defaultFallbackFactory == null) {
                // 这分支理论上不可达：应提供默认配置或回退工厂
                throw new InvalidOperationException(I18n.T("Error.DefaultConfigFallbackUnavailable", "默认配置回退不可用。"));
            }
            var fallback = defaultFallbackFactory();
            if (!userProvidedAtCli) {
                _logger.Info(string.Format(I18n.T("Info.UsingConfigFile", "使用配置文件：\"{0}\"。"), fallback.FullName));
            }
            return (fallback, false); // 退回默认配置不再严格
        }
    }
}
