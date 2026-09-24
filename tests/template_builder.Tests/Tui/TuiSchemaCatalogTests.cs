using Core.Tui;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiSchemaCatalogTests {
    [Fact]
    public void AllFields_DiscoversAllExpectedProperties() {
        var fields = TuiSchemaCatalog.AllFields;
        // Schema 中共 88 个 TEX 属性与 5 个 PROGRAM 属性，共 93 个
        Assert.Equal(93, fields.Count);
        Assert.Equal(93, fields.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Categories_CoverAllFunctionalAreas() {
        var categories = TuiSchemaCatalog.Categories;
        Assert.NotEmpty(categories);

        var categoryTitles = categories.Select(c => c.Title).ToList();
        Assert.Contains("常用核心配置", categoryTitles);
        Assert.Contains("文档与标题页", categoryTitles);
        Assert.Contains("代码排版 (minted)", categoryTitles);
        Assert.Contains("页面版心与布局", categoryTitles);
        Assert.Contains("字体与微排版", categoryTitles);
        Assert.Contains("页眉页脚与样式", categoryTitles);
        Assert.Contains("超链接与目录", categoryTitles);
        Assert.Contains("构建与文件过滤", categoryTitles);

        foreach (var cat in categories) {
            Assert.NotEmpty(cat.Fields);
            Assert.False(string.IsNullOrWhiteSpace(cat.Description));
        }
    }

    [Fact]
    public void AllFields_CanBeResolvedInDefaultConfig() {
        var texLogger = new TestLogger();
        var progLogger = new TestLogger();
        var texParser = new ConfigParser("TEX", texLogger);
        var progParser = new ConfigParser("PROGRAM", progLogger);

        foreach (var field in TuiSchemaCatalog.AllFields) {
            var isProg = string.Equals(field.Section, "PROGRAM", StringComparison.OrdinalIgnoreCase);
            var parser = isProg ? progParser : texParser;

            Assert.True(parser.ContainsKey(field.Key), $"字段 \"{field.Key}\"（所属：{field.Section}）未在默认配置中注册。");
            Assert.True(parser.TryGetConfigValue(field.Key, out _), $"字段 \"{field.Key}\" 无法通过 TryGetConfigValue 获取。");
        }

        Assert.Empty(texLogger.ErrorMessages);
        Assert.Empty(progLogger.ErrorMessages);
    }

    [Fact]
    public void DescriptionsAndConstraints_ArePopulatedFromSchema() {
        var codeField = TuiSchemaCatalog.FieldsByKey["CODE_MINTED_STYLE"];
        Assert.NotNull(codeField.Category);
        Assert.False(string.IsNullOrWhiteSpace(codeField.Label));

        var timeoutField = TuiSchemaCatalog.FieldsByKey["BUILD_TIMEOUT_SECONDS"];
        Assert.Equal(TuiFieldKind.Number, timeoutField.Kind);
        Assert.Contains("超时", timeoutField.Description);
        Assert.NotNull(timeoutField.Constraints);

        var columnsField = TuiSchemaCatalog.FieldsByKey["LAYOUT_COLUMNS"];
        Assert.Equal(TuiFieldKind.Choice, columnsField.Kind);
        Assert.Contains("1", columnsField.Choices!);
        Assert.Contains("2", columnsField.Choices!);
    }

    [Fact]
    public void FeaturedFields_ContainsExactlyTwelveCuratedItems() {
        var featured = TuiSchemaCatalog.FeaturedFields;
        Assert.Equal(12, featured.Count);
        Assert.Equal(featured.Count, featured.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count());
    }
}
