using System;
using System.IO;
using Core.Models;
using Core.Tui;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// 验证 --tui 与 CLI 的 preset / sample 契约与会话贯通 (RC-16)。
/// </summary>
public class TuiPresetAndSampleContractTests {
    [Fact]
    public void TuiLaunchOptions_CarriesPresetAndSampleLimit() {
        var preset = LayoutPresets.Reading;
        var launch = new TuiLaunchOptions(
            SourceDir: new DirectoryInfo("/tmp/src"),
            OutputPdf: new FileInfo("/tmp/out.pdf"),
            ConfigFile: new FileInfo("/tmp/cfg.json"),
            UserProvidedConfig: false,
            TemplateDir: null,
            Verbose: false,
            EnsureDefaultConfig: () => new FileInfo("/tmp/cfg.json"),
            InitialPreset: preset,
            InitialSampleLimit: 1
        );

        Assert.NotNull(launch.InitialPreset);
        Assert.Equal("reading", launch.InitialPreset.Id);
        Assert.Equal(1, launch.InitialSampleLimit);
    }

    [Fact]
    public void TuiSession_ApplyingInitialPreset_SetsOverridesAccurately() {
        var config = new ProjectConfiguration(new TestLogger(), ConfigStrictness.Lax);
        var preset = LayoutPresets.Reading;

        // 模拟向导配置加载后应用 InitialPreset
        LayoutPresets.Apply(config, preset);

        var overrides = config.GetActiveOverrides();
        Assert.True(overrides.ContainsKey("DOCCLASS_ORIENTATION"));
        Assert.Equal("portrait", overrides["DOCCLASS_ORIENTATION"]);
        Assert.True(overrides.ContainsKey("LAYOUT_COLUMNS"));
        Assert.Equal("1", overrides["LAYOUT_COLUMNS"]);
        Assert.True(overrides.ContainsKey("HYPERREF_ENABLE_COLORLINKS"));
        Assert.Equal("true", overrides["HYPERREF_ENABLE_COLORLINKS"]);
    }

    [Fact]
    public void TuiSession_ApplyingCompetitionPreset_SetsLandscapeAndTwoColumns() {
        var config = new ProjectConfiguration(new TestLogger(), ConfigStrictness.Lax);
        var preset = LayoutPresets.Competition;

        LayoutPresets.Apply(config, preset);

        var overrides = config.GetActiveOverrides();
        Assert.Equal("landscape", overrides["DOCCLASS_ORIENTATION"]);
        Assert.Equal("2", overrides["LAYOUT_COLUMNS"]);
        Assert.Equal("6pt", overrides["CODE_FONT_SIZE"]);
        Assert.Equal("false", overrides["TITLE_ENABLE_COVER"]);
        Assert.Equal("true", overrides["TOC_ENABLE_TOC"]);
    }
}
