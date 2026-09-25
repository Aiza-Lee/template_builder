namespace Core.Localization {
    /// <summary>
    /// 支持的应用程序界面语言。
    /// </summary>
    public enum AppLanguage {
        ZhCn,
        EnUs
    }

    public static class AppLanguageExtensions {
        public static string ToCode(this AppLanguage language) => language switch {
            AppLanguage.EnUs => "en-US",
            _ => "zh-CN"
        };

        public static string GetDisplayName(this AppLanguage language) => language switch {
            AppLanguage.EnUs => "English (US)",
            _ => "简体中文"
        };

        public static AppLanguage Parse(string? code) {
            if (string.IsNullOrWhiteSpace(code)) return AppLanguage.ZhCn;
            var clean = code.Trim().ToLowerInvariant().Replace('_', '-');
            return clean switch {
                "en" or "en-us" or "english" => AppLanguage.EnUs,
                _ => AppLanguage.ZhCn
            };
        }
    }
}
