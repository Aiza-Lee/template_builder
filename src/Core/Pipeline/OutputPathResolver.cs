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
                throw new InvalidArgumentException("源文件目录无效。");
            }
            if (!requested.Exists) {
                throw new InvalidArgumentException($"未找到源文件目录 \"{requested.FullName}\"。");
            }
            return requested;
        }

        /// <summary>
        /// 规范化输出路径：建父目录、强制 .pdf 后缀。
        /// </summary>
        public FileInfo ResolveOutputPdf(FileInfo? requested) {
            if (requested == null || requested.Directory == null) {
                throw new InvalidArgumentException("输出文件路径无效。");
            }
            if (!requested.Directory.Exists) {
                _logger.Warning($"未找到输出目录 \"{requested.Directory.FullName}\"，已由程序自动创建。");
                requested.Directory.Create();
            }
            var pdfFileName = Path.GetFileNameWithoutExtension(requested.Name) + ".pdf";
            var resolved = new FileInfo(Path.Combine(requested.Directory.FullName, pdfFileName));
            if (!string.Equals(requested.Name, resolved.Name, StringComparison.OrdinalIgnoreCase)) {
                _logger.Info($"输出路径后缀已自动更正为 .pdf：\"{resolved.FullName}\"。");
            }
            return resolved;
        }
    }
}
