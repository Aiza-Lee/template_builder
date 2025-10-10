using System.Text;
using Utils;

namespace Core {
	internal class PdfBuilder {
		private readonly ILogger _logger;
		private readonly IConfigParser _texConfigParser;
		private readonly IConfigParser _programConfigParser;

		public PdfBuilder(ILogger logger) {
			_logger = logger;
			_texConfigParser = new ConfigParser("TEX", logger);
			_programConfigParser = new ConfigParser("PROGRAM", logger);
		}

		public void Build() {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream("Templates.Main.tex");
			if (stream == null) {
				_logger?.Error("Failed to load embedded TeX template.");
				return;
			}
		}

		/// <summary>
		/// 替换 TeX 模板中的占位符
		/// </summary>
		/// <param name="templateContent">要替换的模板内容</param>
		/// <returns></returns>
		private StringBuilder ReplaceTexPlaceholders(StringBuilder templateContent) {
			foreach (var kvp in _texConfigParser.GetAllConfigs()) {
				var placeholder = $"##{kvp.Key}##";
				templateContent.Replace(placeholder, kvp.Value);
			}
			return templateContent;
		}
	}
}