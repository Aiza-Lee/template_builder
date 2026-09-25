using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Localization {
    /// <summary>
    /// 全局国际化与多语言服务中心。
    /// 集中管理语言翻译提供者与当前界面语言状态，支持无缝切换与未来平滑扩充新语种。
    /// </summary>
    public static class I18n {
        private static readonly object _syncRoot = new();
        private static readonly Dictionary<string, ITranslationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
        private static string _currentLanguageCode = "zh-CN";

        static I18n() {
            ResetToDefaults();
        }

        /// <summary>
        /// 重置为初始默认状态（简体中文与英文）。
        /// </summary>
        public static void ResetToDefaults() {
            lock (_syncRoot) {
                _providers.Clear();
                RegisterProvider(new ZhCnTranslationProvider());
                RegisterProvider(new EnUsTranslationProvider());
                _currentLanguageCode = "zh-CN";
            }
        }

        /// <summary>
        /// 当前激活的界面语言。
        /// </summary>
        public static AppLanguage CurrentLanguage {
            get {
                lock (_syncRoot) return AppLanguageExtensions.Parse(_currentLanguageCode);
            }
            set {
                lock (_syncRoot) _currentLanguageCode = value.ToCode();
            }
        }

        /// <summary>
        /// 当前激活语言的标准代码，如 "zh-CN", "en-US", "ja-JP"。
        /// </summary>
        public static string CurrentLanguageCode {
            get {
                lock (_syncRoot) return _currentLanguageCode;
            }
        }

        /// <summary>
        /// 当前激活语言的人类可读显示名。
        /// </summary>
        public static string CurrentLanguageDisplayName {
            get {
                lock (_syncRoot) {
                    if (_providers.TryGetValue(_currentLanguageCode, out var provider)) {
                        return provider.DisplayName;
                    }
                    return CurrentLanguage.GetDisplayName();
                }
            }
        }

        /// <summary>
        /// 注册自定义语言提供者（支持第三方或未来语种扩展）。
        /// </summary>
        public static void RegisterProvider(ITranslationProvider provider) {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (_syncRoot) {
                _providers[provider.LanguageCode] = provider;
            }
        }

        /// <summary>
        /// 设置当前界面语言（支持 "zh", "zh-CN", "en", "en-US", 以及任意已注册语言代码如 "ja-JP"）。
        /// </summary>
        public static void SetLanguage(string? code) {
            if (string.IsNullOrWhiteSpace(code)) return;
            lock (_syncRoot) {
                var matched = _providers.Keys.FirstOrDefault(k => string.Equals(k, code, StringComparison.OrdinalIgnoreCase));
                if (matched != null) {
                    _currentLanguageCode = matched;
                } else {
                    _currentLanguageCode = AppLanguageExtensions.Parse(code).ToCode();
                }
            }
        }

        /// <summary>
        /// 设置当前界面语言。
        /// </summary>
        public static void SetLanguage(AppLanguage language) {
            lock (_syncRoot) {
                _currentLanguageCode = language.ToCode();
            }
        }

        /// <summary>
        /// 获取当前已注册的所有语言提供者。
        /// </summary>
        public static IReadOnlyList<ITranslationProvider> GetSupportedLanguages() {
            lock (_syncRoot) {
                return _providers.Values.ToList();
            }
        }

        /// <summary>
        /// 翻译指定键的文本。
        /// 若在当前语言中未命中，将降级尝试默认语言，若仍未命中则返回指定的 fallbackText 或键名本身。
        /// </summary>
        public static string T(string key, string? fallbackText = null) {
            if (string.IsNullOrEmpty(key)) return fallbackText ?? string.Empty;

            lock (_syncRoot) {
                if (_providers.TryGetValue(_currentLanguageCode, out var currentProvider)) {
                    var text = currentProvider.TryGetString(key);
                    if (!string.IsNullOrEmpty(text)) {
                        return text;
                    }
                }

                // 降级回退至中文（默认语种）
                if (!string.Equals(_currentLanguageCode, "zh-CN", StringComparison.OrdinalIgnoreCase)
                    && _providers.TryGetValue("zh-CN", out var fallbackProvider)) {
                    var fbText = fallbackProvider.TryGetString(key);
                    if (!string.IsNullOrEmpty(fbText)) {
                        return fbText;
                    }
                }

                return fallbackText ?? key;
            }
        }

        /// <summary>
        /// 翻译带格式化占位符的文本。
        /// </summary>
        public static string TFormat(string key, string fallbackFormat, params object[] args) {
            var template = T(key, fallbackFormat);
            try {
                return string.Format(template, args);
            } catch {
                return template;
            }
        }
    }
}
