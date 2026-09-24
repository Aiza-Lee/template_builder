using System;
using System.IO;

namespace Utils {
    internal class Logger : ILogger {
        private LogLevel _level = LogLevel.INFO;
        private readonly TextWriter _outWriter;
        private readonly TextWriter _errorWriter;

        public Logger(TextWriter? outWriter = null, TextWriter? errorWriter = null) {
            _outWriter = outWriter ?? Console.Out;
            _errorWriter = errorWriter ?? Console.Error;
        }

        public void Info(string message) {
            if (LogLevel.INFO > _level) return;
            SafeSetColor(ConsoleColor.White);
            _outWriter.WriteLine($"{message}");
            SafeResetColor();
        }

        public void Warning(string message) {
            if (LogLevel.WARNING > _level) return;
            SafeSetColor(ConsoleColor.Yellow);
            _errorWriter.WriteLine($"[Warning] {message}");
            SafeResetColor();
        }

        public void Error(string message) {
            if (LogLevel.ERROR > _level) return;
            SafeSetColor(ConsoleColor.Red);
            _errorWriter.WriteLine($"[Error] {message}");
            SafeResetColor();
        }

        public void Debug(string message) {
            if (LogLevel.DEBUG > _level) return;
            SafeSetColor(ConsoleColor.Cyan);
            _outWriter.WriteLine($"[Debug] {message}");
            SafeResetColor();
        }

        public void SetLevel(LogLevel level) => _level = level;

        private static void SafeSetColor(ConsoleColor color) {
            try {
                if (!Console.IsOutputRedirected && !Console.IsErrorRedirected) {
                    Console.ForegroundColor = color;
                }
            } catch {
                // 忽略非 TTY 或不支持色彩环境下的控制台颜色设置异常
            }
        }

        private static void SafeResetColor() {
            try {
                if (!Console.IsOutputRedirected && !Console.IsErrorRedirected) {
                    Console.ResetColor();
                }
            } catch {
                // 忽略非 TTY 或不支持色彩环境下的控制台颜色设置异常
            }
        }
    }
}