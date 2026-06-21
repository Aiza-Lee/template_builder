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
	}
}
