using System.Diagnostics;
using System.Text;
using Utils;

namespace Core {
    /// <summary>
    /// xelatex 子进程单次运行结果。<see cref="TimedOut"/> 为 true 时 <see cref="ExitCode"/> 无意义（进程被 Kill）。
    /// </summary>
    internal record XelatexResult(int ExitCode, string Stderr, bool TimedOut);

    /// <summary>
    /// xelatex 子进程的抽象。提取此接口为了让 <see cref="PdfBuilder"/> 的 xelatex 路径可单测（注入 fake runner），
    /// 同时支持 future 切换（如并行 xelatex、跨进程缓存等）。
    /// </summary>
    internal interface IXelatexRunner {
        /// <summary>
        /// 同步运行一次 xelatex 子进程，等待其退出或超时。
        /// </summary>
        /// <param name="workingDir">子进程的工作目录</param>
        /// <param name="arguments">完整的 xelatex 参数串</param>
        /// <param name="timeoutSeconds">超时秒数；&lt;= 0 表示不限时</param>
        /// <param name="extraFontDirs">需要额外追加到 OSFONTDIR 环境变量的自定义字体目录列表</param>
        XelatexResult Run(string workingDir, string arguments, int timeoutSeconds, IEnumerable<string>? extraFontDirs = null);
    }

    /// <summary>
    /// 默认 <see cref="IXelatexRunner"/> 实现：用 <see cref="Process"/> 同步 spawn xelatex。
    /// 把 stderr 合并到返回值而非留给调用方按行处理。
    /// </summary>
    internal class XelatexRunner : IXelatexRunner {
        private readonly ILogger _logger;

        public XelatexRunner(ILogger logger) {
            _logger = logger;
        }

        public XelatexResult Run(string workingDir, string arguments, int timeoutSeconds, IEnumerable<string>? extraFontDirs = null) {
            var stderr = new StringBuilder();
            using var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "xelatex",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir
                }
            };

            // 注入自定义字体目录到 OSFONTDIR 环境变量，使得 XeTeX 能直接在免安装情况下识别本地字体
            if (extraFontDirs != null) {
                var validDirs = extraFontDirs.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)).Distinct().ToList();
                if (validDirs.Count > 0) {
                    var existing = Environment.GetEnvironmentVariable("OSFONTDIR") ?? "";
                    var sep = Path.PathSeparator;
                    var joined = string.Join(sep.ToString(), validDirs);
                    proc.StartInfo.EnvironmentVariables["OSFONTDIR"] = string.IsNullOrEmpty(existing)
                        ? joined
                        : $"{joined}{sep}{existing}";
                }
            }

            proc.ErrorDataReceived += (_, a) => {
                if (a.Data != null) {
                    _logger.Debug(a.Data);
                    stderr.AppendLine(a.Data);
                }
            };
            proc.OutputDataReceived += (_, a) => {
                if (a.Data != null) {
                    _logger.Debug(a.Data);
                }
            };

            // .NET 9 的 Process.Start() 实例方法在失败时抛 Win32Exception
            // （如 xelatex 不在 PATH），不再返回 false。把异常包成 XelatexResult
            // 让上层 PdfBuilder.CompileTexToPdf 走 XelatexFailure 路径。
            try {
                proc.Start();
            } catch (Exception ex) {
                _logger.Error($"启动 xelatex 失败：{ex.Message}");
                _logger.Error("排查指引：未检测到 xelatex 命令或无法启动。请确认已安装 TeX 发行版（如 TeX Live、MacTeX 或 MiKTeX），并将 xelatex 所在 bin 目录添加至系统环境变量 PATH。");
                return new XelatexResult(-1, stderr.ToString() + $"[failed to start: {ex.Message}]\n", false);
            }

            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            bool exited;
            if (timeoutSeconds > 0) {
                exited = proc.WaitForExit(timeoutSeconds * 1000);
            } else {
                proc.WaitForExit();
                exited = true;
            }

            if (!exited) {
                _logger.Error($"xelatex 运行超时（{timeoutSeconds} 秒），正在终止进程树。");
                try {
                    proc.Kill(entireProcessTree: true);
                } catch (Exception ex) {
                    _logger.Warning($"终止 xelatex 失败（可能已退出）：{ex.Message}");
                }
                // 兜底：再等 2s 让 Kill 生效（防 zombie 进程）
                proc.WaitForExit(2000);
                return new XelatexResult(-1, stderr.ToString() + $"[killed: timeout {timeoutSeconds}s]\n", true);
            }

            return new XelatexResult(proc.ExitCode, stderr.ToString(), false);
        }
    }
}
