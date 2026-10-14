using Core;
using Core.Localization;
using Xunit;

namespace template_builder.Tests;

public class AppVersionTests {
    [Fact]
    public void DisplayVersion_MatchesExpectedPattern() {
        var version = AppVersion.DisplayVersion;
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.StartsWith("2.0.0", version);
        Assert.DoesNotContain("+", version); // DisplayVersion 不应包含 build metadata / commit hash
    }

    [Fact]
    public void FullVersion_ContainsDisplayVersion() {
        var full = AppVersion.FullVersion;
        var display = AppVersion.DisplayVersion;
        Assert.Contains(display, full);
    }

    [Fact]
    public void IsPrerelease_AccuratelyReflectsHyphenPresence() {
        var isPrerelease = AppVersion.IsPrerelease;
        var display = AppVersion.DisplayVersion;
        Assert.Equal(display.Contains('-'), isPrerelease);
    }

    [Fact]
    public void BannerSubtitles_IncludeDisplayVersion() {
        var zh = new ZhCnTranslationProvider();
        var en = new EnUsTranslationProvider();

        var zhSubtitle = zh.TryGetString("Banner.Subtitle");
        var enSubtitle = en.TryGetString("Banner.Subtitle");

        Assert.NotNull(zhSubtitle);
        Assert.NotNull(enSubtitle);
        Assert.Contains(AppVersion.DisplayVersion, zhSubtitle);
        Assert.Contains(AppVersion.DisplayVersion, enSubtitle);
    }
}
