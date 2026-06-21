using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
	/// <summary>
	/// 校验并规范化 `-s` 与 `-o` 选项的路径。
	/// 失败时抛 <see cref="InvalidArgumentException"/>，由调用方映射为 <see cref="ExitCodes.InvalidArguments"/>。
	/// </summary>
	internal sealed class OutputPathResolver(ILogger logger) {
		private readonly ILogger _logger = logger;

		/// <summary>
		/// 校验源文件目录：非 null 且必须存在。
		/// </summary>
		public DirectoryInfo ResolveSourceDir(DirectoryInfo? requested) {
			if (requested == null) {
				throw new InvalidArgumentException("Invalid source files folder.");
			}
			if (!requested.Exists) {
				throw new InvalidArgumentException($"Source files folder \"{requested.FullName}\" not found.");
			}
			return requested;
		}

		/// <summary>
		/// 规范化输出路径：建父目录、强制 .pdf 后缀。
		/// </summary>
		public FileInfo ResolveOutputPdf(FileInfo? requested) {
			if (requested == null || requested.Directory == null) {
				throw new InvalidArgumentException("Invalid output file path.");
			}
			if (!requested.Directory.Exists) {
				_logger.Warning($"Output directory \"{requested.Directory.FullName}\" not found, created by the program.");
				requested.Directory.Create();
			}
			var pdfFileName = Path.GetFileNameWithoutExtension(requested.Name) + ".pdf";
			return new FileInfo(Path.Combine(requested.Directory.FullName, pdfFileName));
		}
	}
}
