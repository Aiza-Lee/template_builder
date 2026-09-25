using Core.Tui;
using Spectre.Console;
using Xunit;

namespace template_builder.Tests.Tui;

public class TerminalWidthRegistryTests {
    public TerminalWidthRegistryTests() {
        // 每个测试前重置为默认注册状态，确保单测隔离
        TerminalWidthRegistry.InitializeDefaults();
    }

    [Fact]
    public void GetRequiredWidth_DefaultRegistrations_ReturnsExpectedMax() {
        var req = TerminalWidthRegistry.GetRequiredWidth();
        Assert.True(req >= 60, $"默认要求的最小宽度应至少为 60 列，实际为: {req}");

        var (bottleneck, width) = TerminalWidthRegistry.GetBottleneck();
        Assert.Equal(req, width);
        Assert.False(string.IsNullOrWhiteSpace(bottleneck));
    }

    [Fact]
    public void Register_CustomComponentWithLargerWidth_IncreasesRequiredWidthDynamically() {
        try {
            TerminalWidthRegistry.Register("UltraWideDataGrid", 92);
            var req = TerminalWidthRegistry.GetRequiredWidth();
            Assert.Equal(92, req);

            var (bottleneck, width) = TerminalWidthRegistry.GetBottleneck();
            Assert.Equal("UltraWideDataGrid", bottleneck);
            Assert.Equal(92, width);
        } finally {
            TerminalWidthRegistry.Unregister("UltraWideDataGrid");
        }

        Assert.Equal(60, TerminalWidthRegistry.GetRequiredWidth());
    }

    [Fact]
    public void IsWidthTooSmall_CorrectlyEvaluatesAgainstRequired() {
        var req = TerminalWidthRegistry.GetRequiredWidth();

        Assert.True(TerminalWidthRegistry.IsWidthTooSmall(req - 1));
        Assert.True(TerminalWidthRegistry.IsWidthTooSmall(30));
        Assert.False(TerminalWidthRegistry.IsWidthTooSmall(req));
        Assert.False(TerminalWidthRegistry.IsWidthTooSmall(req + 20));
    }

    [Fact]
    public void RenderTooSmallWarning_RendersCurrentAndRequiredWidthsInPanel() {
        var warning = TerminalWidthRegistry.RenderTooSmallWarning(currentWidth: 42, requiredWidth: 65);
        var plain = TuiScaffolding.RenderToPlainText(c => c.Write(warning));

        Assert.Contains("终端窗口过小", plain);
        Assert.Contains("42", plain);
        Assert.Contains("65", plain);
        Assert.Contains("请拉大终端窗口", plain);
    }

    [Fact]
    public void Register_InvalidArguments_ThrowsAppropriateException() {
        Assert.Throws<ArgumentException>(() => TerminalWidthRegistry.Register("", 50));
        Assert.Throws<ArgumentException>(() => TerminalWidthRegistry.Register("   ", 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerminalWidthRegistry.Register("Comp", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerminalWidthRegistry.Register("Comp", -10));
    }

    [Fact]
    public void GetRegistrations_ReturnsAllRegisteredComponentsSnapshot() {
        var dict = TerminalWidthRegistry.GetRegistrations();

        Assert.True(dict.ContainsKey("Menu"));
        Assert.True(dict.ContainsKey("Banner"));
        Assert.True(dict.ContainsKey("Dashboard"));
        Assert.True(dict.ContainsKey("FieldCard"));
        Assert.True(dict.ContainsKey("LineEditor"));
        Assert.Equal(60, dict["Menu"]);
    }

    [Fact]
    public void EnsureAdequateWidthAndRender_WhenAdequate_InvokesRenderActionWithWidth() {
        bool invoked = false;
        int passedWidth = 0;

        TerminalWidthRegistry.EnsureAdequateWidthAndRender(AnsiConsole.Console, w => {
            invoked = true;
            passedWidth = w;
        });

        Assert.True(invoked);
        Assert.True(passedWidth > 0);
    }

    [Fact]
    public void BuildBanner_ProducesRenderableRowsAcrossDifferentWidths() {
        var wide = TuiViewRenderer.BuildBanner(width: 80);
        var plainWide = TuiScaffolding.RenderToPlainText(c => c.Write(wide), width: 80);
        Assert.Contains("ACM/ICPC 代码模板生成工具", plainWide);

        var narrow = TuiViewRenderer.BuildBanner(width: 35);
        var plainNarrow = TuiScaffolding.RenderToPlainText(c => c.Write(narrow), width: 35);
        Assert.Contains("Template Builder", plainNarrow);
    }
}
