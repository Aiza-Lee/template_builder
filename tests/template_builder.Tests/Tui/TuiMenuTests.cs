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
        Assert.Contains("Space", text);
        Assert.Contains("Enter", text);
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

    [Fact]
    public void Prompt_WithInitialSelectedId_SelectsMatchingItemByDefault() {
        var items = new List<TuiMenuItem> {
            new("BUILD", "开始构建", action: TuiMenuAction.Build),
            new("CATEGORY_CODE", "代码块排版"),
            new("CATEGORY_FONTS", "字体与微排版"),
            new("BACK", "返回", action: TuiMenuAction.Back)
        };

        // 验证 initialSelectedId 准确设定默认选中的 index（非交互环境与交互环境初始焦点）
        var chosen = TuiMenu.Prompt("标题", items, initialSelectedId: "CATEGORY_FONTS");
        Assert.NotNull(chosen);
        Assert.Equal("CATEGORY_FONTS", chosen.Id);
    }

    [Fact]
    public void SaveAction_IsSaveFlag_TrueOnlyForSaveAction() {
        var saveItem = new TuiMenuItem("SAVE", "保存配置", action: TuiMenuAction.Save);
        var buildItem = new TuiMenuItem("BUILD", "开始构建", action: TuiMenuAction.Build);

        Assert.True(saveItem.IsSave);
        Assert.False(buildItem.IsSave);
    }

    [Fact]
    public void Prompt_WithoutBackItem_UsesFirstItemByDefaultInNonInteractive() {
        var items = new List<TuiMenuItem> {
            new("CHOICE_A", "选项A"),
            new("CHOICE_B", "选项B"),
            new("CHOICE_C", "选项C")
        };

        var chosen = TuiMenu.Prompt("选择选项", items);
        Assert.NotNull(chosen);
        Assert.Equal("CHOICE_A", chosen.Id);
    }

    [Fact]
    public void Prompt_WithoutBackItem_InitialSelectedIdMatchesTargetChoice() {
        var items = new List<TuiMenuItem> {
            new("CHOICE_A", "选项A"),
            new("CHOICE_B", "选项B"),
            new("CHOICE_C", "选项C")
        };

        var chosen = TuiMenu.Prompt("选择选项", items, initialSelectedId: "CHOICE_B");
        Assert.NotNull(chosen);
        Assert.Equal("CHOICE_B", chosen.Id);
    }

    [Fact]
    public void BackSentinel_IsBackIsTrue_AndHasBackAction() {
        var sentinel = TuiMenuItem.BackSentinel;
        Assert.True(sentinel.IsBack);
        Assert.Equal(TuiMenuAction.Back, sentinel.Action);
    }

    [Fact]
    public void ConfirmSentinel_IsBuildIsTrue_AndHasBuildAction() {
        var sentinel = TuiMenuItem.ConfirmSentinel;
        Assert.True(sentinel.IsBuild);
        Assert.Equal(TuiMenuAction.Build, sentinel.Action);
        Assert.Equal("__CONFIRM__", sentinel.Id);
    }

    [Fact]
    public void RenderMenu_ManyItems_RendersAllItemsFlatWithoutFolding() {
        var items = Enumerable.Range(1, 25).Select(i => new TuiMenuItem($"KEY_{i}", $"配置项_{i}")).ToList();

        var renderable = TuiMenu.RenderMenu("长列表测试", items, selectedIndex: 0);
        var text = TuiScaffolding.RenderToPlainText(c => c.Write(renderable));

        // 验证所有 25 项均平铺展开呈现
        for (int i = 1; i <= 25; i++) {
            Assert.Contains($"配置项_{i}", text);
        }

        // 验证彻底不存在“还有xx项”折叠提示
        Assert.DoesNotContain("还有", text);
        Assert.DoesNotContain("向下", text);
        Assert.DoesNotContain("向上", text);
    }

    [Fact]
    public void DeleteSentinel_IsClearIsTrue_AndHasClearAction() {
        var sentinel = TuiMenuItem.DeleteSentinel;
        Assert.True(sentinel.IsClear);
        Assert.Equal(TuiMenuAction.Clear, sentinel.Action);
        Assert.Equal("__DELETE__", sentinel.Id);
    }

    [Fact]
    public void RenderMenu_WithCustomHelp_RendersDelShortcut() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "项目1")
        };
        var help = "[grey]操作：[[↑/↓]] 移动  [[Space / →]] 编辑  [[Del]] 清空  [[Enter]] 确认构建  [[← / Esc]] 返回[/]";
        var renderable = TuiMenu.RenderMenu("菜单", items, selectedIndex: 0, customHelp: help);
        var text = TuiScaffolding.RenderToPlainText(c => c.Write(renderable));

        Assert.Contains("[Del] 清空", text);
        Assert.Contains("[Space / →] 编辑", text);
        Assert.Contains("[Enter] 确认构建", text);
    }

    [Fact]
    public void ProcessKey_VimJAndK_MovesSelectionDownAndUp() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "项目1"),
            new("ITEM2", "项目2"),
            new("ITEM3", "项目3")
        };

        // 按 'j' 向下移动：从 0 -> 1
        var keyJ = new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false);
        var (idxAfterJ, chosenJ) = TuiMenu.ProcessKey(keyJ, items, selectedIndex: 0);
        Assert.Equal(1, idxAfterJ);
        Assert.Null(chosenJ);

        // 再按 'j' 向下移动：从 1 -> 2
        var (idxAfterJ2, _) = TuiMenu.ProcessKey(keyJ, items, selectedIndex: 1);
        Assert.Equal(2, idxAfterJ2);

        // 再次按 'j' 循环回 0
        var (idxAfterJWrap, _) = TuiMenu.ProcessKey(keyJ, items, selectedIndex: 2);
        Assert.Equal(0, idxAfterJWrap);

        // 按 'k' 向上移动：从 0 循环回 2
        var keyK = new ConsoleKeyInfo('k', ConsoleKey.K, false, false, false);
        var (idxAfterKWrap, chosenK) = TuiMenu.ProcessKey(keyK, items, selectedIndex: 0);
        Assert.Equal(2, idxAfterKWrap);
        Assert.Null(chosenK);

        // 从 2 向上移动至 1
        var (idxAfterK, _) = TuiMenu.ProcessKey(keyK, items, selectedIndex: 2);
        Assert.Equal(1, idxAfterK);
    }

    [Fact]
    public void ProcessKey_VimL_SelectsHighlightedItem() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "项目1"),
            new("ITEM2", "项目2")
        };

        var keyL = new ConsoleKeyInfo('l', ConsoleKey.L, false, false, false);
        var (idx, chosen) = TuiMenu.ProcessKey(keyL, items, selectedIndex: 1);

        Assert.Equal(1, idx);
        Assert.NotNull(chosen);
        Assert.Equal("ITEM2", chosen.Id);
    }

    [Fact]
    public void ProcessKey_VimH_TriggersBackSentinelOrBackItem() {
        var itemsWithBack = new List<TuiMenuItem> {
            new("ITEM1", "项目1"),
            new("BACK", "返回上级", action: TuiMenuAction.Back)
        };

        var keyH = new ConsoleKeyInfo('h', ConsoleKey.H, false, false, false);
        var (_, chosenWithBack) = TuiMenu.ProcessKey(keyH, itemsWithBack, selectedIndex: 0);

        Assert.NotNull(chosenWithBack);
        Assert.True(chosenWithBack.IsBack);
        Assert.Equal("BACK", chosenWithBack.Id);

        // 列表内没有显式返回项时，默认回退至全局 BackSentinel
        var itemsWithoutBack = new List<TuiMenuItem> {
            new("ITEM1", "项目1")
        };
        var (_, chosenWithoutBack) = TuiMenu.ProcessKey(keyH, itemsWithoutBack, selectedIndex: 0);
        Assert.NotNull(chosenWithoutBack);
        Assert.True(chosenWithoutBack.IsBack);
        Assert.Same(TuiMenuItem.BackSentinel, chosenWithoutBack);
    }

    [Fact]
    public void ProcessKey_ModifiersWithHJKL_DoesNotTriggerVimNavigation() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "项目1"),
            new("ITEM2", "项目2")
        };

        // Ctrl+J 或 Alt+K 不应触发 vim 移动
        var ctrlJ = new ConsoleKeyInfo('j', ConsoleKey.J, false, false, true); // Control = true
        var (idx1, chosen1) = TuiMenu.ProcessKey(ctrlJ, items, selectedIndex: 0);
        Assert.Equal(0, idx1);
        Assert.Null(chosen1);

        var altK = new ConsoleKeyInfo('k', ConsoleKey.K, false, true, false); // Alt = true
        var (idx2, chosen2) = TuiMenu.ProcessKey(altK, items, selectedIndex: 0);
        Assert.Equal(0, idx2);
        Assert.Null(chosen2);
    }

    [Fact]
    public void RenderMenu_DefaultHelp_ContainsVimHjklHints() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "项目1")
        };
        var renderable = TuiMenu.RenderMenu("菜单", items, selectedIndex: 0);
        var text = TuiScaffolding.RenderToPlainText(c => c.Write(renderable));

        Assert.Contains("jk", text);
        Assert.Contains("h", text);
        Assert.Contains("l", text);
    }
}


