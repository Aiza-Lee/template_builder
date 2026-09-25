using System.Text;
using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// 单行与多行文本编辑器纯逻辑状态机（支持光标任意行列移动、插入、删除、换行与全角字符宽度计算）。
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

        /// <summary>
        /// 将当前缓冲区按换行拆分为行列表（规范化消除 \r）。
        /// </summary>
        public IReadOnlyList<string> GetLines() {
            var raw = _buffer.ToString();
            if (raw.Length == 0) return [""];
            var result = new List<string>();
            int start = 0;
            for (int i = 0; i < raw.Length; i++) {
                if (raw[i] == '\n') {
                    int len = i - start;
                    if (len > 0 && raw[i - 1] == '\r') len--;
                    result.Add(raw.Substring(start, len));
                    start = i + 1;
                }
            }
            result.Add(raw.Substring(start));
            return result;
        }

        public int LineCount => GetLines().Count;

        /// <summary>
        /// 获取光标在多行缓冲区中的 (Line, Column) 坐标（均为 0-indexed）。
        /// </summary>
        public (int Line, int Column) GetCursorPosition() {
            int line = 0;
            int col = 0;
            for (int i = 0; i < _cursor; i++) {
                if (_buffer[i] == '\n') {
                    line++;
                    col = 0;
                } else if (_buffer[i] != '\r') {
                    col++;
                }
            }
            return (line, col);
        }

        public int CursorLineIndex => GetCursorPosition().Line;
        public int CursorColumnIndex => GetCursorPosition().Column;

        /// <summary>
        /// 获取目标 (targetLine, targetCol) 对应的内部缓冲区字符索引。
        /// </summary>
        public int GetBufferIndexFromPosition(int targetLine, int targetCol) {
            int currentLine = 0;
            int lineStart = 0;
            for (int i = 0; i < _buffer.Length; i++) {
                if (currentLine == targetLine) {
                    break;
                }
                if (_buffer[i] == '\n') {
                    currentLine++;
                    lineStart = i + 1;
                }
            }

            int lineEnd = _buffer.Length;
            for (int i = lineStart; i < _buffer.Length; i++) {
                if (_buffer[i] == '\n' || _buffer[i] == '\r') {
                    lineEnd = i;
                    break;
                }
            }

            int lineLen = lineEnd - lineStart;
            return lineStart + Math.Clamp(targetCol, 0, lineLen);
        }

        public void MoveLeft() {
            if (_cursor > 0) _cursor--;
        }

        public void MoveRight() {
            if (_cursor < _buffer.Length) _cursor++;
        }

        public void MoveUp() {
            var (line, col) = GetCursorPosition();
            if (line > 0) {
                _cursor = GetBufferIndexFromPosition(line - 1, col);
            }
        }

        public void MoveDown() {
            var (line, col) = GetCursorPosition();
            var lines = GetLines();
            if (line < lines.Count - 1) {
                _cursor = GetBufferIndexFromPosition(line + 1, col);
            }
        }

        public void MoveHome(bool entireText = false) {
            if (entireText || _cursor == 0) {
                _cursor = 0;
                return;
            }
            for (int i = _cursor - 1; i >= 0; i--) {
                if (_buffer[i] == '\n') {
                    _cursor = i + 1;
                    return;
                }
            }
            _cursor = 0;
        }

        public void MoveEnd(bool entireText = false) {
            if (entireText || _cursor == _buffer.Length) {
                _cursor = _buffer.Length;
                return;
            }
            for (int i = _cursor; i < _buffer.Length; i++) {
                if (_buffer[i] == '\n' || _buffer[i] == '\r') {
                    _cursor = i;
                    return;
                }
            }
            _cursor = _buffer.Length;
        }

        public void Insert(char ch) {
            _buffer.Insert(_cursor, ch);
            _cursor++;
        }

        public void Insert(string text) {
            if (string.IsNullOrEmpty(text)) return;
            var normalized = text.Replace("\r\n", "\n");
            _buffer.Insert(_cursor, normalized);
            _cursor += normalized.Length;
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
        /// 计算光标所在当前行内、光标前文本在终端中的视觉显示列宽（考虑全角 CJK 字符与半角字符）。
        /// </summary>
        public int CursorColumnDisplayWidth {
            get {
                var lines = GetLines();
                var (lineIdx, colIdx) = GetCursorPosition();
                if (lineIdx < 0 || lineIdx >= lines.Count) return 0;
                var currentLine = lines[lineIdx];
                var prefix = currentLine.Substring(0, Math.Clamp(colIdx, 0, currentLine.Length));
                return GetDisplayWidth(prefix);
            }
        }

        /// <summary>
        /// 兼容属性：计算当前光标在当前行内的视觉列宽。
        /// </summary>
        public int CursorDisplayWidth => CursorColumnDisplayWidth;

        /// <summary>
        /// 计算当前完整缓冲区的最大单行视觉显示列宽。
        /// </summary>
        public int TotalDisplayWidth {
            get {
                var lines = GetLines();
                if (lines.Count <= 1) return GetDisplayWidth(_buffer.ToString());
                int max = 0;
                foreach (var l in lines) {
                    var w = GetDisplayWidth(l);
                    if (w > max) max = w;
                }
                return max;
            }
        }

        /// <summary>
        /// 计算当前行内光标后剩余文本在终端中的视觉显示列宽。
        /// </summary>
        public int RemainingDisplayWidth {
            get {
                var lines = GetLines();
                var (lineIdx, colIdx) = GetCursorPosition();
                if (lineIdx < 0 || lineIdx >= lines.Count) return 0;
                var currentLine = lines[lineIdx];
                if (colIdx >= currentLine.Length) return 0;
                var remainder = currentLine.Substring(colIdx);
                return GetDisplayWidth(remainder);
            }
        }

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
    /// TUI 交互式行/多行文本编辑器：
    /// 1. 彻底解决标准 TextPrompt 无法使用左/右/上/下箭头移动光标的问题；
    /// 2. 完整支持多行文本编辑与精确光标 (Row, Col) 计算，杜绝多行时光标悬浮错位；
    /// 3. 支持：上下左右箭头、Home/End (含 Ctrl 全文跳跃)、Backspace、Delete、Ctrl+U 清空、Escape 取消、回车提交/换行。
    /// </summary>
    public static class TuiLineEditor {
        static TuiLineEditor() {
            TerminalWidthRegistry.Register("LineEditor", 40);
        }

        public static string? ReadLine(
            string promptMarkup,
            string? initialValue = null,
            Func<string, (bool Valid, string? Error)>? validator = null,
            bool multiline = false
        ) {
            if (!promptMarkup.EndsWith('\n') && !promptMarkup.EndsWith("\r\n")) {
                promptMarkup += "\n";
            }

            if (Console.IsInputRedirected) {
                AnsiConsole.Markup(promptMarkup);
                var raw = Console.ReadLine();
                return string.IsNullOrEmpty(raw) ? initialValue : raw;
            }

            var state = new TuiLineEditorState(initialValue);

            while (true) {
                AnsiConsole.Markup(promptMarkup);

                var (startLeft, startTop) = TuiRedrawer.GetCursorPosition();
                int previousVisualLineCount = 1;
                Redraw(state, startLeft, startTop, ref previousVisualLineCount);

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
                        if (multiline) {
                            if (key.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                                break;
                            }
                            state.Insert('\n');
                            Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                            continue;
                        }
                        if (key.Modifiers.HasFlag(ConsoleModifiers.Shift) || key.Modifiers.HasFlag(ConsoleModifiers.Alt)) {
                            state.Insert('\n');
                            Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                            continue;
                        }
                        break;
                    }

                    if (key.Key == ConsoleKey.Escape) {
                        cancelled = true;
                        break;
                    }

                    if (key.Key == ConsoleKey.LeftArrow) {
                        state.MoveLeft();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.RightArrow) {
                        state.MoveRight();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.UpArrow) {
                        state.MoveUp();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.DownArrow) {
                        state.MoveDown();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.Home || (key.Key == ConsoleKey.A && key.Modifiers.HasFlag(ConsoleModifiers.Control))) {
                        state.MoveHome(entireText: key.Modifiers.HasFlag(ConsoleModifiers.Control));
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.End || (key.Key == ConsoleKey.E && key.Modifiers.HasFlag(ConsoleModifiers.Control))) {
                        state.MoveEnd(entireText: key.Modifiers.HasFlag(ConsoleModifiers.Control));
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.Backspace) {
                        state.Backspace();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.Delete) {
                        state.Delete();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                        state.Clear();
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    } else if (!char.IsControl(key.KeyChar)) {
                        state.Insert(key.KeyChar);
                        Redraw(state, startLeft, startTop, ref previousVisualLineCount);
                    }
                }

                // 退出编辑器时，将终端物理光标移动到渲染块底部下方
                TuiRedrawer.SetCursorPosition(0, startTop + Math.Max(1, previousVisualLineCount));
                Console.WriteLine();

                if (cancelled) {
                    return null;
                }

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

        private static void Redraw(TuiLineEditorState state, int startLeft, int startTop, ref int previousVisualLineCount) {
            var lines = state.GetLines();
            var (cursorLine, cursorCol) = state.GetCursorPosition();
            int winWidth = TuiEnvironment.SafeWindowWidth();
            TuiRedrawer.RedrawEditor(null, startLeft, startTop, lines, cursorLine, cursorCol, winWidth, ref previousVisualLineCount);
        }
    }
}
