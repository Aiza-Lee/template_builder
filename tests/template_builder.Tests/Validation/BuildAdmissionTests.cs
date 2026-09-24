using System.IO;
using Core;
using Core.Models;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Validation;

public class BuildAdmissionTests {
    [Fact]
    public void RunBuild_EmptySourceDirectory_RejectsBeforeCompilationWithValidationFailed() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var emptySrc = Directory.CreateDirectory(Path.Combine(tmp.Path, "empty_src"));
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var options = new BuildSubcommandOptions(emptySrc, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var result = runner.RunBuild(options, userProvidedConfig: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.Equal(BuildStatus.Failed, result.Status);
        Assert.Contains("为空", result.ErrorMessage);

        // 验证没有产生构建副作用（未生成 mid-output.tex）
        var workDir = Path.Combine(emptySrc.FullName, ".template_builder", "build");
        Assert.False(File.Exists(Path.Combine(workDir, "mid-output.tex")));
    }

    [Fact]
    public void RunBuild_AllFilesFiltered_RejectsBeforeCompilationWithValidationFailed() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "archive.zip"), "binary data");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var result = runner.RunBuild(options, userProvidedConfig: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.Contains("未匹配到任何有效源文件", result.ErrorMessage);
    }

    [Fact]
    public void RunBuild_SixLevelDeepDirectory_RejectsBeforeCompilationWithValidationFailed() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var deep = srcDir.FullName;
        for (int i = 0; i < 6; i++) {
            deep = Path.Combine(deep, $"d_{i}");
            Directory.CreateDirectory(deep);
        }
        File.WriteAllText(Path.Combine(deep, "file.cpp"), "// code");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var result = runner.RunBuild(options, userProvidedConfig: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.Contains("嵌套层级过深", result.ErrorMessage);
    }

    [Fact]
    public void RunBuild_InvalidConfigType_TabSizeOops_RejectsBeforeCompilationWithInvalidArguments() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "main.cpp"), "int main() {}");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, """
        {
            "TEX": {
                "code": {
                    "tab_size": "oops"
                }
            }
        }
        """);

        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        // 绝不抛出裸 InvalidCastException，准入校验提前拦截并返回结构化结果与 ExitCodes.InvalidArguments (2)
        var result = runner.RunBuild(options, userProvidedConfig: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.InvalidArguments, result.ExitCode);
        Assert.Equal(BuildStatus.Failed, result.Status);
        Assert.Contains("配置校验失败", result.ErrorMessage);
        Assert.Contains("TEX.code.tab_size", result.ErrorMessage);
    }

    [Fact]
    public void RunBuild_InvalidEnum_ColumnsThree_RejectsBeforeCompilationWithInvalidArguments() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "main.cpp"), "int main() {}");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, """
        {
            "TEX": {
                "layout": {
                    "columns": 3
                }
            }
        }
        """);

        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var result = runner.RunBuild(options, userProvidedConfig: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.InvalidArguments, result.ExitCode);
        Assert.Contains("columns", result.ErrorMessage);
    }
}
