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
		/// <param name="workingDir">子进程的工作目录（当前实现是 AppContext.BaseDirectory）</param>
		/// <param name="arguments">完整的 xelatex 参数串</param>
		/// <param name="timeoutSeconds">超时秒数；&lt;= 0 表示不限时</param>
		XelatexResult Run(string workingDir, string arguments, int timeoutSeconds);
	}

	/// <summary>
	/// 默认 <see cref="IXelatexRunner"/> 实现：用 <see cref="Process"/> 同步 spawn xelatex。
	/// 行为对齐 Round 2 <c>PdfBuilder.RunXelatex</c>，但把 stderr 合并到返回值而非留给调用方按行处理。
	/// </summary>
	internal class XelatexRunner : IXelatexRunner {
		private readonly ILogger _logger;

		public XelatexRunner(ILogger logger) {
			_logger = logger;
		}

		public XelatexResult Run(string workingDir, string arguments, int timeoutSeconds) {
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

			if (!proc.Start()) {
				return new XelatexResult(-1, stderr.ToString(), false);
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
				_logger.Error($"xelatex exceeded timeout ({timeoutSeconds}s); killing process tree.");
				try {
					proc.Kill(entireProcessTree: true);
				} catch (Exception ex) {
					_logger.Warning($"Failed to kill xelatex (may have exited): {ex.Message}");
				}
				// 兜底：再等 2s 让 Kill 生效（防 zombie 进程）
				proc.WaitForExit(2000);
				return new XelatexResult(-1, stderr.ToString() + $"[killed: timeout {timeoutSeconds}s]\n", true);
			}

			return new XelatexResult(proc.ExitCode, stderr.ToString(), false);
		}
	}
}
