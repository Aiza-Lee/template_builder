using System;
using System.IO;

namespace Utils {
    /// <summary>
    /// 将诊断日志重定向至 stderr 的 ILogger 包装器，确保 stdout 纯净用于机器可读输出（如 JSON）。
    /// </summary>
    internal sealed class DiagnosticStderrLogger : ILogger {
        private readonly ILogger _inner;
        private LogLevel _level = LogLevel.INFO;
        private readonly TextWriter _errorWriter;

        public DiagnosticStderrLogger(ILogger inner, TextWriter? errorWriter = null) {
            _inner = inner;
            _errorWriter = errorWriter ?? Console.Error;
        }

        public void Info(string message) {
            if (LogLevel.INFO > _level) return;
            _errorWriter.WriteLine(message);
        }

        public void Warning(string message) {
            if (LogLevel.WARNING > _level) return;
            _errorWriter.WriteLine($"[Warning] {message}");
        }

        public void Error(string message) {
            if (LogLevel.ERROR > _level) return;
            _errorWriter.WriteLine($"[Error] {message}");
        }

        public void Debug(string message) {
            if (LogLevel.DEBUG > _level) return;
            _errorWriter.WriteLine($"[Debug] {message}");
        }

        public void SetLevel(LogLevel level) {
            _level = level;
            _inner.SetLevel(level);
        }
    }
}
