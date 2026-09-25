using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Core.Models;
using Core.SourceTree;
using Core.Tui;
using Spectre.Console;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// 跨终端尺寸与文本布局鲁棒性验收测试套件 (UX-15)。
/// 覆盖：
/// 1. 终端尺寸（极窄 35~40 列、窄屏 50~60 列、基准 80 列、宽屏 120~160 列）；
/// 2. 超长目录与文件路径在卡片与面板中的安全呈现；
/// 3. 中文/CJK 字符在各渲染组件中的对齐与换行；
/// 4. TuiLineEditor 在 CJK 与复杂 ASCII 混排下的光标、宽度计算与编辑操作；
/// 5. 非交互模式环境检测。
/// </summary>
public class CrossTerminalLayoutTests {
    private static readonly int[] TerminalWidths = [35, 40, 50, 60, 75, 80, 120, 160];

    [Theory]
    [InlineData(35)]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(75)]
    [InlineData(80)]
    [InlineData(120)]
    [InlineData(160)]
    public void Banner_RendersAcrossAllTerminalWidths_WithoutException(int width) {
        var text = TuiScaffolding.RenderToPlainText(c => TuiViewRenderer.RenderBanner(c, width), width: width);
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        if (width >= 75) {
            Assert.Contains("ACM/ICPC 代码模板生成工具", text);
        } else if (width >= 48) {
            Assert.Contains("ACM/ICPC 代码模板生成工具", text);
        } else {
            Assert.Contains("=== Template Builder ===", text);
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void Breadcrumb_RendersAcrossVariousWidths(int width) {
        for (int step = 1; step <= 3; step++) {
            var text = TuiScaffolding.RenderToPlainText(
                c => TuiViewRenderer.RenderBreadcrumb(c, step, "步骤导航测试"),
                width: width
            );
            Assert.Contains("步骤", text);
            Assert.Contains("步骤导航测试", text);
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void FieldCard_WithLongValueAndChinese_RendersSafely(int width) {
        var field = TuiSchemaCatalog.FieldsByKey["TITLE_CONTENT"];
        var longChineseVal = "ACM/ICPC 2026 算法与数据结构实战竞赛综合代码速查与排版系统手册";
        var configItem = new ConfigItem(
            key: "TITLE_CONTENT",
            section: "TEX",
            builtinValue: "",
            projectValue: "项目旧标题",
            overrideValue: longChineseVal
        );

        var text = TuiScaffolding.RenderToPlainText(
            c => TuiViewRenderer.RenderFieldCard(c, field, configItem),
            width: width
        );

        Assert.Contains("文档标题", text);
        Assert.Contains("TITLE_CONTENT", text);
        Assert.Contains("运行时覆盖", text);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void OverridesTable_WithMultipleOverrides_RendersSafely(int width) {
        var overrides = new Dictionary<string, string> {
            ["TITLE_CONTENT"] = "测试极值标题",
            ["LAYOUT_COLUMNS"] = "1",
            ["CODE_FONT_SIZE"] = "8pt",
            ["DOC_CLASS_ORIENTATION"] = "portrait"
        };
        var config = new ProjectConfiguration();
        config.TrySetOverride("TITLE_CONTENT", "测试极值标题");
        config.TrySetOverride("LAYOUT_COLUMNS", "1");

        var text = TuiScaffolding.RenderToPlainText(
            c => TuiViewRenderer.RenderOverridesTable(c, overrides, config),
            width: width
        );

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("当前已配置的运行时覆盖项", text);
        if (width >= 80) {
            Assert.Contains("测试极值标题", text);
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void CategoryMenuPreview_RendersAcrossWidths(int width) {
        var overrides = new Dictionary<string, string> {
            ["TITLE_CONTENT"] = "标题",
            ["CODE_FONT_SIZE"] = "7pt"
        };

        var text = TuiScaffolding.RenderToPlainText(
            c => TuiViewRenderer.RenderCategoryMenuPreview(c, overrides),
            width: width
        );

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("配置调优分类导航预览", text);
        if (width >= 80) {
            Assert.Contains("常用核心配置", text);
            Assert.Contains("代码排版", text);
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void Dashboard_WithUltraLongPaths_RendersWithoutOverflowCrash(int width) {
        var veryLongPath = "/home/developer/workspace/very/deep/nested/subfolder/hierarchy/projects/icpc_templates_2026/build_artifacts/outputs/final_booklet_release_v2_signed.pdf";
        var sampleLogs = new List<string> {
            "00:01 [INFO] 扫描中...",
            "00:02 [SUCCESS] 构建成功"
        };

        var text = TuiScaffolding.RenderToPlainText(
            c => TuiViewRenderer.RenderDashboard(c, sampleLogs, TimeSpan.FromSeconds(3.14), ExitCodes.Success, veryLongPath),
            width: width
        );

        Assert.Contains("构建成功", text);
        Assert.Contains("3.14s", text);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void SourceTreePreview_RendersGracefullyAcrossWidths(int width) {
        using var tmp = TempDir.Create();
        var deepDir = Path.Combine(tmp.Path, "deep_category_with_long_name_chinese_算法");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "01_segment_tree_线段树.cpp"), "// code");
        File.WriteAllText(Path.Combine(deepDir, "02_fenwick_树状数组.cpp"), "// code");
        File.WriteAllText(Path.Combine(deepDir, "readme.md"), "# doc");
        File.WriteAllText(Path.Combine(deepDir, "ignore.bak"), "temp");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        foreach (var mode in (SourceTreeFilterMode[])Enum.GetValues(typeof(SourceTreeFilterMode))) {
            var text = TuiScaffolding.RenderToPlainText(
                c => TuiViewRenderer.RenderSourceTreePreview(c, plan, mode),
                width: width
            );
            Assert.NotNull(text);
            Assert.NotEmpty(text);
            Assert.Contains("源码收录与章节结构预检面板", text);
            if (width >= 80) {
                Assert.Contains("deep_category", text);
            }
        }
    }

    [Fact]
    public void LineEditor_WithMixedChineseAndAscii_MaintainsAccurateDisplayWidthAndMovement() {
        var editor = new TuiLineEditorState("ACM图论_算法模板");
        // "ACM" (3) + "图论" (4) + "_" (1) + "算法模板" (8) = 16 display columns
        Assert.Equal(16, editor.TotalDisplayWidth);
        Assert.Equal(editor.TotalDisplayWidth, editor.CursorDisplayWidth);
        Assert.Equal(0, editor.RemainingDisplayWidth);

        // Move to start
        editor.MoveHome();
        Assert.Equal(0, editor.Cursor);
        Assert.Equal(0, editor.CursorDisplayWidth);
        Assert.Equal(16, editor.RemainingDisplayWidth);

        // Move right 3 characters -> "ACM"
        editor.MoveRight();
        editor.MoveRight();
        editor.MoveRight();
        Assert.Equal(3, editor.Cursor);
        Assert.Equal(3, editor.CursorDisplayWidth);
        Assert.Equal(13, editor.RemainingDisplayWidth);

        // Move right past "图"
        editor.MoveRight();
        Assert.Equal(4, editor.Cursor);
        Assert.Equal(5, editor.CursorDisplayWidth); // 3 + 2 = 5
        Assert.Equal(11, editor.RemainingDisplayWidth);

        // Insert at cursor
        editor.Insert("网络流_");
        // "ACM图" + "网络流_" (6 + 1 = 7) + "论_算法模板" (2 + 1 + 8 = 11)
        Assert.Contains("ACM图网络流_论_算法模板", editor.Text);

        // Clear
        editor.Clear();
        Assert.Equal("", editor.Text);
        Assert.Equal(0, editor.TotalDisplayWidth);
        Assert.Equal(0, editor.CursorDisplayWidth);
    }
}
