using Core.Tui;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// <see cref="TuiOptionsCatalog"/> 与真实配置契约的一致性测试。
/// <para>
/// 这组测试存在的理由：填错配置键<b>不会报错</b>——覆盖应用时只记一条 Warning 然后跳过，
/// 界面照样跑完、构建照样成功，只是用户的修改悄悄没生效。因此必须由测试兜住键名。
/// </para>
/// </summary>
public class TuiOptionsCatalogTests {
    /// <summary>按 TuiRunner 的方式构造：TEX 根对象 + 内嵌默认配置。</summary>
    private static ConfigParser NewTexParser() => new("TEX", new TestLogger());

    [Fact]
    public void AllKeys_AreRegisteredInDefaultConfig() {
        var texParser = NewTexParser();
        var progParser = new ConfigParser("PROGRAM", new TestLogger());

        foreach (var field in TuiOptionsCatalog.Fields) {
            var isProg = string.Equals(field.Section, "PROGRAM", StringComparison.OrdinalIgnoreCase);
            var parser = isProg ? progParser : texParser;

            // 回写当前值即可验证键存在且类型可转换；未注册的键会返回 false
            Assert.True(
                parser.ContainsKey(field.Key),
                $"配置键 \"{field.Key}\"（{field.Label}）在默认配置中未注册。"
            );
            var current = parser[field.Key].GetAsString();
            Assert.True(
                parser.TrySetConfigValueFromString(field.Key, current),
                $"配置键 \"{field.Key}\"（{field.Label}）未被默认配置注册或类型不匹配。"
            );
        }
    }

    [Fact]
    public void Keys_AreUnique() {
        var keys = TuiOptionsCatalog.Fields.Select(f => f.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Labels_AreUnique() {
        // MultiSelectionPrompt 以 Label 同时作为显示文本与选中标识，重复会导致勾选错项
        var labels = TuiOptionsCatalog.Fields.Select(f => f.Label).ToList();
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ChoiceFields_DeclareAtLeastTwoChoices() {
        foreach (var field in TuiOptionsCatalog.Fields.Where(f => f.Kind == TuiFieldKind.Choice)) {
            Assert.NotNull(field.Choices);
            Assert.True(
                field.Choices!.Count >= 2,
                $"Choice 类型字段 \"{field.Label}\" 至少需要两个候选值。"
            );
        }
    }

    [Fact]
    public void NonChoiceFields_DoNotDeclareChoices() {
        foreach (var field in TuiOptionsCatalog.Fields.Where(f => f.Kind != TuiFieldKind.Choice)) {
            Assert.Null(field.Choices);
        }
    }

    [Fact]
    public void ChoiceFields_DefaultValuesAreAmongTheirChoices() {
        // 否则界面上无法回到配置文件的原始值
        var parser = NewTexParser();

        foreach (var field in TuiOptionsCatalog.Fields.Where(f => f.Kind == TuiFieldKind.Choice)) {
            var current = parser[field.Key].GetAsString();
            Assert.Contains(current, field.Choices!);
        }
    }

    [Fact]
    public void Catalog_ExposesAroundTenFastConfigItems() {
        // UX-12 聚焦标题、作者、代码字号、行距、页面方向、分栏、边距和文件范围等高频调整项 (18 项)
        Assert.InRange(TuiOptionsCatalog.Fields.Count, 8, 20);
    }
}
