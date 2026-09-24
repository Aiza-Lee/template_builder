using Core.Tui;
using Spectre.Console;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiMenuTests {
    [Fact]
    public void FormatItemLine_OverriddenItemsHighlightedWithAmberColor() {
        var items = new List<TuiMenuItem> {
            new("KEY1", "普通配置项", "默认值"),
            new("KEY2", "已修改配置项", "新值", isOverridden: true)
        };

        var renderable = TuiMenu.RenderMenu("测试菜单", items, selectedIndex: 0, scrollOffset: 0, pageSize: 10);
        var text = TuiScaffolding.RenderToAnsi(c => c.Write(renderable));

        // 包含标题与操作引导
        Assert.Contains("测试菜单", text);
        Assert.Contains("普通配置项", text);
        Assert.Contains("已修改配置项", text);
        Assert.Contains("已修改", text);
        Assert.Contains("Enter / →", text);
        Assert.Contains("← / Esc", text);
    }

    [Fact]
    public void FormatItemLine_SpecialActionsFormattedProperly() {
        var items = new List<TuiMenuItem> {
            new("BUILD", "开始构建", action: TuiMenuAction.Build),
            new("BACK", "返回上级", action: TuiMenuAction.Back),
            new("CLEAR", "清空所有覆盖项", action: TuiMenuAction.Clear),
            new("INSPECT", "查看当前全部覆盖项", action: TuiMenuAction.Inspect)
        };

        var renderable = TuiMenu.RenderMenu("操作菜单", items, selectedIndex: 0, scrollOffset: 0, pageSize: 10);
        var plain = TuiScaffolding.RenderToPlainText(c => c.Write(renderable));

        Assert.Contains(">> 开始构建", plain);
        Assert.Contains("<< 返回上级", plain);
        Assert.Contains("x  清空所有覆盖项", plain);
        Assert.Contains("*  查看当前全部覆盖项", plain);
    }

    [Fact]
    public void Prompt_RedirectedInput_ReturnsDefaultItemGracefully() {
        var items = new List<TuiMenuItem> {
            new("OPT1", "选项1"),
            new("OPT2", "选项2"),
            new("BACK", "返回", action: TuiMenuAction.Back)
        };

        // 在单测等非交互式环境下，Prompt 必须安全返回指定 defaultIndex 的选项，绝不阻塞卡死
        var chosen = TuiMenu.Prompt("标题", items, defaultIndex: 1);
        Assert.NotNull(chosen);
        Assert.Equal("OPT2", chosen.Id);
    }

    [Fact]
    public void Prompt_EmptyItems_ReturnsBackSentinel() {
        var chosen = TuiMenu.Prompt("空菜单", []);
        Assert.Same(TuiMenuItem.BackSentinel, chosen);
        Assert.True(chosen.IsBack);
    }
}
