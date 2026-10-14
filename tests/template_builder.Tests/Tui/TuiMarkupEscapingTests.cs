using Core.Localization;
using Core.Models;
using Core.Tui;
using Spectre.Console;
using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiMarkupEscapingTests {
    [Theory]
    [InlineData("[audit].json")]
    [InlineData("[red].json")]
    [InlineData("[bold].json")]
    [InlineData("[审核_audit 2026] [v1.0].json")]
    [InlineData("path/[style]/[blue] config.json")]
    public void PromptTitle_WithBracketedAndColorLikeConfigNames_ParsesCleanlyWithoutMarkupException(string fileName) {
        var fileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), fileName));
        var session = new ProjectSession {
            ConfigFile = fileInfo,
            UserProvidedConfig = true
        };

        string sourceLabel = session.ConfigFile == null
            ? I18n.T("ConfigMenu.SourceBuiltin", "内置默认")
            : session.UserProvidedConfig
                ? string.Format(I18n.T("ConfigMenu.SourceUser", "用户项目 ({0})"), Markup.Escape(session.ConfigFile.Name))
                : string.Format(I18n.T("ConfigMenu.SourceDefault", "默认项目 ({0})"), Markup.Escape(session.ConfigFile.Name));

        var promptTitle = $"[bold {TuiTheme.Secondary}]{I18n.T("ConfigMenu.Title", "配置调优")}[/]  [grey]{string.Format(I18n.T("ConfigMenu.Source", "来源: {0}"), sourceLabel)}  {string.Format(I18n.T("ConfigMenu.OverridesCount", "覆盖: {0}"), 0)}[/]：";

        // 验证 Spectre.Console Markup 解析不抛出 "Could not find color or style" 等异常
        var markup = new Markup(promptTitle);
        var plain = TuiScaffolding.RenderToPlainText(c => c.Write(markup));

        Assert.Contains(fileInfo.Name, plain);
    }

    [Theory]
    [InlineData("[audit].json")]
    [InlineData("[bold].json")]
    [InlineData("[red].json")]
    [InlineData("[custom [nested] style].json")]
    public void FinalSourceLabel_WithBracketedPath_RendersCleanlyWithoutMarkupException(string fileName) {
        var fileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), fileName));
        var session = new ProjectSession {
            ConfigFile = fileInfo,
            UserProvidedConfig = true
        };

        string finalSourceLabel = session.ConfigFile == null
            ? I18n.T("ConfigMenu.SourceBuiltinDesc", "内置默认配置")
            : session.UserProvidedConfig
                ? string.Format(I18n.T("ConfigMenu.SourceUserDesc", "用户项目配置 ({0})"), Markup.Escape(session.ConfigFile.FullName))
                : string.Format(I18n.T("ConfigMenu.SourceDefaultDesc", "默认项目配置 ({0})"), Markup.Escape(session.ConfigFile.FullName));

        var rendered = TuiScaffolding.RenderToPlainText(c => {
            c.MarkupLine($"[bold]{I18n.T("ConfigMenu.ConfigSource", "配置来源")}[/]：{finalSourceLabel}");
        });

        Assert.Contains(fileInfo.FullName, rendered);
    }

    [Fact]
    public void TuiMenu_WithBracketedAndColorLikeItemTitles_RendersLiteralBrackets() {
        var items = new List<TuiMenuItem> {
            new("ITEM1", "包含 [audit] 与 [red] 的字段", "[1, 2, 3] 数组值"),
            new("ITEM2", "普通字段", "[未修改]")
        };

        var renderable = TuiMenu.RenderMenu("测试菜单", items, selectedIndex: 0);
        var plain = TuiScaffolding.RenderToPlainText(c => c.Write(renderable));

        Assert.Contains("[audit]", plain);
        Assert.Contains("[red]", plain);
        Assert.Contains("[1, 2, 3]", plain);
        Assert.Contains("[未修改]", plain);
    }
}
