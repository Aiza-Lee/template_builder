using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
	/// <summary>
	/// init 子命令的执行器：把嵌入式 <c>DefaultConfig.jsonc</c> 资源写入用户指定路径。
	/// <para>
	/// <c>--format jsonc</c>（默认）原样复制资源（含 inline <c>//</c> 注释）。<br/>
	/// <c>--format json</c> 走 <see cref="JsonNode"/> 解析再序列化以剥除注释，得到纯 JSON。
	/// </para>
	/// </summary>
	internal sealed class ConfigInitializer(ILogger logger, ManifestResourceManager resMgr) {
		private readonly ILogger _logger = logger;
		private readonly ManifestResourceManager _resMgr = resMgr;

		/// <summary>
		/// 执行 init。返回退出码（0 成功；2 参数错误；5 嵌入式资源缺失）。
		/// </summary>
		public int Run(InitSubcommandOptions options) {
			try {
				// 读嵌入式资源
				var resourceText = _resMgr.GetResourceInString("DefaultConfig.jsonc");
				string outputText = options.Format switch {
					"json" => StripCommentsToJson(resourceText),
					_ => resourceText,  // "jsonc" 或其他默认原样输出
				};

				// 确保父目录存在
				if (options.OutputPath.Directory is { } parent && !parent.Exists) {
					parent.Create();
				}

				File.WriteAllText(options.OutputPath.FullName, outputText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				_logger.Info($"Wrote config file to \"{options.OutputPath.FullName}\".");
				return ExitCodes.Success;
			} catch (MissingEmbeddedResourceException ex) {
				_logger.Error(ex.Message);
				return ExitCodes.MissingEmbeddedResource;
			}
		}

		/// <summary>
		/// 把含 JSONC 注释的文本解析为 <see cref="JsonElement"/> 再缩进序列化为纯 JSON。
		/// 使用 <see cref="JsonDocument"/> 配合 <see cref="JsonCommentHandling.Skip"/> 跳过注释。
		/// </summary>
		internal static string StripCommentsToJson(string jsoncText) {
			var options = new JsonDocumentOptions {
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
			};
			using var doc = JsonDocument.Parse(jsoncText, options);
			return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
		}
	}
}
