using Core.Tui;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiLineEditorTests {
    [Fact]
    public void Constructor_InitializesTextAndCursorAtEnd() {
        var state = new TuiLineEditorState("hello");
        Assert.Equal("hello", state.Text);
        Assert.Equal(5, state.Length);
        Assert.Equal(5, state.Cursor);
    }

    [Fact]
    public void MoveLeftAndRight_ClampsWithinBounds() {
        var state = new TuiLineEditorState("abc");
        Assert.Equal(3, state.Cursor);

        state.MoveLeft();
        Assert.Equal(2, state.Cursor);

        state.MoveLeft();
        state.MoveLeft();
        state.MoveLeft(); // Try to move past 0
        Assert.Equal(0, state.Cursor);

        state.MoveRight();
        Assert.Equal(1, state.Cursor);

        state.MoveRight();
        state.MoveRight();
        state.MoveRight(); // Try to move past 3
        Assert.Equal(3, state.Cursor);
    }

    [Fact]
    public void MoveHomeAndEnd_JumpsToExtremes() {
        var state = new TuiLineEditorState("testing");
        Assert.Equal(7, state.Cursor);

        state.MoveHome();
        Assert.Equal(0, state.Cursor);

        state.MoveEnd();
        Assert.Equal(7, state.Cursor);
    }

    [Fact]
    public void Insert_InsertsAtCurrentCursorPosition() {
        var state = new TuiLineEditorState("ac");
        state.MoveLeft(); // Cursor between 'a' and 'c'
        state.Insert('b');

        Assert.Equal("abc", state.Text);
        Assert.Equal(2, state.Cursor);
    }

    [Fact]
    public void InsertString_InsertsAtCurrentCursorPosition() {
        var state = new TuiLineEditorState("start-end");
        state.MoveHome();
        state.MoveRight();
        state.MoveRight();
        state.MoveRight();
        state.MoveRight();
        state.MoveRight(); // after "start"
        state.Insert("middle-");

        Assert.Equal("startmiddle--end", state.Text);
    }

    [Fact]
    public void Backspace_DeletesCharBeforeCursor() {
        var state = new TuiLineEditorState("abc");
        state.Backspace();
        Assert.Equal("ab", state.Text);
        Assert.Equal(2, state.Cursor);

        state.MoveHome();
        state.Backspace(); // Nothing to delete before 0
        Assert.Equal("ab", state.Text);
        Assert.Equal(0, state.Cursor);
    }

    [Fact]
    public void Delete_DeletesCharAtCursor() {
        var state = new TuiLineEditorState("abc");
        state.MoveHome(); // Cursor at 'a'
        state.Delete();
        Assert.Equal("bc", state.Text);
        Assert.Equal(0, state.Cursor);

        state.MoveEnd();
        state.Delete(); // Nothing to delete at end
        Assert.Equal("bc", state.Text);
    }

    [Fact]
    public void Clear_EmptiesBufferAndResetsCursor() {
        var state = new TuiLineEditorState("something");
        state.Clear();
        Assert.Empty(state.Text);
        Assert.Equal(0, state.Cursor);
        Assert.Equal(0, state.Length);
    }

    [Fact]
    public void DisplayWidth_CalculatesAsciiAndFullWidthCjkCorrectly() {
        Assert.Equal(0, TuiLineEditorState.GetDisplayWidth(null));
        Assert.Equal(0, TuiLineEditorState.GetDisplayWidth(""));
        Assert.Equal(5, TuiLineEditorState.GetDisplayWidth("hello"));
        Assert.Equal(4, TuiLineEditorState.GetDisplayWidth("代码")); // 2 CJK characters = 4 display columns
        Assert.Equal(9, TuiLineEditorState.GetDisplayWidth("代码 font")); // 4 + 1 + 4 = 9
    }

    [Fact]
    public void CursorAndRemainingDisplayWidth_CalculatesCorrectOffsets() {
        var state = new TuiLineEditorState("中文abc");
        // Total display width: 2 + 2 + 3 = 7
        Assert.Equal(7, state.TotalDisplayWidth);

        state.MoveHome();
        Assert.Equal(0, state.CursorDisplayWidth);
        Assert.Equal(7, state.RemainingDisplayWidth);

        state.MoveRight(); // After "中"
        Assert.Equal(2, state.CursorDisplayWidth);
        Assert.Equal(5, state.RemainingDisplayWidth);

        state.MoveRight(); // After "文"
        Assert.Equal(4, state.CursorDisplayWidth);
        Assert.Equal(3, state.RemainingDisplayWidth);

        state.MoveEnd();
        Assert.Equal(7, state.CursorDisplayWidth);
        Assert.Equal(0, state.RemainingDisplayWidth);
    }

    [Fact]
    public void MultiLine_GetLines_SplitsLinesCorrectly() {
        var state1 = new TuiLineEditorState("");
        Assert.Equal(new[] { "" }, state1.GetLines());
        Assert.Equal(1, state1.LineCount);

        var state2 = new TuiLineEditorState("first\nsecond\r\nthird");
        var lines = state2.GetLines();
        Assert.Equal(3, lines.Count);
        Assert.Equal("first", lines[0]);
        Assert.Equal("second", lines[1]);
        Assert.Equal("third", lines[2]);
    }

    [Fact]
    public void MultiLine_CursorCoordinates_CalculateLineAndColCorrectly() {
        // "line1\nline2: 中文\nend"
        var state = new TuiLineEditorState("line1\nline2: 中文\nend");
        // Start: cursor at end (index 21)
        Assert.Equal(2, state.CursorLineIndex);
        Assert.Equal(3, state.CursorColumnIndex);

        state.MoveHome(entireText: true);
        Assert.Equal(0, state.CursorLineIndex);
        Assert.Equal(0, state.CursorColumnIndex);
        Assert.Equal(0, state.CursorColumnDisplayWidth);

        // Move to line 1
        state.MoveDown();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(0, state.CursorColumnIndex);
        Assert.Equal(0, state.CursorColumnDisplayWidth);

        // Move past "line2: " (7 chars)
        for (int i = 0; i < 7; i++) state.MoveRight();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(7, state.CursorColumnIndex);
        Assert.Equal(7, state.CursorColumnDisplayWidth);

        // Move past "中" (1 CJK char = 2 visual columns)
        state.MoveRight();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(8, state.CursorColumnIndex);
        Assert.Equal(9, state.CursorColumnDisplayWidth); // 7 + 2 = 9!
    }

    [Fact]
    public void MultiLine_MoveUpAndDown_PreservesOrClampsColumn() {
        var text = "short\nvery long line here\nend";
        var state = new TuiLineEditorState(text);

        // Move to "very long line here" at col 15
        state.MoveHome(entireText: true);
        state.MoveDown();
        for (int i = 0; i < 15; i++) state.MoveRight();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(15, state.CursorColumnIndex);

        // Move up to "short" (len 5): column clamps to 5
        state.MoveUp();
        Assert.Equal(0, state.CursorLineIndex);
        Assert.Equal(5, state.CursorColumnIndex);

        // Move down to line 1: column stays at 5
        state.MoveDown();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(5, state.CursorColumnIndex);

        // Move down to "end" (len 3): column clamps to 3
        state.MoveDown();
        Assert.Equal(2, state.CursorLineIndex);
        Assert.Equal(3, state.CursorColumnIndex);
    }

    [Fact]
    public void MultiLine_HomeAndEnd_NavigateCurrentLineVersusEntireText() {
        var text = "first line\nsecond line\nthird line";
        var state = new TuiLineEditorState(text);

        // Move cursor into middle of second line
        state.MoveHome(entireText: true);
        state.MoveDown();
        state.MoveRight();
        state.MoveRight();
        state.MoveRight();
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(3, state.CursorColumnIndex);

        // Line Home
        state.MoveHome(entireText: false);
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(0, state.CursorColumnIndex);

        // Line End
        state.MoveEnd(entireText: false);
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal("second line".Length, state.CursorColumnIndex);

        // Entire Text Home & End
        state.MoveHome(entireText: true);
        Assert.Equal(0, state.Cursor);
        state.MoveEnd(entireText: true);
        Assert.Equal(state.Length, state.Cursor);
    }

    [Fact]
    public void MultiLine_BackspaceAndDelete_AcrossNewlines() {
        var state = new TuiLineEditorState("abc\ndef");
        state.MoveHome(entireText: true);
        state.MoveDown(); // At start of "def" (Line 1, Col 0, index 4)
        Assert.Equal(1, state.CursorLineIndex);
        Assert.Equal(0, state.CursorColumnIndex);

        // Backspace merges line 1 into line 0
        state.Backspace();
        Assert.Equal("abcdef", state.Text);
        Assert.Equal(0, state.CursorLineIndex);
        Assert.Equal(3, state.CursorColumnIndex);

        // Delete at index 3 removes 'd'
        state.Delete();
        Assert.Equal("abcef", state.Text);
    }
}
