using System;
using System.IO;
using Core;
using Core.Models;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// UX-03 验收测试：验证 TUI 默认值、空值及实际生效值的一致性。
/// 确保显式区分设置值、设置为空、恢复项目配置与恢复内置默认四种动作，
/// 并且从同一最终配置渲染界面和执行构建。
/// </summary>
public class TuiConfigOverrideBehaviorTests {
    [Fact]
    public void SingleColumnProject_WhenSelectingBuiltinDoubleColumn_OverridesCorrectlyAndPersistsInBuild() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "layout": {
                    "columns": 1
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var item = config.GetItem("LAYOUT_COLUMNS")!;
        Assert.Equal("1", item.ProjectValue);
        Assert.Equal("2", item.BuiltinValue);
        Assert.Equal("1", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);

        // 模拟用户在 TUI 选项中选择了 "2" (内置默认值)
        // 旧 Bug: 因 2 == defaultVal，会把覆盖移除，导致回退到项目配置 1
        // 新逻辑: 识别到项目值为 1，必须以运行时覆盖形式记录 2
        var success = config.TrySetOverride("LAYOUT_COLUMNS", "2");
        Assert.True(success);

        Assert.Equal("2", item.EffectiveValue);
        Assert.Equal(ConfigSource.Override, item.Source);
        Assert.True(item.IsOverridden);
        Assert.True(config.GetActiveOverrides().ContainsKey("LAYOUT_COLUMNS"));
        Assert.Equal("2", config.GetActiveOverrides()["LAYOUT_COLUMNS"]);

        // 验证底层的 TexParser 生效值确实为 2
        Assert.Equal(2, config.TexParser["LAYOUT_COLUMNS"].GetAsInt());
    }

    [Fact]
    public void SettingEmptyString_AuthorAndTitleNote_ProducesTrueEmptyValuesInConfigAndTex() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "author": "NonEmptyAuthor",
                "title": {
                    "note": "NonEmptyNote"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var authorItem = config.GetItem("AUTHOR")!;
        var noteItem = config.GetItem("TITLE_NOTE")!;
        Assert.Equal("NonEmptyAuthor", authorItem.EffectiveValue);
        Assert.Equal("NonEmptyNote", noteItem.EffectiveValue);

        // 显式设置为空字符串 (清空取值动作)
        Assert.True(config.TrySetOverride("AUTHOR", string.Empty));
        Assert.True(config.TrySetOverride("TITLE_NOTE", string.Empty));

        Assert.Equal(string.Empty, authorItem.EffectiveValue);
        Assert.Equal(ConfigSource.Override, authorItem.Source);
        Assert.Equal(string.Empty, authorItem.OverrideValue);

        Assert.Equal(string.Empty, noteItem.EffectiveValue);
        Assert.Equal(ConfigSource.Override, noteItem.Source);
        Assert.Equal(string.Empty, noteItem.OverrideValue);

        // 验证 ActiveOverrides 正确导出空字符串，不被视为空白而丢弃
        var overrides = config.GetActiveOverrides();
        Assert.Equal(string.Empty, overrides["AUTHOR"]);
        Assert.Equal(string.Empty, overrides["TITLE_NOTE"]);

        // 生成 TeX 产物，验证是否真实替换为空
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "demo.cpp"), "int main() {}");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(cfgFile.FullName, json);

        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, false, null, overrides);
        var builder = new PdfBuilder(new TestLogger(), options, config, new ManifestResourceManager());
        var texContent = builder.GenerateTexContent_ForTest();

        // 验证 LaTeX 中 author 与 title note 确实为空
        Assert.Contains(@"\author{}", texContent);
        Assert.Contains(@"pdfauthor={},", texContent);
        Assert.DoesNotContain("NonEmptyAuthor", texContent);
        Assert.DoesNotContain("Aiza", texContent);
    }

    [Fact]
    public void ResetToBuiltin_And_ResetToProject_CycleTransitionsCorrectly() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "layout": {
                    "columns": 1
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var item = config.GetItem("LAYOUT_COLUMNS")!;
        Assert.Equal("1", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);

        // 1. 恢复内置默认 (2) -> 产生 Override 记录
        config.ResetToBuiltin("LAYOUT_COLUMNS");
        Assert.Equal("2", item.EffectiveValue);
        Assert.Equal(ConfigSource.Override, item.Source);
        Assert.True(config.GetActiveOverrides().ContainsKey("LAYOUT_COLUMNS"));

        // 2. 恢复项目配置 (1) -> 清理 Override 记录
        config.ResetToProject("LAYOUT_COLUMNS");
        Assert.Equal("1", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);
        Assert.False(config.GetActiveOverrides().ContainsKey("LAYOUT_COLUMNS"));

        // 3. 再次覆盖为 "1"（与项目配置相同）-> 此时等价于项目配置
        config.TrySetOverride("LAYOUT_COLUMNS", "1");
        // ClearOverride 即可撤销
        config.ClearOverride("LAYOUT_COLUMNS");
        Assert.Equal("1", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);
    }

    [Fact]
    public void ResetToBuiltin_WhenNoProjectValueConfigured_ClearsOverrideCompletely() {
        var config = new ProjectConfiguration();
        // 未加载项目配置，完全为内置默认
        var item = config.GetItem("LAYOUT_COLUMNS")!;
        Assert.Null(item.ProjectValue);
        Assert.Equal("2", item.BuiltinValue);
        Assert.Equal(ConfigSource.Builtin, item.Source);

        // 覆盖为 1
        config.TrySetOverride("LAYOUT_COLUMNS", "1");
        Assert.Equal("1", item.EffectiveValue);
        Assert.Equal(ConfigSource.Override, item.Source);
        Assert.True(config.GetActiveOverrides().ContainsKey("LAYOUT_COLUMNS"));

        // 重置为内置默认 -> 因无项目值，应当直接 ClearOverride，不残留无意义的覆盖
        config.ResetToBuiltin("LAYOUT_COLUMNS");
        Assert.Equal("2", item.EffectiveValue);
        Assert.Equal(ConfigSource.Builtin, item.Source);
        Assert.False(config.GetActiveOverrides().ContainsKey("LAYOUT_COLUMNS"));
    }

    [Fact]
    public void ToggleField_WhenProjectHasFalse_SettingTrue_OverridesAndResetsProperly() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "code": {
                    "line_numbers": false
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var item = config.GetItem("CODE_LINE_NUMBERS")!;
        Assert.Equal("false", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);

        // 切换为 true
        Assert.True(config.TrySetOverride("CODE_LINE_NUMBERS", "true"));
        Assert.Equal("true", item.EffectiveValue);
        Assert.Equal(ConfigSource.Override, item.Source);

        // 恢复项目值
        Assert.True(config.ResetToProject("CODE_LINE_NUMBERS"));
        Assert.Equal("false", item.EffectiveValue);
        Assert.Equal(ConfigSource.Project, item.Source);
    }

    [Fact]
    public void TitleContent_CanBeClearedToEmptyString() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "title": {
                    "content": "Initial Title"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var item = config.GetItem("TITLE_CONTENT")!;
        Assert.Equal("Initial Title", item.EffectiveValue);

        // 清空标题
        Assert.True(config.TrySetOverride("TITLE_CONTENT", string.Empty));
        Assert.Equal(string.Empty, item.EffectiveValue);
        Assert.Equal(ConfigSource.Override, item.Source);
    }
}
