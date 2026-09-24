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
}
