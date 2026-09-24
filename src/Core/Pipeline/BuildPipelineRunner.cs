using System.Collections.Generic;
using System.IO;
using Core.Models;
using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
    /// <summary>
    /// 构建流水线的最终装配点：读取配置 JSON、基于统一 <see cref="ProjectConfiguration"/> 管理配置与覆盖项、
    /// 调用 <see cref="PdfBuilder.ExecuteBuild"/>，并把领域异常映射为退出码与结构化结果。
    /// </summary>
    internal sealed class BuildPipelineRunner(ILogger logger, ManifestResourceManager resMgr) {
        private readonly ILogger _logger = logger;
        private readonly ManifestResourceManager _resMgr = resMgr;

        /// <summary>
        /// 执行构建流水线并返回结构化结果。支持传入已预先装配并微调的 <see cref="ProjectConfiguration"/>。
        /// </summary>
        public BuildResult RunBuild(
            BuildSubcommandOptions options,
            bool userProvidedConfig,
            ProjectConfiguration? existingConfig = null
        ) {
            try {
                ProjectConfiguration projectConfig;
                if (existingConfig != null) {
                    projectConfig = existingConfig;
                    if (options.ConfigOverrides is { Count: > 0 }) {
                        ApplyConfigOverrides(options.ConfigOverrides, projectConfig, _logger);
                    }
                } else {
                    var strictness = userProvidedConfig ? ConfigStrictness.Strict : ConfigStrictness.Lax;
                    projectConfig = new ProjectConfiguration(_logger, strictness);

                    if (!options.ConfigFile.Exists) {
                        _logger.Error($"未找到配置文件：{options.ConfigFile.FullName}");
                        return BuildResult.CreateFailure(
                            ExitCodes.InvalidArguments,
                            BuildStatus.Failed,
                            $"未找到配置文件：{options.ConfigFile.FullName}"
                        );
                    }

                    var configJson = File.ReadAllText(options.ConfigFile.FullName);
                    projectConfig.LoadProjectConfig(configJson, options.ConfigFile.FullName);

                    if (options.ConfigOverrides is { Count: > 0 }) {
                        ApplyConfigOverrides(options.ConfigOverrides, projectConfig, _logger);
                    }
                }

                // 统一准入校验 1: 配置合法性校验（类型、枚举、范围、正则约束及 LaTeX 尺寸）
                var configValidation = Core.Validation.ConfigValidator.Validate(projectConfig);
                if (!configValidation.IsValid) {
                    foreach (var err in configValidation.Errors) {
                        _logger.Error($"[配置校验失败] {err.PropertyPath}: {err.Message} 当前值: \"{err.CurrentValue}\"，期望: {err.Expected}。建议: {err.Recommendation}");
                    }
                    var firstErr = configValidation.Errors[0];
                    return BuildResult.CreateFailure(
                        ExitCodes.InvalidArguments,
                        BuildStatus.Failed,
                        $"配置校验失败：{firstErr.Message}（期望：{firstErr.Expected}；建议：{firstErr.Recommendation}）",
                        diagnostics: configValidation.Errors.Select(e => $"{e.PropertyPath}: {e.Message}").ToList()
                    );
                }

                // 统一准入校验 2: 源文件预检（目录存在性、空目录、全过滤、超深目录）
                var preflight = Core.Validation.SourcePreflight.Inspect(options.SourceDir, projectConfig, _logger);
                if (!preflight.IsValid) {
                    _logger.Error($"[源文件预检失败] {preflight.Message} 建议: {preflight.Recommendation}");
                    return BuildResult.CreateFailure(
                        preflight.ExitCode,
                        BuildStatus.Failed,
                        $"{preflight.Message}（建议：{preflight.Recommendation}）",
                        diagnostics: [preflight.Message]
                    );
                }

                return new PdfBuilder(_logger, options, projectConfig, _resMgr).ExecuteBuild();
            } catch (UnknownConfigKeyException ex) {
                _logger.Error(ex.Message);
                return BuildResult.CreateFailure(ExitCodes.InvalidArguments, BuildStatus.Failed, ex.Message);
            } catch (MalformedConfigException ex) {
                var where = ex.SourcePath != null ? $" (source: {ex.SourcePath})" : string.Empty;
                _logger.Error($"{ex.Message}{where}");
                return BuildResult.CreateFailure(ExitCodes.MalformedConfig, BuildStatus.Failed, $"{ex.Message}{where}");
            } catch (MissingEmbeddedResourceException ex) {
                _logger.Error(ex.Message);
                return BuildResult.CreateFailure(ExitCodes.MissingEmbeddedResource, BuildStatus.Failed, ex.Message);
            } catch (InvalidCastException ex) {
                _logger.Error($"[配置类型转换错误] {ex.Message}");
                return BuildResult.CreateFailure(ExitCodes.InvalidArguments, BuildStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 执行一次构建并返回退出码。保持与旧调用点兼容。
        /// </summary>
        public int Run(BuildSubcommandOptions options, bool userProvidedConfig) {
            return RunBuild(options, userProvidedConfig).ExitCode;
        }

        /// <summary>
        /// 把运行时配置覆盖应用到统一项目配置模型上。
        /// </summary>
        internal static void ApplyConfigOverrides(
            IReadOnlyDictionary<string, string>? overrides,
            ProjectConfiguration configuration,
            ILogger logger
        ) {
            if (overrides is not { Count: > 0 }) return;

            foreach (var (key, value) in overrides) {
                var applied = configuration.TrySetOverride(key, value);
                if (!applied) {
                    if (configuration.TexParser.ContainsKey(key) || configuration.ProgramParser.ContainsKey(key)) {
                        logger.Warning($"[配置覆盖失败] \"{key}\" 的值 \"{value}\" 无法转换为对应数据类型，覆盖已跳过。");
                    } else {
                        logger.Warning($"[未知配置项] \"{key}\" 不在默认配置中，覆盖已跳过。");
                    }
                }
            }
        }

        /// <summary>
        /// 兼容旧版基于两组独立解析器的覆盖应用重载。
        /// </summary>
        internal static void ApplyConfigOverrides(
            IReadOnlyDictionary<string, string>? overrides,
            ConfigParser texParser,
            ConfigParser programParser,
            ILogger logger
        ) {
            if (overrides is not { Count: > 0 }) return;

            foreach (var (key, value) in overrides) {
                var applied = texParser.TrySetConfigValueFromString(key, value)
                           || programParser.TrySetConfigValueFromString(key, value);
                if (!applied) {
                    if (texParser.ContainsKey(key) || programParser.ContainsKey(key)) {
                        logger.Warning($"[配置覆盖失败] \"{key}\" 的值 \"{value}\" 无法转换为对应数据类型，覆盖已跳过。");
                    } else {
                        logger.Warning($"[未知配置项] \"{key}\" 不在默认配置中，覆盖已跳过。");
                    }
                }
            }
        }
    }
}
