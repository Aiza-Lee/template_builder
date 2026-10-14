using Core.Models;
using Core.Tui;
using Core.Validation;
using Xunit;

namespace template_builder.Tests.Validation;

public class ConfigValidatorTests {
    [Fact]
    public void Validate_DefaultConfig_IsValid() {
        var config = new ProjectConfiguration();
        var result = ConfigValidator.Validate(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidInteger_TabSizeOops_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "code": {
                    "tab_size": "oops"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "CODE_TAB_SIZE");
        Assert.Equal("TEX.code.tab_size", err.PropertyPath);
        Assert.Equal("oops", err.CurrentValue);
        Assert.Contains("整数", err.Expected);
        Assert.Contains("修改", err.Recommendation);
    }

    [Fact]
    public void Validate_NegativeInteger_WhenMinimumIsZero_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "code": {
                    "tab_size": -1
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "CODE_TAB_SIZE");
        Assert.Contains("小于允许的最小值", err.Message);
        Assert.Equal("-1", err.CurrentValue);
    }

    [Fact]
    public void Validate_IntegerExceedingMaximum_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "hyperref": {
                    "bookmarks_depth": 10
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "HYPERREF_BOOKMARKS_DEPTH");
        Assert.Contains("大于允许的最大值", err.Message);
        Assert.Equal("10", err.CurrentValue);
    }

    [Fact]
    public void Validate_InvalidBoolean_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "code": {
                    "break_lines": "yes_please"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "CODE_BREAK_LINES");
        Assert.Contains("不是合法的布尔值", err.Message);
    }

    [Fact]
    public void Validate_InvalidEnum_ColumnsThree_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "layout": {
                    "columns": 3
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "LAYOUT_COLUMNS");
        Assert.Contains("不在允许的枚举值列表中", err.Message);
        Assert.Contains("1, 2", err.Expected);
    }

    [Fact]
    public void Validate_InvalidEnum_TitleAlignment_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "title": {
                    "alignment": "justify_all"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "TITLE_ALIGNMENT");
        Assert.Contains("不在允许的枚举值列表中", err.Message);
    }

    [Fact]
    public void Validate_InvalidPattern_FirstNumber_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "code": {
                    "firstnumber": "not_auto_or_digits"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "CODE_FIRSTNUMBER");
        Assert.Contains("不符合格式规则", err.Message);
    }

    [Theory]
    [InlineData("invalid_dim")]
    [InlineData("100")]
    [InlineData("10foo")]
    public void Validate_InvalidDimension_LeftMargin_ReturnsError(string badMargin) {
        var config = new ProjectConfiguration();
        var json = $$"""
        {
            "TEX": {
                "geometry": {
                    "left_margin": "{{badMargin}}"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "GEOMETRY_LEFT_MARGIN");
        Assert.Contains("不是合法的 LaTeX 尺寸表达式", err.Message);
    }

    [Theory]
    [InlineData("10pt")]
    [InlineData("1.5cm")]
    [InlineData("20mm")]
    [InlineData("0.75in")]
    [InlineData("0")]
    [InlineData("0pt")]
    public void Validate_ValidDimension_LeftMargin_Passes(string validMargin) {
        var config = new ProjectConfiguration();
        var json = $$"""
        {
            "TEX": {
                "geometry": {
                    "left_margin": "{{validMargin}}"
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyIncludeFileTypes_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "include_file_types": []
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "INCLUDE_FILE_TYPES");
        Assert.Contains("不能为空", err.Message);
    }

    [Fact]
    public void Validate_IncludeFileTypeWithoutDotPrefix_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "include_file_types": ["cs", "cpp"]
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Key == "INCLUDE_FILE_TYPES" && e.CurrentValue == "cs");
    }

    [Fact]
    public void GetNumberConstraint_ReturnsExpectedMinAndMax() {
        var tabSize = ConfigValidator.GetNumberConstraint("CODE_TAB_SIZE");
        Assert.Equal(0, tabSize.Min);
        Assert.Null(tabSize.Max);

        var sectionDepth = ConfigValidator.GetNumberConstraint("LAYOUT_SECTION_DEPTH");
        Assert.Equal(1, sectionDepth.Min);
        Assert.Equal(5, sectionDepth.Max);

        var nonExistent = ConfigValidator.GetNumberConstraint("NON_EXISTENT");
        Assert.Null(nonExistent.Min);
        Assert.Null(nonExistent.Max);
    }

    [Fact]
    public void Validate_NumberForString_Author_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "author": 123
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "AUTHOR");
        Assert.Contains("类型必须为字符串", err.Message);
    }

    [Fact]
    public void Validate_NumberForBoolean_EnableCover_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "TEX": {
                "title": {
                    "enable_cover": 1
                }
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "TITLE_ENABLE_COVER");
        Assert.Contains("不是合法的布尔值", err.Message);
    }

    [Fact]
    public void Validate_NumberForArray_IgnorePatterns_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "ignore_patterns": 42
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "IGNORE_PATTERNS");
        Assert.Contains("类型必须为数组", err.Message);
    }

    [Fact]
    public void Validate_NonStringInArray_IgnorePatterns_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "ignore_patterns": ["*.bak", 42]
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "IGNORE_PATTERNS");
        Assert.Contains("数组元素必须为字符串", err.Message);
    }

    [Fact]
    public void Validate_EmptyPatternInIgnorePatterns_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "ignore_patterns": ["  "]
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "IGNORE_PATTERNS");
        Assert.Contains("包含空通配符条目", err.Message);
    }

    [Fact]
    public void Validate_InvalidSectionOrderOverride_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "section_order_overrides": ["invalid_format"]
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "SECTION_ORDER_OVERRIDES");
        Assert.Contains("格式非法", err.Message);
    }

    [Fact]
    public void Validate_InvalidSectionTitleOverride_ReturnsError() {
        var config = new ProjectConfiguration();
        var json = """
        {
            "PROGRAM": {
                "section_title_overrides": ["invalid_format"]
            }
        }
        """;
        config.LoadProjectConfig(json);

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        var err = Assert.Single(result.Errors, e => e.Key == "SECTION_TITLE_OVERRIDES");
        Assert.Contains("格式非法", err.Message);
    }
}
