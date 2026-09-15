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
        var parser = NewTexParser();

        foreach (var field in TuiOptionsCatalog.Fields) {
            // 回写当前值即可验证键存在且类型可转换；未注册的键会返回 false
            var current = parser[field.Key].GetAsString();
            Assert.False(
                string.IsNullOrEmpty(current),
                $"配置键 \"{field.Key}\"（{field.Label}）在默认配置中取不到值，键名可能有误。"
            );
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
        // 任务约定「约 10 项」：守住这个量级，避免清单无限膨胀成第二个配置文件
        Assert.InRange(TuiOptionsCatalog.Fields.Count, 8, 15);
    }
}
