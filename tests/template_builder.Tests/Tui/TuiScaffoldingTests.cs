using Core.Tui;
using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiScaffoldingTests {
    [Fact]
    public void GenerateAllSnapshots_CreatesAllExpectedArtifacts() {
        using var tmp = TempDir.Create();
        var outDir = Path.Combine(tmp.Path, "tui-scaffold");

        var files = TuiScaffolding.GenerateAllSnapshots(outDir, width: 80);

        Assert.NotEmpty(files);
        Assert.True(File.Exists(Path.Combine(outDir, "SUMMARY.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "01_banner.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "01_banner.ansi")));
        Assert.True(File.Exists(Path.Combine(outDir, "02_step1_paths.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "03_step2_category_menu.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "04_step2_fields_list.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "05_step2_field_card.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "06_overrides_table.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "07_step3_dashboard.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "08_step2_source_tree_preview.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "09_step2_presets_preview.txt")));

        var summaryContent = File.ReadAllText(Path.Combine(outDir, "SUMMARY.md"));
        Assert.Contains("品牌霓虹艺术字 Banner", summaryContent);
        Assert.Contains("步骤 1: 路径与运行参数向导", summaryContent);
        Assert.Contains("步骤 2: 配置调优分类导航", summaryContent);
        Assert.Contains("步骤 3: 实时监控与完成仪表板", summaryContent);
        Assert.Contains("步骤 2: 源码收录与章节结构预检面板", summaryContent);
        Assert.Contains("步骤 2: 排版预设体系概览与选择", summaryContent);
    }

    [Fact]
    public void RenderToPlainText_StripsAnsiControlCodes() {
        var text = TuiScaffolding.RenderToPlainText(c => {
            TuiViewRenderer.RenderBanner(c, width: 80);
        });

        Assert.Contains("ACM/ICPC 代码模板生成工具", text);
        var escIdx = text.IndexOf('\u001b');
        Assert.True(escIdx < 0, escIdx >= 0 ? $"Found ESC at {escIdx}: '{(int)text[escIdx]}' '{text[escIdx..Math.Min(text.Length, escIdx + 20)].Replace("\u001b", "<ESC>")}'" : "");
    }

    [Fact]
    public void RenderToAnsi_PreservesColorCodes() {
        var ansiText = TuiScaffolding.RenderToAnsi(c => {
            TuiViewRenderer.RenderBanner(c, width: 80);
        });

        Assert.Contains("ACM/ICPC 代码模板生成工具", ansiText);
        // 确保带色彩输出中保留有 ANSI 颜色控制字符
        Assert.Contains("\u001b", ansiText);
    }

    [Fact]
    public void RenderBanner_AdaptsGracefullyToNarrowTerminals() {
        // 窄屏测试 (宽 50 列)
        var narrowText = TuiScaffolding.RenderToPlainText(c => {
            TuiViewRenderer.RenderBanner(c, width: 50);
        });
        Assert.Contains("ACM/ICPC 代码模板生成工具", narrowText);

        // 极窄屏测试 (宽 35 列)
        var ultraNarrowText = TuiScaffolding.RenderToPlainText(c => {
            TuiViewRenderer.RenderBanner(c, width: 35);
        });
        Assert.Contains("=== Template Builder ===", ultraNarrowText);
    }
}
