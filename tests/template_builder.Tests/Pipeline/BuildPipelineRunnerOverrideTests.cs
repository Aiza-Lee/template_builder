using Core.Pipeline;
using Utils;
using Xunit;

namespace template_builder.Tests.Pipeline;

/// <summary>
/// <see cref="BuildPipelineRunner.ApplyConfigOverrides"/> 的单元测试：
/// 覆盖值必须落到正确的根对象解析器上，未知键必须留痕而不是被吞掉。
/// </summary>
public class BuildPipelineRunnerOverrideTests {
    private static (ConfigParser Tex, ConfigParser Program, TestLogger Logger) NewParsers() {
        var logger = new TestLogger();
        return (
            new ConfigParser("TEX", logger, ConfigStrictness.Lax),
            new ConfigParser("PROGRAM", logger, ConfigStrictness.Lax),
            logger
        );
    }

    [Fact]
    public void ApplyConfigOverrides_TexKey_UpdatesTexParserOnly() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(
            new Dictionary<string, string> { ["LAYOUT_COLUMNS"] = "1" },
            tex, program, logger
        );

        Assert.Equal(1, tex["LAYOUT_COLUMNS"].GetAsInt());
        // 该键不归属 PROGRAM 根对象：PROGRAM 解析器应拒绝它（若两边都接受，覆盖会被路由到错误对象上）
        Assert.False(program.TrySetConfigValueFromString("LAYOUT_COLUMNS", "1"));
    }

    [Fact]
    public void ApplyConfigOverrides_ProgramKey_UpdatesProgramParser() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(
            new Dictionary<string, string> { ["BUILD_PASS_COUNT"] = "1" },
            tex, program, logger
        );

        Assert.Equal(1, program["BUILD_PASS_COUNT"].GetAsInt());
    }

    [Fact]
    public void ApplyConfigOverrides_UnknownKey_LogsWarning() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(
            new Dictionary<string, string> { ["NO_SUCH_KEY"] = "x" },
            tex, program, logger
        );

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.WARNING && e.Message.Contains("NO_SUCH_KEY")
        );
    }

    [Fact]
    public void ApplyConfigOverrides_InvalidScalarValue_LogsWarning() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(
            new Dictionary<string, string> { ["LAYOUT_COLUMNS"] = "两列" },
            tex, program, logger
        );

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.WARNING && e.Message.Contains("LAYOUT_COLUMNS")
        );
        Assert.Equal(2, tex["LAYOUT_COLUMNS"].GetAsInt()); // 非法值不得写入
    }

    [Fact]
    public void ApplyConfigOverrides_Null_IsNoOp() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(null, tex, program, logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.WARNING);
    }

    [Fact]
    public void ApplyConfigOverrides_Empty_IsNoOp() {
        var (tex, program, logger) = NewParsers();

        BuildPipelineRunner.ApplyConfigOverrides(
            new Dictionary<string, string>(), tex, program, logger
        );

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.WARNING);
    }
}
