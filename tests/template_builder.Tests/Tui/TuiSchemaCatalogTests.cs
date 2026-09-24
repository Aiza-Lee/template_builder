using Core.Tui;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiSchemaCatalogTests {
    [Fact]
    public void AllFields_DiscoversAllExpectedProperties() {
        var fields = TuiSchemaCatalog.AllFields;
        // Schema 中共 90 个 TEX 属性与 7 个 PROGRAM 属性，共 97 个
        Assert.Equal(97, fields.Count);
        Assert.Equal(97, fields.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count());
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
    public void FeaturedFields_ContainsTwentyCuratedItems() {
        var featured = TuiSchemaCatalog.FeaturedFields;
        Assert.Equal(20, featured.Count);
        Assert.Equal(featured.Count, featured.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count());

        var keys = featured.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("TITLE_CONTENT", keys);
        Assert.Contains("AUTHOR", keys);
        Assert.Contains("TITLE_ENABLE_COVER", keys);
        Assert.Contains("TOC_ENABLE_TOC", keys);
        Assert.Contains("CODE_FONT_SIZE", keys);
        Assert.Contains("CODE_LINE_HEIGHT", keys);
        Assert.Contains("DOCCLASS_ORIENTATION", keys);
        Assert.Contains("LAYOUT_COLUMNS", keys);
        Assert.Contains("GEOMETRY_LEFT_MARGIN", keys);
        Assert.Contains("GEOMETRY_RIGHT_MARGIN", keys);
        Assert.Contains("GEOMETRY_TOP_MARGIN", keys);
        Assert.Contains("GEOMETRY_BOTTOM_MARGIN", keys);
        Assert.Contains("INCLUDE_FILE_TYPES", keys);
        Assert.Contains("IGNORE_PATTERNS", keys);
        Assert.Contains("LAYOUT_TOC_IN_COLUMNS", keys);
    }

    [Fact]
    public void AllFields_HaveComprehensiveChineseDescriptions() {
        foreach (var field in TuiSchemaCatalog.AllFields) {
            Assert.False(
                string.IsNullOrWhiteSpace(field.Description),
                $"配置项 \"{field.Key}\" 的描述为空，UX-12 要求全量 93 个字段均具备清晰中文解释。"
            );
        }
    }

    [Theory]
    [InlineData("AUTHOR", "AUTHOR")]
    [InlineData("作者", "AUTHOR")]
    [InlineData("font", "CODE_FONT_SIZE")]
    [InlineData("字号", "CODE_FONT_SIZE")]
    [InlineData("边距", "GEOMETRY_LEFT_MARGIN")]
    [InlineData("目录", "LAYOUT_TOC_IN_COLUMNS")]
    public void SearchFields_FindsExpectedFields(string query, string expectedKey) {
        var results = TuiSchemaCatalog.SearchFields(query);
        Assert.NotEmpty(results);
        Assert.Contains(results, f => f.Key == expectedKey);
    }

    [Fact]
    public void SearchFields_WithEmptyQuery_ReturnsEmptyList() {
        Assert.Empty(TuiSchemaCatalog.SearchFields(null));
        Assert.Empty(TuiSchemaCatalog.SearchFields(""));
        Assert.Empty(TuiSchemaCatalog.SearchFields("   "));
    }

    [Fact]
    public void FormatFieldValueDisplay_ClarifiesTocInColumns() {
        var falseDisp = TuiViewRenderer.FormatFieldValueDisplay("false", "LAYOUT_TOC_IN_COLUMNS");
        Assert.Equal("跟随正文 (false)", falseDisp);

        var trueDisp = TuiViewRenderer.FormatFieldValueDisplay("true", "LAYOUT_TOC_IN_COLUMNS");
        Assert.Equal("独立单栏 (true)", trueDisp);

        var normalDisp = TuiViewRenderer.FormatFieldValueDisplay("true", "CODE_LINE_NUMBERS");
        Assert.Equal("true", normalDisp);
    }
}
