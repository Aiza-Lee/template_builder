using System;
using System.IO;
using System.Linq;
using Core;
using Core.Models;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests;

public class ProjectConfigurationTests {
    [Fact]
    public void ProjectConfiguration_InitializesBuiltinDefaults_AllSourceBuiltin() {
        var config = new ProjectConfiguration();

        Assert.NotEmpty(config.Items);
        // 验证默认配置项数量
        Assert.True(config.Items.Count >= 90);

        // 默认情况下所有项均为 Builtin 来源
        foreach (var item in config.Items.Values) {
            Assert.Equal(ConfigSource.Builtin, item.Source);
            Assert.False(item.IsOverridden);
            Assert.False(item.HasProjectValue);
            Assert.Equal(item.BuiltinValue, item.EffectiveValue);
        }

        // 检查特定关键字段的内置默认值
        Assert.Equal("Times New Roman", config.GetEffectiveValue("GLOBAL_MAIN_FONT"));
        Assert.Equal("2", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("2", config.GetEffectiveValue("BUILD_PASS_COUNT"));
    }

    [Fact]
    public void LoadProjectConfig_UpdatesExplicitKeysToProjectSource_KeepsOthersBuiltin() {
        var config = new ProjectConfiguration();

        const string json = """
        {
            "TEX": {
                "author": "TestAuthor",
                "layout": {
                    "columns": 1
                }
            },
            "PROGRAM": {
                "build": {
                    "pass_count": 3
                }
            }
        }
        """;

        config.LoadProjectConfig(json);

        // 显式配置的项应为 Project 来源
        var authorItem = config.GetItem("AUTHOR");
        Assert.NotNull(authorItem);
        Assert.Equal(ConfigSource.Project, authorItem.Source);
        Assert.Equal("TestAuthor", authorItem.ProjectValue);
        Assert.Equal("TestAuthor", authorItem.EffectiveValue);
        Assert.True(authorItem.HasProjectValue);
        Assert.False(authorItem.IsOverridden);

        var columnsItem = config.GetItem("LAYOUT_COLUMNS");
        Assert.NotNull(columnsItem);
        Assert.Equal(ConfigSource.Project, columnsItem.Source);
        Assert.Equal("1", columnsItem.ProjectValue);
        Assert.Equal("1", columnsItem.EffectiveValue);

        var passCountItem = config.GetItem("BUILD_PASS_COUNT");
        Assert.NotNull(passCountItem);
        Assert.Equal(ConfigSource.Project, passCountItem.Source);
        Assert.Equal("3", passCountItem.ProjectValue);
        Assert.Equal("3", passCountItem.EffectiveValue);

        // 未在 JSON 中指定的项仍为 Builtin 来源
        var fontItem = config.GetItem("GLOBAL_MAIN_FONT");
        Assert.NotNull(fontItem);
        Assert.Equal(ConfigSource.Builtin, fontItem.Source);
        Assert.Null(fontItem.ProjectValue);
        Assert.Equal("Times New Roman", fontItem.EffectiveValue);

        // 底层 Parser 也同步更新
        Assert.Equal(3, config.ProgramParser["BUILD_PASS_COUNT"].GetAsInt());
        Assert.Equal("TestAuthor", config.TexParser["AUTHOR"].GetAsString());
    }

    [Fact]
    public void TrySetOverride_TexAndProgramKeys_UpdatesSourceToOverride() {
        var config = new ProjectConfiguration();

        Assert.True(config.TrySetOverride("AUTHOR", "OverriddenAuthor"));
        Assert.True(config.TrySetOverride("BUILD_PASS_COUNT", "4"));

        var authorItem = config.GetItem("AUTHOR");
        Assert.NotNull(authorItem);
        Assert.Equal(ConfigSource.Override, authorItem.Source);
        Assert.True(authorItem.IsOverridden);
        Assert.Equal("OverriddenAuthor", authorItem.OverrideValue);
        Assert.Equal("OverriddenAuthor", authorItem.EffectiveValue);

        var passItem = config.GetItem("BUILD_PASS_COUNT");
        Assert.NotNull(passItem);
        Assert.Equal(ConfigSource.Override, passItem.Source);
        Assert.Equal("4", passItem.EffectiveValue);

        var overrides = config.GetActiveOverrides();
        Assert.Equal(2, overrides.Count);
        Assert.Equal("OverriddenAuthor", overrides["AUTHOR"]);
        Assert.Equal("4", overrides["BUILD_PASS_COUNT"]);

        // Parser 中也同步
        Assert.Equal(4, config.ProgramParser["BUILD_PASS_COUNT"].GetAsInt());
        Assert.Equal("OverriddenAuthor", config.TexParser["AUTHOR"].GetAsString());
    }

    [Fact]
    public void ClearOverride_RevertsToProjectOrBuiltinSource() {
        var config = new ProjectConfiguration();
        const string json = """
        {
            "TEX": {
                "author": "ProjectAuthor"
            }
        }
        """;
        config.LoadProjectConfig(json);

        // 场景 1: 有项目值的字段被覆盖后清除
        config.TrySetOverride("AUTHOR", "TempAuthor");
        Assert.Equal("TempAuthor", config.GetEffectiveValue("AUTHOR"));
        Assert.Equal(ConfigSource.Override, config.GetSource("AUTHOR"));

        config.ClearOverride("AUTHOR");
        Assert.Equal("ProjectAuthor", config.GetEffectiveValue("AUTHOR"));
        Assert.Equal(ConfigSource.Project, config.GetSource("AUTHOR"));
        Assert.Equal("ProjectAuthor", config.TexParser["AUTHOR"].GetAsString());

        // 场景 2: 无项目值（内置默认）的字段被覆盖后清除
        config.TrySetOverride("LAYOUT_COLUMNS", "1");
        Assert.Equal("1", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal(ConfigSource.Override, config.GetSource("LAYOUT_COLUMNS"));

        config.ClearOverride("LAYOUT_COLUMNS");
        Assert.Equal("2", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal(ConfigSource.Builtin, config.GetSource("LAYOUT_COLUMNS"));
        Assert.Equal("2", config.TexParser["LAYOUT_COLUMNS"].GetAsString());
    }

    [Fact]
    public void ResetToBuiltin_WhenProjectValueExists_OverridesWithBuiltinValue() {
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

        // 重置为内置默认：因项目本身配置了 1，需通过覆盖内置值 2 才能在构建时生效为 2（解决 UX-03 核心 Bug）
        Assert.True(config.ResetToBuiltin("LAYOUT_COLUMNS"));
        Assert.Equal("2", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("2", config.TexParser["LAYOUT_COLUMNS"].GetAsString());

        // 再重置回项目值
        Assert.True(config.ResetToProject("LAYOUT_COLUMNS"));
        Assert.Equal("1", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("1", config.TexParser["LAYOUT_COLUMNS"].GetAsString());
    }

    [Fact]
    public void ProjectConfiguration_StrictValidation_ThrowsOnUnknownKey() {
        var config = new ProjectConfiguration(null, ConfigStrictness.Strict);
        const string invalidJson = """
        {
            "TEX": {
                "non_existent_key": "bad"
            }
        }
        """;

        Assert.Throws<UnknownConfigKeyException>(() => config.LoadProjectConfig(invalidJson));
    }

    [Fact]
    public void ProjectSession_TracksState_AndProducesBuildOptionsWithOverrides() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var session = new ProjectSession {
            SourceDir = srcDir,
            OutputPdf = outPdf,
            ConfigFile = cfgFile,
            Verbose = true
        };

        session.Configuration.TrySetOverride("LAYOUT_COLUMNS", "1");
        session.Configuration.TrySetOverride("BUILD_PASS_COUNT", "3");

        var opts = session.ToBuildOptions();
        Assert.Equal(srcDir.FullName, opts.SourceDir.FullName);
        Assert.Equal(outPdf.FullName, opts.OutputPdf.FullName);
        Assert.Equal(cfgFile.FullName, opts.ConfigFile.FullName);
        Assert.True(opts.Verbose);
        Assert.NotNull(opts.ConfigOverrides);
        Assert.Equal(2, opts.ConfigOverrides.Count);
        Assert.Equal("1", opts.ConfigOverrides["LAYOUT_COLUMNS"]);
        Assert.Equal("3", opts.ConfigOverrides["BUILD_PASS_COUNT"]);
    }

    [Fact]
    public void BuildResult_SuccessAndFailure_EncapsulatesStructuredData() {
        var pdfInfo = new FileInfo("/fake/output.pdf");
        var successResult = BuildResult.CreateSuccess(
            pdfInfo,
            "/fake/mid-output.tex",
            "/fake/mid-output.log",
            2,
            TimeSpan.FromSeconds(1.5),
            ["Warning: font substitute"]
        );

        Assert.True(successResult.IsSuccess);
        Assert.Equal(BuildStatus.SuccessWithWarnings, successResult.Status);
        Assert.Equal(ExitCodes.Success, successResult.ExitCode);
        Assert.Equal(pdfInfo.FullName, successResult.OutputPdf?.FullName);
        Assert.Equal(2, successResult.PassesCompleted);
        Assert.Single(successResult.Warnings);

        var failResult = BuildResult.CreateFailure(
            ExitCodes.XelatexFailure,
            BuildStatus.Failed,
            "LaTeX compilation error",
            ["! Undefined control sequence."],
            "/fake/mid-output.log",
            TimeSpan.FromSeconds(0.8),
            passesCompleted: 1
        );

        Assert.False(failResult.IsSuccess);
        Assert.Equal(BuildStatus.Failed, failResult.Status);
        Assert.Equal(ExitCodes.XelatexFailure, failResult.ExitCode);
        Assert.Equal("LaTeX compilation error", failResult.ErrorMessage);
        Assert.Single(failResult.Diagnostics);
        Assert.Equal(1, failResult.PassesCompleted);
    }

    [Fact]
    public void SaveToFile_CreatesValidJsonAndPromotesOverridesToProjectValues() {
        using var tmp = TempDir.Create();
        var targetFile = Path.Combine(tmp.Path, "saved_config.json");

        var config = new ProjectConfiguration();
        config.TrySetOverride("CODE_FONT_SIZE", "9pt");
        config.TrySetOverride("CODE_MINTED_STYLE", "monokai");
        config.TrySetOverride("AUTHOR", "Custom Author");

        Assert.Equal(3, config.GetActiveOverrides().Count);
        Assert.True(config.GetItem("CODE_FONT_SIZE")!.IsOverridden);

        // 保存到文件
        config.SaveToFile(targetFile);

        Assert.True(File.Exists(targetFile));

        // 验证内存状态：覆盖项已全部晋升为 ProjectValue，覆盖状态被清空
        Assert.Empty(config.GetActiveOverrides());
        var savedItem = config.GetItem("CODE_FONT_SIZE")!;
        Assert.False(savedItem.IsOverridden);
        Assert.True(savedItem.HasProjectValue);
        Assert.Equal("9pt", savedItem.EffectiveValue);
        Assert.Equal(ConfigSource.Project, savedItem.Source);

        // 验证文件持久化：重新加载后依然为 9pt、monokai、Custom Author
        var freshConfig = new ProjectConfiguration();
        freshConfig.LoadProjectConfig(File.ReadAllText(targetFile), targetFile);

        Assert.Equal("9pt", freshConfig.GetEffectiveValue("CODE_FONT_SIZE"));
        Assert.Equal("monokai", freshConfig.GetEffectiveValue("CODE_MINTED_STYLE"));
        Assert.Equal("Custom Author", freshConfig.GetEffectiveValue("AUTHOR"));
    }

    [Fact]
    public void SaveToFile_PreservesCustomSettingsWhenUpdatingExistingFile() {
        using var tmp = TempDir.Create();
        var targetFile = Path.Combine(tmp.Path, "existing_config.json");
        const string initialJson = """
        {
            "$schema": "./DefaultConfig.schema.json",
            "TEX": {
                "author": "Initial Author",
                "layout": {
                    "columns": 1
                }
            }
        }
        """;
        File.WriteAllText(targetFile, initialJson);

        var config = new ProjectConfiguration();
        config.LoadProjectConfig(initialJson, targetFile);

        // 覆盖 columns 为 2，设置新标题
        config.TrySetOverride("LAYOUT_COLUMNS", "2");
        config.TrySetOverride("TITLE_CONTENT", "My New Document");

        config.SaveToFile(targetFile);

        var content = File.ReadAllText(targetFile);
        Assert.Contains("My New Document", content);
        Assert.Contains("\"columns\": 2", content);

        var freshConfig = new ProjectConfiguration();
        freshConfig.LoadProjectConfig(content, targetFile);
        Assert.Equal("2", freshConfig.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("My New Document", freshConfig.GetEffectiveValue("TITLE_CONTENT"));
        Assert.Equal("Initial Author", freshConfig.GetEffectiveValue("AUTHOR"));
    }

    [Fact]
    public void LoadProjectConfig_StrictMode_RejectsUnknownRootProperty() {
        var config = new ProjectConfiguration(strictness: ConfigStrictness.Strict);
        const string json = """
        {
            "INVALID_ROOT": {
                "author": "Alice"
            }
        }
        """;
        var ex = Assert.Throws<MalformedConfigException>(() => config.LoadProjectConfig(json));
        Assert.Contains("包含未知属性 'INVALID_ROOT'", ex.Message);
    }

    [Fact]
    public void LoadProjectConfig_SparseConfigs_LoadsSuccessfully() {
        var texOnlyConfig = new ProjectConfiguration(strictness: ConfigStrictness.Strict);
        texOnlyConfig.LoadProjectConfig("""{ "TEX": { "author": "Alice" } }""");
        Assert.Equal("Alice", texOnlyConfig.GetEffectiveValue("AUTHOR"));

        var progOnlyConfig = new ProjectConfiguration(strictness: ConfigStrictness.Strict);
        progOnlyConfig.LoadProjectConfig("""{ "PROGRAM": { "build": { "pass_count": 1 } } }""");
        Assert.Equal("1", progOnlyConfig.GetEffectiveValue("BUILD_PASS_COUNT"));

        var emptyConfig = new ProjectConfiguration(strictness: ConfigStrictness.Strict);
        emptyConfig.LoadProjectConfig("{}");
        Assert.Equal("Times New Roman", emptyConfig.GetEffectiveValue("GLOBAL_MAIN_FONT"));
    }
}
