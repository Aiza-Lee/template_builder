using System;
using Core.Tui;
using Xunit;

namespace template_builder.Tests.Tui;

public class TuiThemeTests {
    [Fact]
    public void ThemeColors_AreValidHexStrings() {
        var colors = new[] {
            TuiTheme.Primary,
            TuiTheme.PrimaryDim,
            TuiTheme.Secondary,
            TuiTheme.Accent,
            TuiTheme.Success,
            TuiTheme.Warning,
            TuiTheme.Danger,
            TuiTheme.Muted,
            TuiTheme.Border,
            TuiTheme.Surface,
            TuiTheme.TextLight,
            TuiTheme.TextDim
        };

        foreach (var c in colors) {
            Assert.StartsWith("#", c);
            Assert.Equal(7, c.Length);
            Assert.True(int.TryParse(c.Substring(1), System.Globalization.NumberStyles.HexNumber, null, out _));
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void GetSpectrumColor_ProducesSmoothLowSaturationValues(double t) {
        var (r, g, b) = TuiTheme.GetSpectrumColor(t);
        Assert.InRange(r, 0, 255);
        Assert.InRange(g, 0, 255);
        Assert.InRange(b, 0, 255);

        // 验证低饱和度特征：RGB 极值差不会像霓虹色 (255, 0, 85) 那样达到 255
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        Assert.True(max - min <= 160, $"RGB ({r},{g},{b}) 饱和度过高，差值为 {max - min}");
    }
}
