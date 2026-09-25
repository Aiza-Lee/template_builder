using System;
using System.Collections.Generic;
using Core.Commands;
using Core.Localization;
using Utils;
using Xunit;

namespace template_builder.Tests.Localization;

public class I18nTests : IDisposable {
    public I18nTests() {
        I18n.ResetToDefaults();
    }

    public void Dispose() {
        I18n.ResetToDefaults();
    }

    [Fact]
    public void CurrentLanguage_DefaultIsZhCn() {
        Assert.Equal(AppLanguage.ZhCn, I18n.CurrentLanguage);
        Assert.Equal("zh-CN", I18n.CurrentLanguageCode);
        Assert.Equal("简体中文", I18n.CurrentLanguageDisplayName);
    }

    [Theory]
    [InlineData("en", AppLanguage.EnUs, "en-US", "English (US)")]
    [InlineData("en-US", AppLanguage.EnUs, "en-US", "English (US)")]
    [InlineData("EN_us", AppLanguage.EnUs, "en-US", "English (US)")]
    [InlineData("english", AppLanguage.EnUs, "en-US", "English (US)")]
    [InlineData("zh", AppLanguage.ZhCn, "zh-CN", "简体中文")]
    [InlineData("zh-CN", AppLanguage.ZhCn, "zh-CN", "简体中文")]
    public void SetLanguage_ByCodeString_SwitchesLanguageAccurately(
        string inputCode,
        AppLanguage expectedLang,
        string expectedCode,
        string expectedDisplayName
    ) {
        I18n.SetLanguage(inputCode);
        Assert.Equal(expectedLang, I18n.CurrentLanguage);
        Assert.Equal(expectedCode, I18n.CurrentLanguageCode);
        Assert.Equal(expectedDisplayName, I18n.CurrentLanguageDisplayName);
    }

    [Fact]
    public void T_ReturnsCorrectTranslation_BetweenChineseAndEnglish() {
        // 中文模式
        I18n.SetLanguage(AppLanguage.ZhCn);
        Assert.Equal("返回上级", I18n.T("Menu.Back"));
        Assert.Equal("确认配置并开始构建", I18n.T("Menu.ConfirmBuild"));
        Assert.Contains("清空", I18n.T("Controls.Full"));

        // 切换为英文模式
        I18n.SetLanguage(AppLanguage.EnUs);
        Assert.Equal("Back", I18n.T("Menu.Back"));
        Assert.Equal("Confirm Configuration & Start Build", I18n.T("Menu.ConfirmBuild"));
        Assert.Contains("Clear", I18n.T("Controls.Full"));
    }

    [Fact]
    public void T_MissingKeyInActiveLanguage_FallsBackToDefaultOrFallbackText() {
        I18n.SetLanguage(AppLanguage.EnUs);

        // 未知键返回指定 fallback 或键自身
        Assert.Equal("默认回退文字", I18n.T("NON_EXISTENT_KEY", "默认回退文字"));
        Assert.Equal("NON_EXISTENT_KEY", I18n.T("NON_EXISTENT_KEY"));
    }

    [Fact]
    public void RegisterProvider_CustomLanguage_ExtendsLanguageSupportCleanly() {
        var mockJa = new MockJapaneseProvider();
        I18n.RegisterProvider(mockJa);

        var supported = I18n.GetSupportedLanguages();
        Assert.Contains(supported, p => p.LanguageCode == "ja-JP");

        I18n.SetLanguage("ja-JP");
        Assert.Equal("戻る", I18n.T("Menu.Back"));
    }

