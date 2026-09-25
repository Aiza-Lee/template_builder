using System.Text;

namespace Core.Tui {
    /// <summary>
    /// 终端重绘与光标控制模块（Terminal Redrawer）。
    /// <para>
    /// 提供统一的单行与多行文本重绘、光标自由定位、行尾残留擦除、ANSI 回退支持及无头可测试机制。
    /// 为单行编辑器、多行编辑器及终端自适应重绘提供模块化基础支撑。
    /// </para>
    /// </summary>
    internal static class TuiRedrawer {
        public const string AnsiClearLineFromCursor = "\x1b[K";
        public const string AnsiClearEntireLine = "\x1b[2K";
        public const string AnsiHideCursor = "\x1b[?25l";
        public const string AnsiShowCursor = "\x1b[?25h";

        /// <summary>
        /// 获取当前光标位置（带非标准环境安全降级）。
        /// </summary>
        public static (int Left, int Top) GetCursorPosition() {
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0) {
                    return (Console.CursorLeft, Console.CursorTop);
                }
            } catch {
                // 非标准/重定向环境容错
            }
            return (0, 0);
        }

        /// <summary>
        /// 设置光标绝对坐标。
        /// </summary>
        public static void SetCursorPosition(int left, int top, TextWriter? writer = null) {
            var targetWriter = writer ?? Console.Out;
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0 && (writer == null || ReferenceEquals(writer, Console.Out))) {
                    Console.SetCursorPosition(Math.Max(0, left), Math.Max(0, top));
                    return;
                }
            } catch {
                // 忽略 SetCursorPosition 失败，降级为 ANSI 序列
            }

            // ANSI 序列：1-based 坐标
            targetWriter.Write($"\x1b[{Math.Max(1, top + 1)};{Math.Max(1, left + 1)}H");
        }

        /// <summary>
        /// 清除当前行从光标到行尾的残留内容。
        /// </summary>
        public static void ClearFromCursorToEndOfLine(TextWriter? writer = null) {
            var targetWriter = writer ?? Console.Out;
            targetWriter.Write(AnsiClearLineFromCursor);
        }

        /// <summary>
        /// 切换光标可见性。
        /// </summary>
        public static void SetCursorVisible(bool visible, TextWriter? writer = null) {
            var targetWriter = writer ?? Console.Out;
            try {
                if (!Console.IsOutputRedirected && (writer == null || ReferenceEquals(writer, Console.Out))) {
                    Console.CursorVisible = visible;
                    return;
                }
            } catch {
                // 忽略非交互异常，回退为 ANSI 序列
            }
            targetWriter.Write(visible ? AnsiShowCursor : AnsiHideCursor);
        }

        /// <summary>
        /// 计算给定文本逻辑行在特定终端宽度和起始偏移下的视觉布局（视觉行切分、光标绝对行列、总视觉行数）。
        /// 1. 完整考虑 CJK 全角字符（占 2 列）与 ASCII 半角字符（占 1 列）；
        /// 2. 精确处理终端边缘换行时的光标落点（若光标处于满行末尾，准确放置在下一视觉行第 0 列，杜绝光标悬浮停留在上一行末尾）；
        /// 3. 支持无头单元测试（返回结构化信息）。
        /// </summary>
        public static (int CursorVisualRow, int CursorVisualCol, int TotalVisualRows, List<string> VisualLines) CalculateVisualLayout(
            IReadOnlyList<string> logicalLines,
            int cursorLogicalLine,
            int cursorColIndex,
            int startLeft,
            int windowWidth
        ) {
            if (windowWidth <= 0) windowWidth = 80;
            startLeft = Math.Clamp(startLeft, 0, Math.Max(0, windowWidth - 1));

            var visualLines = new List<string>();
            int targetVisualRow = 0;
            int targetVisualCol = 0;
            bool cursorSet = false;

            var lines = logicalLines.Count == 0 ? [""] : logicalLines;

            for (int l = 0; l < lines.Count; l++) {
                var line = lines[l];
                bool isCursorLine = (l == cursorLogicalLine);
                int lineStartLeft = (visualLines.Count == 0) ? startLeft : 0;
                int currentLimit = Math.Max(1, windowWidth - lineStartLeft);

                if (string.IsNullOrEmpty(line)) {
                    if (isCursorLine && !cursorSet && cursorColIndex <= 0) {
                        targetVisualRow = visualLines.Count;
                        targetVisualCol = lineStartLeft;
                        cursorSet = true;
                    }
                    visualLines.Add(string.Empty);
                    continue;
                }

                var currentChunk = new StringBuilder();
                int currentChunkWidth = 0;
                int chunkStartCol = lineStartLeft;

                for (int i = 0; i < line.Length; i++) {
                    if (isCursorLine && !cursorSet && i == cursorColIndex) {
                        targetVisualRow = visualLines.Count;
                        targetVisualCol = chunkStartCol + currentChunkWidth;
                        cursorSet = true;
                    }

                    char ch = line[i];
                    int charWidth = TuiLineEditorState.GetDisplayWidth(ch.ToString());
                    if (currentChunkWidth + charWidth > currentLimit) {
                        visualLines.Add(currentChunk.ToString());
                        currentChunk.Clear();
                        currentChunkWidth = 0;
                        chunkStartCol = 0;
                        currentLimit = Math.Max(1, windowWidth);
                    }

                    currentChunk.Append(ch);
                    currentChunkWidth += charWidth;
                }

                if (isCursorLine && !cursorSet && cursorColIndex >= line.Length) {
                    if (currentChunkWidth >= currentLimit) {
                        visualLines.Add(currentChunk.ToString());
                        currentChunk.Clear();
                        targetVisualRow = visualLines.Count;
                        targetVisualCol = 0;
                        visualLines.Add(string.Empty);
                        cursorSet = true;
                    } else {
                        targetVisualRow = visualLines.Count;
                        targetVisualCol = chunkStartCol + currentChunkWidth;
                        cursorSet = true;
                        visualLines.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }
                } else {
                    visualLines.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }
            }

            if (!cursorSet) {
                targetVisualRow = Math.Max(0, visualLines.Count - 1);
                targetVisualCol = 0;
            }

            return (targetVisualRow, targetVisualCol, visualLines.Count, visualLines);
        }

        /// <summary>
        /// 自适应终端宽度的文本编辑器统一重绘方法（支持自动边缘折行、全角/半角光标精准计算与旧残留行擦除）。
        /// </summary>
        public static void RedrawEditor(
            TextWriter? writer,
            int startLeft,
            int startTop,
            IReadOnlyList<string> logicalLines,
            int cursorLogicalLine,
            int cursorColIndex,
            int windowWidth,
            ref int previousVisualLineCount
        ) {
            var (cursorVisualRow, cursorVisualCol, totalVisualRows, visualLines) =
                CalculateVisualLayout(logicalLines, cursorLogicalLine, cursorColIndex, startLeft, windowWidth);

            var targetWriter = writer ?? Console.Out;
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0 && (writer == null || ReferenceEquals(writer, Console.Out))) {
                    for (int v = 0; v < visualLines.Count; v++) {
                        Console.SetCursorPosition(v == 0 ? startLeft : 0, startTop + v);
                        Console.Write(visualLines[v]);
                        Console.Write(AnsiClearLineFromCursor);
                    }
                    if (previousVisualLineCount > visualLines.Count) {
                        for (int v = visualLines.Count; v < previousVisualLineCount; v++) {
                            Console.SetCursorPosition(0, startTop + v);
                            Console.Write(AnsiClearLineFromCursor);
                        }
                    }
                    Console.SetCursorPosition(cursorVisualCol, startTop + cursorVisualRow);
                    previousVisualLineCount = visualLines.Count;
                    return;
                }
            } catch {
                // 忽略非标准/无头异常，回退为纯 ANSI 序列
            }

            var sb = new StringBuilder();
            for (int v = 0; v < visualLines.Count; v++) {
                int col = (v == 0 ? startLeft : 0);
                sb.Append($"\x1b[{startTop + v + 1};{col + 1}H");
                sb.Append(visualLines[v]);
                sb.Append(AnsiClearLineFromCursor);
            }
            if (previousVisualLineCount > visualLines.Count) {
                for (int v = visualLines.Count; v < previousVisualLineCount; v++) {
                    sb.Append($"\x1b[{startTop + v + 1};1H");
                    sb.Append(AnsiClearLineFromCursor);
                }
            }
            sb.Append($"\x1b[{startTop + cursorVisualRow + 1};{cursorVisualCol + 1}H");
            targetWriter.Write(sb.ToString());
            targetWriter.Flush();
            previousVisualLineCount = visualLines.Count;
        }

        /// <summary>
        /// 重绘单行文本并精确定位光标。
        /// </summary>
        /// <param name="writer">输出流（为 null 时默认 Console.Out）</param>
        /// <param name="startLeft">文本起始列</param>
        /// <param name="startTop">文本起始行</param>
        /// <param name="text">当前待显示的文本内容</param>
        /// <param name="cursorDisplayWidth">光标在文本中的视觉显示列宽（考虑全角字符）</param>
        /// <param name="remainingDisplayWidth">光标后剩余文本在终端中的视觉显示列宽（ANSI 相对回退用）</param>
        public static void RedrawSingleLine(
            TextWriter? writer,
            int startLeft,
            int startTop,
            string text,
            int cursorDisplayWidth,
            int remainingDisplayWidth = 0
        ) {
            var targetWriter = writer ?? Console.Out;
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0 && (writer == null || ReferenceEquals(writer, Console.Out))) {
                    int winWidth = TuiEnvironment.SafeWindowWidth();
                    int targetRow = startTop + (startLeft + cursorDisplayWidth) / winWidth;
                    int targetCol = (startLeft + cursorDisplayWidth) % winWidth;
                    Console.SetCursorPosition(startLeft, startTop);
                    Console.Write(text);
                    Console.Write(AnsiClearLineFromCursor);
                    Console.SetCursorPosition(targetCol, targetRow);
                    return;
                }
            } catch {
                // 忽略平台异常，回退到 ANSI
            }

            // ANSI 回退机制：写文本后，设置列绝对位置或利用相对回退
            targetWriter.Write("\r\x1b[" + (startLeft + 1) + "G");
            targetWriter.Write(text);
            targetWriter.Write(AnsiClearLineFromCursor);
            if (remainingDisplayWidth > 0) {
                targetWriter.Write($"\x1b[{remainingDisplayWidth}D");
            } else {
                targetWriter.Write("\r\x1b[" + (startLeft + cursorDisplayWidth + 1) + "G");
            }
        }

        /// <summary>
        /// 构建多行重绘的纯 ANSI 转义字符串（100% 可单测且无副作用）。
        /// </summary>
        /// <param name="startLeft">起始列（0-indexed）</param>
        /// <param name="startTop">起始行（0-indexed）</param>
        /// <param name="lines">当前要渲染的所有行文本</param>
        /// <param name="cursorRow">目标光标所在行索引（0-indexed）</param>
        /// <param name="cursorColDisplayWidth">目标光标所在列的显示宽度（考虑全角字符）</param>
        /// <param name="previousLineCount">上次渲染的总行数（若本次少于上次，用于清除多余残留行）</param>
        public static string BuildMultiLineSequence(
            int startLeft,
            int startTop,
            IReadOnlyList<string> lines,
            int cursorRow,
            int cursorColDisplayWidth,
            int previousLineCount = 0
        ) {
            var sb = new StringBuilder();
            var count = lines.Count;

            for (int i = 0; i < count; i++) {
                // 定位到该行起始位置（1-based 坐标）
                sb.Append($"\x1b[{startTop + i + 1};{startLeft + 1}H");
                sb.Append(lines[i]);
                sb.Append(AnsiClearLineFromCursor);
            }

            // 若本次行数少于上次行数，清除多余的旧行
            if (previousLineCount > count) {
                for (int i = count; i < previousLineCount; i++) {
                    sb.Append($"\x1b[{startTop + i + 1};{startLeft + 1}H");
                    sb.Append(AnsiClearLineFromCursor);
                }
            }

            // 将光标定位到指定的 (row, col) 位置
            var targetTop = startTop + Math.Clamp(cursorRow, 0, Math.Max(0, count - 1));
            var targetLeft = startLeft + Math.Max(0, cursorColDisplayWidth);
            sb.Append($"\x1b[{targetTop + 1};{targetLeft + 1}H");

            return sb.ToString();
        }

        /// <summary>
        /// 重绘多行文本并精确定位光标（支持清除旧残留行）。
        /// </summary>
        public static void RedrawMultiLine(
            TextWriter? writer,
            int startLeft,
            int startTop,
            IReadOnlyList<string> lines,
            int cursorRow,
            int cursorColDisplayWidth,
            int previousLineCount = 0
        ) {
            var targetWriter = writer ?? Console.Out;
            try {
                if (!Console.IsOutputRedirected && Console.CursorLeft >= 0 && (writer == null || ReferenceEquals(writer, Console.Out))) {
                    var count = lines.Count;
                    for (int i = 0; i < count; i++) {
                        Console.SetCursorPosition(startLeft, startTop + i);
                        Console.Write(lines[i]);
                        Console.Write(AnsiClearLineFromCursor);
                    }
                    if (previousLineCount > count) {
                        for (int i = count; i < previousLineCount; i++) {
                            Console.SetCursorPosition(startLeft, startTop + i);
                            Console.Write(AnsiClearLineFromCursor);
                        }
                    }
                    var finalRow = startTop + Math.Clamp(cursorRow, 0, Math.Max(0, count - 1));
                    var finalCol = startLeft + Math.Max(0, cursorColDisplayWidth);
                    Console.SetCursorPosition(finalCol, finalRow);
                    return;
                }
            } catch {
                // 降级为 ANSI 序列
            }

            var seq = BuildMultiLineSequence(startLeft, startTop, lines, cursorRow, cursorColDisplayWidth, previousLineCount);
            targetWriter.Write(seq);
            targetWriter.Flush();
        }
    }
}
