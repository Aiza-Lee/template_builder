using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
    /// <summary>
    /// 构建流水线的最终装配点：读取配置 JSON、构造两个 <see cref="ConfigParser"/>、
    /// 调用 <see cref="PdfBuilder.Build"/>，并把领域异常映射为退出码。
    /// </summary>
    internal sealed class BuildPipelineRunner(ILogger logger, ManifestResourceManager resMgr) {
        private readonly ILogger _logger = logger;
        private readonly ManifestResourceManager _resMgr = resMgr;

        /// <summary>
        /// 执行一次构建。捕获 <see cref="MalformedConfigException"/>、
        /// <see cref="MissingEmbeddedResourceException"/> 与 <see cref="UnknownConfigKeyException"/>，
        /// 分别返回 <see cref="ExitCodes.MalformedConfig"/>、
        /// <see cref="ExitCodes.MissingEmbeddedResource"/> 与 <see cref="ExitCodes.InvalidArguments"/>。
        /// </summary>
        public int Run(BuildSubcommandOptions options, bool userProvidedConfig) {
            try {
                var strictness = userProvidedConfig ? ConfigStrictness.Strict : ConfigStrictness.Lax;
                var texParser = new ConfigParser("TEX", _logger, strictness);
                var programParser = new ConfigParser("PROGRAM", _logger, strictness);

                var configJson = File.ReadAllText(options.ConfigFile.FullName);
                texParser.ParseConfigFile(configJson, options.ConfigFile.FullName);
                programParser.ParseConfigFile(configJson, options.ConfigFile.FullName);

                ApplyConfigOverrides(options.ConfigOverrides, texParser, programParser, _logger);

                return new PdfBuilder(_logger, options, texParser, programParser, _resMgr).Build();
            } catch (UnknownConfigKeyException ex) {
                _logger.Error(ex.Message);
                return ExitCodes.InvalidArguments;
            } catch (MalformedConfigException ex) {
                var where = ex.SourcePath != null ? $" (source: {ex.SourcePath})" : string.Empty;
                _logger.Error($"{ex.Message}{where}");
                return ExitCodes.MalformedConfig;
            } catch (MissingEmbeddedResourceException ex) {
                _logger.Error(ex.Message);
                return ExitCodes.MissingEmbeddedResource;
            }
        }

        /// <summary>
        /// 把运行时配置覆盖应用到两个解析器上。
        /// <para>
        /// 键是<b>相对各自根对象</b>的（如 <c>LAYOUT_COLUMNS</c>、<c>BUILD_PASS_COUNT</c>），
        /// 因此先试 TEX 解析器再试 PROGRAM 解析器。两个根对象的键集在实践中不相交
        /// （TEX 与 PROGRAM 分组不同），故先命中即命中；两者都不认该键时记 Warning 并跳过，
        /// 避免把键名拼写错误静默吞掉。
        /// </para>
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
                    logger.Warning($"[未知配置项] \"{key}\" 不在默认配置中，覆盖已跳过。");
                }
            }
        }
    }
}
