using System.Text;
using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// 单行文本编辑器纯逻辑状态机（支持光标任意位置移动、插入、删除与全角字符宽度计算）。
    /// 解耦终端 I/O，便于 100% 单元测试。
    /// </summary>
    public sealed class TuiLineEditorState {
        private readonly StringBuilder _buffer;
        private int _cursor;

        public TuiLineEditorState(string? initial = null) {
            _buffer = new StringBuilder(initial ?? string.Empty);
            _cursor = _buffer.Length;
        }

        public string Text => _buffer.ToString();
        public int Length => _buffer.Length;
        public int Cursor => _cursor;

        public void MoveLeft() {
            if (_cursor > 0) _cursor--;
        }

        public void MoveRight() {
            if (_cursor < _buffer.Length) _cursor++;
        }

        public void MoveHome() {
            _cursor = 0;
        }

        public void MoveEnd() {
            _cursor = _buffer.Length;
        }

        public void Insert(char ch) {
            _buffer.Insert(_cursor, ch);
            _cursor++;
        }

        public void Insert(string text) {
            if (string.IsNullOrEmpty(text)) return;
            _buffer.Insert(_cursor, text);
            _cursor += text.Length;
        }

        public void Backspace() {
            if (_cursor > 0) {
                _cursor--;
                _buffer.Remove(_cursor, 1);
            }
        }

        public void Delete() {
            if (_cursor < _buffer.Length) {
                _buffer.Remove(_cursor, 1);
            }
        }

        public void Clear() {
            _buffer.Clear();
            _cursor = 0;
        }

        /// <summary>
        /// 计算光标前文本在终端中的视觉显示列宽（考虑中日韩全角字符与半角字符）。
        /// </summary>
        public int CursorDisplayWidth => GetDisplayWidth(_buffer.ToString(0, _cursor));

        /// <summary>
        /// 计算当前完整缓冲区文本在终端中的视觉显示列宽。
        /// </summary>
        public int TotalDisplayWidth => GetDisplayWidth(_buffer.ToString());

        /// <summary>
        /// 计算光标后剩余文本在终端中的视觉显示列宽。
        /// </summary>
        public int RemainingDisplayWidth => GetDisplayWidth(_buffer.ToString(_cursor, _buffer.Length - _cursor));

        public static int GetDisplayWidth(string? s) {
            if (string.IsNullOrEmpty(s)) return 0;
            int width = 0;
            foreach (var ch in s) {
                // East Asian Wide / Fullwidth characters
                if (ch >= 0x1100 && (
                    (ch >= 0x1100 && ch <= 0x115F) ||
                    (ch >= 0x2E80 && ch <= 0xA4CF && ch != 0x303F) ||
                    (ch >= 0xAC00 && ch <= 0xD7A3) ||
                    (ch >= 0xF900 && ch <= 0xFAFF) ||
                    (ch >= 0xFE10 && ch <= 0xFE19) ||
                    (ch >= 0xFE30 && ch <= 0xFE6F) ||
                    (ch >= 0xFF00 && ch <= 0xFF60) ||
                    (ch >= 0xFFE0 && ch <= 0xFFE6))) {
                    width += 2;
                } else {
                    width += 1;
                }
            }
            return width;
        }
    }

    /// <summary>
    /// TUI 交互式单行编辑器：彻底解决标准 TextPrompt 无法使用左/右箭头移动光标的问题。
    /// 支持：左/右箭头、Home/End、Backspace、Delete、Ctrl+U 清空、Escape 取消、回车提交。
    /// </summary>
    public static class TuiLineEditor {
        public static string? ReadLine(
            string promptMarkup,
            string? initialValue = null,
            Func<string, (bool Valid, string? Error)>? validator = null
        ) {
            if (Console.IsInputRedirected) {
                AnsiConsole.Markup(promptMarkup);
                var raw = Console.ReadLine();
                return string.IsNullOrEmpty(raw) ? initialValue : raw;
            }

            var state = new TuiLineEditorState(initialValue);

            while (true) {
                AnsiConsole.Markup(promptMarkup);

                var startLeft = 0;
                var startTop = 0;
                try {
                    startLeft = Console.CursorLeft;
                    startTop = Console.CursorTop;
                } catch {
                    // 非标准环境容错
                }

                Redraw(state, startLeft, startTop);

                var cancelled = false;
                while (true) {
                    ConsoleKeyInfo key;
                    try {
                        key = Console.ReadKey(true);
                    } catch {
                        // 终端不可交互时优雅回退
                        return state.Text;
                    }

                    if (key.Key == ConsoleKey.Enter) {
                        break;
                    }

                    if (key.Key == ConsoleKey.Escape) {
                        cancelled = true;
                        break;
                    }

                    if (key.Key == ConsoleKey.LeftArrow) {
                        state.MoveLeft();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.RightArrow) {
                        state.MoveRight();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.Home || (key.Key == ConsoleKey.A && key.Modifiers.HasFlag(ConsoleModifiers.Control))) {
                        state.MoveHome();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.End || (key.Key == ConsoleKey.E && key.Modifiers.HasFlag(ConsoleModifiers.Control))) {
                        state.MoveEnd();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.Backspace) {
                        state.Backspace();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.Delete) {
                        state.Delete();
                        Redraw(state, startLeft, startTop);
                    } else if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                        state.Clear();
                        Redraw(state, startLeft, startTop);
                    } else if (!char.IsControl(key.KeyChar)) {
                        state.Insert(key.KeyChar);
                        Redraw(state, startLeft, startTop);
                    }
                }

                if (cancelled) {
                    Console.WriteLine();
                    return null;
                }

                Console.WriteLine();
                var result = state.Text;
                if (validator != null) {
                    var (valid, error) = validator(result);
                    if (!valid) {
                        AnsiConsole.MarkupLine($"[red]{Markup.Escape(error ?? "输入内容非法，请重新输入")}[/]");
                        continue;
                    }
                }

                return result;
            }
        }

        private static void Redraw(TuiLineEditorState state, int startLeft, int startTop) {
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0) {
                    Console.SetCursorPosition(startLeft, startTop);
                    Console.Write(state.Text);
                    Console.Write("\x1b[K"); // 清除光标到行尾残留字符
                    Console.SetCursorPosition(startLeft + state.CursorDisplayWidth, startTop);
                    return;
                }
            } catch {
                // 忽略不支持 SetCursorPosition 的环境
            }

            // ANSI 回退机制：写文本后，利用光标向左回退保持精确位置
            Console.Write("\r\x1b[" + (startLeft + 1) + "G");
            Console.Write(state.Text);
            Console.Write("\x1b[K");
            var remaining = state.RemainingDisplayWidth;
            if (remaining > 0) {
                Console.Write($"\x1b[{remaining}D");
            }
        }
    }
}
