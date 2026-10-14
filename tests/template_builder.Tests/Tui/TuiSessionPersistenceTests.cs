using System.IO;
using Core;
using Core.Models;
using Core.Tui;
using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiSessionPersistenceTests {
    [Fact]
    public void ProjectSession_CrossStepReturn_PreservesAllPathsAndOverrides() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "mysrc"));
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "proj_config.json"));
        var tmplDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "mytemplates"));

        var session = new ProjectSession {
            SourceDir = srcDir,
            OutputPdf = outPdf,
            ConfigFile = cfgFile,
            TemplateDir = tmplDir,
            Verbose = true,
            UserProvidedConfig = true,
            LastFocusedCategoryId = "category_code"
        };

        // 模拟用户在步骤 2 调整了若干项配置
        session.Configuration.TrySetOverride("CODE_FONT_SIZE", "8pt");
        session.Configuration.TrySetOverride("CODE_MINTED_STYLE", "autumn");

        // 用户返回步骤 1 (MODIFY_PATHS)，修改输出 PDF 路径
        var newOutPdf = new FileInfo(Path.Combine(tmp.Path, "out_v2.pdf"));
        session.OutputPdf = newOutPdf;

        // 验证返回步骤 1 并再次进入构建时，先前设置的覆盖项完全无损保留
        var overrides = session.GetOverrides();
        Assert.Equal(2, overrides.Count);
        Assert.Equal("8pt", overrides["CODE_FONT_SIZE"]);
        Assert.Equal("autumn", overrides["CODE_MINTED_STYLE"]);

        var buildOptions = session.ToBuildOptions();
        Assert.Equal(srcDir.FullName, buildOptions.SourceDir.FullName);
        Assert.Equal(newOutPdf.FullName, buildOptions.OutputPdf.FullName);
        Assert.Equal(cfgFile.FullName, buildOptions.ConfigFile.FullName);
        Assert.Equal(tmplDir.FullName, buildOptions.TemplateDir?.FullName);
        Assert.True(buildOptions.Verbose);
        Assert.Equal(2, buildOptions.ConfigOverrides?.Count);
    }

    [Fact]
    public void MapFailureToCategory_MapsKnownErrorsToCorrectCategory() {
        // 1. 字体缺失错误
        var fontFailure = BuildResult.CreateFailure(
            ExitCodes.XelatexFailure,
            BuildStatus.Failed,
            "LaTeX compilation error",
            ["! Package fontspec Error: The font 'Fira Code' cannot be found."],
            failureReason: "未安装指定西文主字体",
            recommendation: "请检查西文主字体设置或系统字体安装"
        );
        Assert.Equal("fonts", TuiRunner.MapFailureToCategory(fontFailure));

        // 2. Minted / pygments 错误
        var mintedFailure = BuildResult.CreateFailure(
            ExitCodes.XelatexFailure,
            BuildStatus.Failed,
            "LaTeX compilation error",
            ["! Package minted Error: You must invoke LaTeX with the -shell-escape flag."],
            failureReason: "minted 宏包要求启用 shell-escape",
            recommendation: "请检查 pygmentize 是否安装"
        );
        Assert.Equal("category_code", TuiRunner.MapFailureToCategory(mintedFailure));

        // 3. 边距与排版尺寸错误
        var geometryFailure = BuildResult.CreateFailure(
            ExitCodes.InvalidArguments,
            BuildStatus.Failed,
            "配置校验失败",
            ["GEOMETRY_LEFT_MARGIN: 非法尺寸格式"],
            failureReason: "版心边距非法",
            recommendation: "请在 geometry_layout 中配置有效 LaTeX 尺寸"
        );
        Assert.Equal("geometry_layout", TuiRunner.MapFailureToCategory(geometryFailure));

        // 4. 成功构建返回 null
        using var tmp = TempDir.Create();
        var dummyPdf = new FileInfo(Path.Combine(tmp.Path, "dummy.pdf"));
        File.WriteAllText(dummyPdf.FullName, "%PDF-1.4");
        var successResult = BuildResult.CreateSuccess(dummyPdf, "mid.tex", "mid.log", 2, TimeSpan.FromSeconds(1));
        Assert.Null(TuiRunner.MapFailureToCategory(successResult));
    }

    [Fact]
    public void ProjectSession_FocusMemory_RetainsCategoryIdAcrossOperations() {
        var session = new ProjectSession();
        Assert.Null(session.LastFocusedCategoryId);

        session.LastFocusedCategoryId = "fonts";
        Assert.Equal("fonts", session.LastFocusedCategoryId);

        session.LastFocusedCategoryId = "category_code";
        Assert.Equal("category_code", session.LastFocusedCategoryId);
    }

    [Fact]
    public void ProjectSession_TracksConfigurationLoadedState() {
        var session = new ProjectSession();
        Assert.False(session.IsConfigurationLoaded);
        Assert.Null(session.LoadedConfigPath);

        session.LoadConfig("""
        {
            "TEX": {
                "layout": { "columns": 1 }
            }
        }
        """, "/tmp/test-config.json");

        Assert.True(session.IsConfigurationLoaded);
        Assert.Equal("/tmp/test-config.json", session.LoadedConfigPath);
        Assert.Equal("1", session.Configuration.Items["LAYOUT_COLUMNS"].EffectiveValue);
    }

    [Fact]
    public void LoadOrRecoverConfiguration_LoadsExplicitConfigFile_AndSetsLoadedState() {
        using var tmp = TempDir.Create();
        var cfgPath = Path.Combine(tmp.Path, "custom.json");
        File.WriteAllText(cfgPath, """
        {
            "TEX": {
                "layout": { "columns": 1 }
            }
        }
        """);

        var session = new ProjectSession(new TestLogger()) {
            ConfigFile = new FileInfo(cfgPath),
            UserProvidedConfig = true
        };
        Assert.False(session.IsConfigurationLoaded);
        // 初始构造的配置为默认值 2
        Assert.Equal("2", session.Configuration.Items["LAYOUT_COLUMNS"].EffectiveValue);

        var launch = new TuiLaunchOptions(
            new DirectoryInfo(tmp.Path),
            new FileInfo(Path.Combine(tmp.Path, "out.pdf")),
            new FileInfo(cfgPath),
            UserProvidedConfig: true,
            TemplateDir: null,
            Verbose: false,
            EnsureDefaultConfig: () => new FileInfo(cfgPath)
        );

        var runner = new TuiRunner(new TestLogger());
        runner.LoadOrRecoverConfiguration(session, launch, new TestLogger());

        Assert.True(session.IsConfigurationLoaded);
        Assert.Equal(cfgPath, session.LoadedConfigPath);
        // 真实加载了配置文件的 columns = 1
        Assert.Equal("1", session.Configuration.Items["LAYOUT_COLUMNS"].EffectiveValue);
    }
}