    [Fact]
    public void CommandLine_LangOption_ParsesAndConfiguresLanguage() {
        var logger = new TestLogger();
        var factory = new RootCommandFactory(logger);
        var root = factory.CreateRootCommand();

        // 验证命令行 --lang en-US 成功解析
        var parseResult = root.Parse(["build", "--lang", "en-US", "--tui-scaffold", "/tmp/non_existent_folder_test"]);
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void TranslationProviders_HaveIdenticalKeySetsAndNonEmptyValues() {
        var zh = new ZhCnTranslationProvider();
        var en = new EnUsTranslationProvider();

        var zhStrings = zh.GetAllStrings();
        var enStrings = en.GetAllStrings();

        Assert.NotEmpty(zhStrings);
        Assert.NotEmpty(enStrings);

        // 验证两个语种词典键完全对称，无任何遗漏
        var missingInEn = zhStrings.Keys.Except(enStrings.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var missingInZh = enStrings.Keys.Except(zhStrings.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Empty(missingInEn);
        Assert.Empty(missingInZh);
        Assert.Equal(zhStrings.Count, enStrings.Count);

        // 验证无空白翻译条目
        foreach (var (k, v) in zhStrings) {
            Assert.False(string.IsNullOrWhiteSpace(v), $"zh-CN 中键 '{k}' 取值为空白");
        }
        foreach (var (k, v) in enStrings) {
            Assert.False(string.IsNullOrWhiteSpace(v), $"en-US 中键 '{k}' 取值为空白");
        }
    }

    [Fact]
    public void T_KeyCoverage_ResolvesAccuratelyAcrossMajorCategories() {
        // 中文断言
        I18n.SetLanguage(AppLanguage.ZhCn);
        Assert.Equal("基础路径与运行参数", I18n.T("Breadcrumb.Step1"));
        Assert.Equal("当前已配置的运行时覆盖项", I18n.T("Overrides.Title"));
        Assert.Equal("✔ 构建成功", I18n.T("Dashboard.Success"));
        Assert.Equal("源目录路径", I18n.T("SourceTree.SourceDir"));
        Assert.Equal("源文件目录", I18n.T("Prompt.SourceDir"));
        Assert.Equal("常用核心配置", I18n.T("Category.featured.Title"));
        Assert.Equal("比赛打印 (Competition Print)", I18n.T("Preset.competition.Name"));
        Assert.Equal("完整章节树", I18n.T("SourceTree.ModeAll"));
        Assert.Equal("未知", I18n.T("Build.Unknown"));

        // 英文断言
        I18n.SetLanguage(AppLanguage.EnUs);
        Assert.Equal("Basic Paths & Parameters", I18n.T("Breadcrumb.Step1"));
        Assert.Equal("Currently Configured Runtime Overrides", I18n.T("Overrides.Title"));
        Assert.Equal("✔ Build Succeeded", I18n.T("Dashboard.Success"));
        Assert.Equal("Source Directory: ", I18n.T("SourceTree.SourceDir"));
        Assert.Equal("Source Directory", I18n.T("Prompt.SourceDir"));
        Assert.Equal("Core Configuration", I18n.T("Category.featured.Title"));
        Assert.Equal("Competition Print", I18n.T("Preset.competition.Name"));
        Assert.Equal("Full Chapter Tree", I18n.T("SourceTree.ModeAll"));
        Assert.Equal("Unknown", I18n.T("Build.Unknown"));
    }

    [Fact]
    public void CategoryGroup_DisplayTitleAndDescription_AreLocalized() {
        var group = new Core.Tui.TuiCategoryGroup("featured", "常用核心配置", "高频排版调整项", []);

        I18n.SetLanguage(AppLanguage.ZhCn);
        Assert.Equal("常用核心配置", group.DisplayTitle);

        I18n.SetLanguage(AppLanguage.EnUs);
        Assert.Equal("Core Configuration", group.DisplayTitle);
    }

    [Fact]
    public void LayoutPreset_DisplayProperties_AreLocalized() {
        var preset = Core.Models.LayoutPresets.Competition;

        I18n.SetLanguage(AppLanguage.ZhCn);
        Assert.Contains("比赛打印", preset.DisplayName);
        Assert.Contains("面向 ACM/ICPC", preset.DisplayDescription);

        I18n.SetLanguage(AppLanguage.EnUs);
        Assert.Equal("Competition Print", preset.DisplayName);
        Assert.Contains("Optimized for ACM/ICPC", preset.DisplayDescription);
    }

    [Fact]
    public void TuiMenuItem_Sentinels_TitleIsDynamicWithLanguage() {
        I18n.SetLanguage(AppLanguage.ZhCn);
        Assert.Equal("返回上级", Core.Tui.TuiMenuItem.BackSentinel.Title);
        Assert.Equal("确认配置并开始构建", Core.Tui.TuiMenuItem.ConfirmSentinel.Title);
        Assert.Equal("清空当前项", Core.Tui.TuiMenuItem.DeleteSentinel.Title);

        I18n.SetLanguage(AppLanguage.EnUs);
        Assert.Equal("Back", Core.Tui.TuiMenuItem.BackSentinel.Title);
        Assert.Equal("Confirm Configuration & Start Build", Core.Tui.TuiMenuItem.ConfirmSentinel.Title);
        Assert.Equal("Clear Current Item", Core.Tui.TuiMenuItem.DeleteSentinel.Title);
    }

    private sealed class MockJapaneseProvider : ITranslationProvider {
        public AppLanguage Language => AppLanguage.ZhCn; // 复用基础枚举或扩展
        public string LanguageCode => "ja-JP";
        public string DisplayName => "日本語";

        private readonly Dictionary<string, string> _dict = new() {
            ["Menu.Back"] = "戻る",
            ["Menu.ConfirmBuild"] = "ビルド開始"
        };

        public string? TryGetString(string key) => _dict.TryGetValue(key, out var v) ? v : null;
        public IReadOnlyDictionary<string, string> GetAllStrings() => _dict;
    }
}
