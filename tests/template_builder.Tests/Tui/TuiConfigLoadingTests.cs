using Core.Tui;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiConfigLoadingTests {
    [Fact]
    public void ReadConfigValues_PopulatesAllFieldsCorrectly_WithoutCorruptedJsonObjectStrings() {
        using var tmp = TempDir.Create();
        var cfgPath = Path.Combine(tmp.Path, "config.json");
        // 导出默认配置作为基准
        var defaultJson = new ManifestResourceManager().GetResourceInString("DefaultConfig.jsonc");
        File.WriteAllText(cfgPath, defaultJson);

        var (current, defaults) = TuiRunner.ReadConfigValues(new FileInfo(cfgPath));

        // 验证所有 95 项配置均被成功获取（88 项 TEX + 7 项 PROGRAM）
        Assert.Equal(95, current.Count);
        Assert.Equal(95, defaults.Count);

        // 验证没有任何字段取值变成 "{}"（防止之前错误的 fallback 对象导致值被污染）
        foreach (var (k, v) in current) {
            Assert.NotEqual("{}", v);
        }
        foreach (var (k, v) in defaults) {
            Assert.NotEqual("{}", v);
        }

        // 验证留空的字段取到的是空字符串 "" 而不是 "{}"
        Assert.True(current.ContainsKey("CODE_XRIGHTMARGIN"));
        Assert.Equal(string.Empty, current["CODE_XRIGHTMARGIN"]);
        Assert.Equal(string.Empty, defaults["CODE_XRIGHTMARGIN"]);

        Assert.True(current.ContainsKey("CODE_XLEFTMARGIN"));
        Assert.Equal(string.Empty, current["CODE_XLEFTMARGIN"]);

        Assert.True(current.ContainsKey("CODE_ESCAPEINSIDE"));
        Assert.Equal(string.Empty, current["CODE_ESCAPEINSIDE"]);

        // 验证 PROGRAM 字段被正确读取（而不是此前因调用 texParser 而 fallback 到 "{}"）
        Assert.True(current.ContainsKey("BUILD_PASS_COUNT"));
        Assert.Equal("2", current["BUILD_PASS_COUNT"]);

        Assert.True(current.ContainsKey("BUILD_TIMEOUT_SECONDS"));
        Assert.Equal("120", current["BUILD_TIMEOUT_SECONDS"]);
    }

    [Fact]
    public void ReadConfigValues_DoesNotPolluteLogOrThrowErrors() {
        using var tmp = TempDir.Create();
        var cfgPath = Path.Combine(tmp.Path, "config.json");
        File.WriteAllText(cfgPath, "{}");

        // 即使用户配置为空文件，也应当平滑回退到默认配置，不产生异常
        var (current, defaults) = TuiRunner.ReadConfigValues(new FileInfo(cfgPath));
        Assert.NotEmpty(current);
        Assert.NotEmpty(defaults);
        Assert.Equal("Times New Roman", defaults["GLOBAL_MAIN_FONT"]);
    }

    [Fact]
    public void ReadConfigValues_WhenCorruptedJson_ThrowsMalformedConfigException_DoesNotDisguiseAsDefaults() {
        using var tmp = TempDir.Create();
        var cfgPath = Path.Combine(tmp.Path, "corrupted.json");
        File.WriteAllText(cfgPath, "{ invalid json content }");

        // 绝不能吞掉解析异常并伪装成成功加载默认配置
        Assert.Throws<Utils.Exceptions.MalformedConfigException>(() =>
            TuiRunner.ReadConfigValues(new FileInfo(cfgPath)));
    }
}
