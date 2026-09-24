using Utils;
using Xunit;

namespace template_builder.Tests;

/// <summary>
/// <see cref="ConfigParser.TrySetConfigValueFromString"/> 单元测试。
/// 该方法是 TUI 交互模式注入配置覆盖的唯一入口，因此需要锁住两点：
/// 按默认配置的 JSON 类型正确强制转换，以及非法输入必须「返回 false 且不改动原值」——
/// 静默写入错误类型会让构建在更晚、更难定位的地方炸掉。
/// </summary>
public class ConfigParserOverrideTests {
    private static ConfigParser NewTexParser() => new("TEX", new TestLogger());

    [Fact]
    public void TrySet_StringKey_OverridesValue() {
        var parser = NewTexParser();

        Assert.True(parser.TrySetConfigValueFromString("TITLE_CONTENT", "我的标题"));

        Assert.Equal("我的标题", parser["TITLE_CONTENT"].GetAsString());
    }

    [Fact]
    public void TrySet_NumberKey_CoercesFromString() {
        var parser = NewTexParser();

        Assert.True(parser.TrySetConfigValueFromString("LAYOUT_COLUMNS", "1"));

        Assert.Equal(1, parser["LAYOUT_COLUMNS"].GetAsInt());
    }

    [Fact]
    public void TrySet_BoolKey_CoercesFromString() {
        var parser = NewTexParser();

        Assert.True(parser.TrySetConfigValueFromString("TYPESETTING_PARSKIP_ENABLED", "true"));

        Assert.True(parser["TYPESETTING_PARSKIP_ENABLED"].GetAsBool());
    }

    [Fact]
    public void TrySet_NumberKey_WithNonNumeric_ReturnsFalseAndKeepsOriginalValue() {
        var parser = NewTexParser();
        var original = parser["LAYOUT_COLUMNS"].GetAsInt();

        Assert.False(parser.TrySetConfigValueFromString("LAYOUT_COLUMNS", "两列"));

        Assert.Equal(original, parser["LAYOUT_COLUMNS"].GetAsInt());
    }

    [Fact]
    public void TrySet_BoolKey_WithNonBool_ReturnsFalse() {
        var parser = NewTexParser();

        Assert.False(parser.TrySetConfigValueFromString("TYPESETTING_PARSKIP_ENABLED", "yes"));
    }

    [Fact]
    public void TrySet_UnknownKey_ReturnsFalse() {
        var parser = NewTexParser();

        Assert.False(parser.TrySetConfigValueFromString("NOT_A_REAL_KEY", "x"));
    }

    [Fact]
    public void TrySet_ArrayKey_JsonSyntax_Succeeds() {
        var parser = NewTexParser();

        Assert.True(parser.TrySetConfigValueFromString("METADATA_KEYWORDS", "[\"a\", \"b\"]"));
        Assert.Equal(["a", "b"], parser["METADATA_KEYWORDS"].GetAsStringArray());
    }

    [Fact]
    public void TrySet_ArrayKey_CommaSeparated_Succeeds() {
        var parser = NewTexParser();

        Assert.True(parser.TrySetConfigValueFromString("METADATA_KEYWORDS", "kw1, kw2"));
        Assert.Equal(["kw1", "kw2"], parser["METADATA_KEYWORDS"].GetAsStringArray());
    }

    [Fact]
    public void TrySet_ValueWithQuotesAndBackslashes_IsStoredVerbatim() {
        // 值里的引号 / 反斜杠是正则的 JSON 转义风险点，必须原样保存
        var parser = NewTexParser();
        var tricky = "a\"b\\c_{d}";

        Assert.True(parser.TrySetConfigValueFromString("TITLE_CONTENT", tricky));

        Assert.Equal(tricky, parser["TITLE_CONTENT"].GetAsString());
    }

    [Fact]
    public void TrySet_DoesNotAffectKeysOwnedByOtherRootObject() {
        // PROGRAM 的键在 TEX 解析器上必须不被接受，否则覆盖会被路由到错误的根对象
        var parser = NewTexParser();

        Assert.False(parser.TrySetConfigValueFromString("BUILD_PASS_COUNT", "1"));
    }
}
