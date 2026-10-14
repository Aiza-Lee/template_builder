using System.IO;
using Core.Tui;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiRedrawerTests {
    [Fact]
    public void GetCursorPosition_DoesNotThrow() {
        var (left, top) = TuiRedrawer.GetCursorPosition();
        Assert.True(left >= 0);
        Assert.True(top >= 0);
    }

    [Fact]
    public void SetCursorPosition_WritesExpectedAnsiSequence() {
        using var sw = new StringWriter();
        TuiRedrawer.SetCursorPosition(10, 5, sw);
        // ANSI 坐标是 1-based: row=6, col=11
        Assert.Equal("\x1b[6;11H", sw.ToString());
    }

    [Fact]
    public void SetCursorVisible_WritesExpectedAnsiSequence() {
        using var swHide = new StringWriter();
        TuiRedrawer.SetCursorVisible(false, swHide);
        Assert.Equal(TuiRedrawer.AnsiHideCursor, swHide.ToString());

        using var swShow = new StringWriter();
        TuiRedrawer.SetCursorVisible(true, swShow);
        Assert.Equal(TuiRedrawer.AnsiShowCursor, swShow.ToString());
    }

    [Fact]
    public void ClearFromCursorToEndOfLine_WritesExpectedAnsiSequence() {
        using var sw = new StringWriter();
        TuiRedrawer.ClearFromCursorToEndOfLine(sw);
        Assert.Equal(TuiRedrawer.AnsiClearLineFromCursor, sw.ToString());
    }

    [Fact]
    public void RedrawSingleLine_WritesTextAndClearAndCursorOffset() {
        using var sw = new StringWriter();
        TuiRedrawer.RedrawSingleLine(sw, startLeft: 4, startTop: 2, text: "hello world", cursorDisplayWidth: 5, remainingDisplayWidth: 6);
        var output = sw.ToString();

        // 包含起始列定位、文本、清除尾部、向左回退
        Assert.Contains("hello world", output);
        Assert.Contains(TuiRedrawer.AnsiClearLineFromCursor, output);
        Assert.Contains("\x1b[6D", output);
    }

    [Fact]
    public void BuildMultiLineSequence_CorrectlyPositionsLinesAndCursor() {
        var lines = new[] { "line one", "line two: 中文", "line three" };
        var seq = TuiRedrawer.BuildMultiLineSequence(
            startLeft: 2,
            startTop: 10,
            lines: lines,
            cursorRow: 1,
            cursorColDisplayWidth: 14, // "line two: " (10) + "中" (2) = 12 等
            previousLineCount: 4       // 上次有 4 行，需要清除第 4 行
        );

        // 验证每行的 1-based 定位: row 11, col 3; row 12, col 3; row 13, col 3
        Assert.Contains("\x1b[11;3Hline one\x1b[K", seq);
        Assert.Contains("\x1b[12;3Hline two: 中文\x1b[K", seq);
        Assert.Contains("\x1b[13;3Hline three\x1b[K", seq);

        // 验证清理第 4 行残留 (row 14, col 3)
        Assert.Contains("\x1b[14;3H\x1b[K", seq);

        // 验证最终光标定位: row 12 (10 + cursorRow 1 + 1), col 17 (startLeft 2 + cursorColDisplayWidth 14 + 1)
        Assert.EndsWith("\x1b[12;17H", seq);
    }

    [Fact]
    public void RedrawMultiLine_WritesToTextWriter() {
        using var sw = new StringWriter();
        var lines = new[] { "alpha", "beta" };
        TuiRedrawer.RedrawMultiLine(sw, 0, 0, lines, 0, 3, 0);

        var output = sw.ToString();
        Assert.Contains("alpha", output);
        Assert.Contains("beta", output);
        Assert.Contains(TuiRedrawer.AnsiClearLineFromCursor, output);
    }

    [Fact]
    public void CalculateVisualLayout_ShortSingleLine_PlacesCursorOnSameLine() {
        var lines = new[] { "hello" };
        var (cursorRow, cursorCol, totalRows, visualLines) =
            TuiRedrawer.CalculateVisualLayout(lines, cursorLogicalLine: 0, cursorColIndex: 5, startLeft: 0, windowWidth: 80);

        Assert.Equal(0, cursorRow);
        Assert.Equal(5, cursorCol);
        Assert.Equal(1, totalRows);
        Assert.Single(visualLines);
        Assert.Equal("hello", visualLines[0]);
    }

    [Fact]
    public void CalculateVisualLayout_TextExceedingWidth_WrapsCursorToNextLine() {
        // 80 宽度终端，输入 85 个字符，光标在末尾 (index 85)
        var text = new string('a', 85);
        var lines = new[] { text };
        var (cursorRow, cursorCol, totalRows, visualLines) =
            TuiRedrawer.CalculateVisualLayout(lines, cursorLogicalLine: 0, cursorColIndex: 85, startLeft: 0, windowWidth: 80);

        // 验证光标准确换到下一行第 5 列，绝不悬浮在上一行末尾
        Assert.Equal(1, cursorRow);
        Assert.Equal(5, cursorCol);
        Assert.Equal(2, totalRows);
        Assert.Equal(2, visualLines.Count);
        Assert.Equal(80, visualLines[0].Length);
        Assert.Equal("aaaaa", visualLines[1]);
    }

    [Fact]
    public void CalculateVisualLayout_TextExactWidth_WrapsCursorToStartOfNextLine() {
        // 刚好填满 80 列，光标在末尾
        var text = new string('x', 80);
        var lines = new[] { text };
        var (cursorRow, cursorCol, totalRows, visualLines) =
            TuiRedrawer.CalculateVisualLayout(lines, cursorLogicalLine: 0, cursorColIndex: 80, startLeft: 0, windowWidth: 80);

        // 光标应在第 1 行第 0 列，总视觉行数为 2
        Assert.Equal(1, cursorRow);
        Assert.Equal(0, cursorCol);
        Assert.Equal(2, totalRows);
        Assert.Equal(2, visualLines.Count);
        Assert.Equal(80, visualLines[0].Length);
        Assert.Equal("", visualLines[1]);
    }

    [Fact]
    public void CalculateVisualLayout_CJKCharactersAtBoundary_WrapsCleanly() {
        // 宽度 40，前 39 个字符为 ASCII，第 40 个字符为全角汉字 "中" (占 2 列)
        // 39 + 2 = 41 > 40，因此 "中" 必须换到下一行第 0 列，视觉行 0 留 39 个字符
        var text = new string('c', 39) + "中文";
        var lines = new[] { text };
        var (cursorRow, cursorCol, totalRows, visualLines) =
            TuiRedrawer.CalculateVisualLayout(lines, cursorLogicalLine: 0, cursorColIndex: text.Length, startLeft: 0, windowWidth: 40);

        Assert.Equal(1, cursorRow);
        Assert.Equal(4, cursorCol); // "中文" 占 4 列
        Assert.Equal(2, totalRows);
        Assert.Equal(39, visualLines[0].Length);
        Assert.Equal("中文", visualLines[1]);
    }

    [Fact]
    public void RedrawEditor_WhenTextShrinks_ClearsOldVisualLines() {
        using var sw = new StringWriter();
        var lines = new[] { "short now" };
        int previousVisualLineCount = 3; // 上次占用 3 行

        TuiRedrawer.RedrawEditor(
            sw,
            startLeft: 0,
            startTop: 5,
            logicalLines: lines,
            cursorLogicalLine: 0,
            cursorColIndex: 9,
            windowWidth: 80,
            previousVisualLineCount: ref previousVisualLineCount
        );

        var output = sw.ToString();
        // 验证绘制了第 1 行
        Assert.Contains("short now", output);
        // 验证清除了旧残留行 (第 7 行与第 8 行，1-based 坐标为 row=7 与 row=8)
        Assert.Contains("\x1b[7;1H\x1b[K", output);
        Assert.Contains("\x1b[8;1H\x1b[K", output);
        // 验证光标定位到 row 6 (startTop 5 + 1), col 10 (9 + 1)
        Assert.EndsWith("\x1b[6;10H", output);
        // 验证 previousVisualLineCount 缩减为 1
        Assert.Equal(1, previousVisualLineCount);
    }
}
