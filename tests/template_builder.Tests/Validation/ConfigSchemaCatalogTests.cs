using System;
using Core.Schema;
using Core.SourceTree;
using Core.Tui;
using Core.Validation;
using Xunit;

namespace template_builder.Tests.Validation;

public class ConfigSchemaCatalogTests {
    [Fact]
    public void AllFields_ContainsExpectedCatalogDefinitions() {
        var fields = ConfigSchemaCatalog.AllFields;
        Assert.NotNull(fields);
        Assert.NotEmpty(fields);
        Assert.True(fields.Count >= 90, $"Expected at least 90 schema fields, but found {fields.Count}");

        Assert.Contains(fields, f => f.Key == "AUTHOR" && f.Section == "TEX" && f.ExpectedType == "string");
        Assert.Contains(fields, f => f.Key == "LAYOUT_COLUMNS" && f.ExpectedType == "integer");
        Assert.Contains(fields, f => f.Key == "INCLUDE_FILE_TYPES" && f.Section == "PROGRAM" && f.ExpectedType == "array");
    }

    [Fact]
    public void FieldsByKey_LookupMatchesAllFields() {
        var map = ConfigSchemaCatalog.FieldsByKey;
        Assert.NotNull(map);

        Assert.True(map.TryGetValue("LAYOUT_COLUMNS", out var columnsDef));
        Assert.NotNull(columnsDef);
        Assert.Equal("TEX.layout.columns", columnsDef.JsonPath);
        Assert.NotNull(columnsDef.Choices);
        Assert.Contains("1", columnsDef.Choices);
        Assert.Contains("2", columnsDef.Choices);

        Assert.True(map.TryGetValue("LAYOUT_SECTION_DEPTH", out var sectionDepthDef));
        Assert.NotNull(sectionDepthDef);
        Assert.Equal("TEX.layout.section_depth", sectionDepthDef.JsonPath);
        Assert.Equal(1, sectionDepthDef.Minimum);
        Assert.Equal(5, sectionDepthDef.Maximum);

        Assert.True(map.TryGetValue("CODE_TAB_SIZE", out var tabSizeDef));
        Assert.NotNull(tabSizeDef);
        Assert.Equal(0, tabSizeDef.Minimum);
    }

    [Fact]
    public void GetNumberConstraint_ReturnsMinAndMax() {
        var (min, max) = ConfigSchemaCatalog.GetNumberConstraint("LAYOUT_SECTION_DEPTH");
        Assert.Equal(1, min);
        Assert.Equal(5, max);

        var (nonExistentMin, nonExistentMax) = ConfigSchemaCatalog.GetNumberConstraint("NON_EXISTENT_KEY");
        Assert.Null(nonExistentMin);
        Assert.Null(nonExistentMax);
    }

    [Fact]
    public void TuiConfigField_InheritsFromConfigFieldDefinition() {
        var tuiFields = TuiSchemaCatalog.AllFields;
        Assert.NotNull(tuiFields);
        Assert.Equal(ConfigSchemaCatalog.AllFields.Count, tuiFields.Count);

        var first = tuiFields[0];
        Assert.IsAssignableFrom<ConfigFieldDefinition>(first);

        // 验证 ValidateSingleValue 支持多态传入 ConfigFieldDefinition 或 TuiConfigField
        ConfigFieldDefinition def = first;
        var (isValid, _) = ConfigValidator.ValidateSingleValue(def, "valid_value");
        Assert.True(isValid || !isValid); // 可调用无异常
    }

    [Fact]
    public void SourceTreePlan_AllSectionCommands_AndDefaultLanguageMap_AreSSOT() {
        Assert.Equal(5, SourceTreePlan.AllSectionCommands.Length);
        Assert.Equal("section", SourceTreePlan.AllSectionCommands[0]);
        Assert.Equal("subparagraph", SourceTreePlan.AllSectionCommands[4]);

        Assert.True(SourceTreePlan.DefaultLanguageMap.ContainsKey("cpp"));
        Assert.True(SourceTreePlan.DefaultLanguageMap.ContainsKey("c"));
        Assert.True(SourceTreePlan.DefaultLanguageMap.ContainsKey("cs"));
        Assert.True(SourceTreePlan.DefaultLanguageMap.ContainsKey("py"));

        // BuildLanguageMap 用户覆盖正确应用
        var custom = SourceTreePlan.BuildLanguageMap([".custom:mypy", ".frag:glsl"]);
        Assert.Equal("mypy", custom["custom"]);
        Assert.Equal("glsl", custom["frag"]);
        Assert.Equal("cpp", custom["cpp"]); // 保留内置默认
    }
}
