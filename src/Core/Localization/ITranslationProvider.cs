using System.Collections.Generic;

namespace Core.Localization {
    /// <summary>
    /// 语言翻译字典提供者接口。
    /// 支持日后平滑扩展新语言（如日文、德文等），无需修改业务代码。
    /// </summary>
    public interface ITranslationProvider {
        AppLanguage Language { get; }
        string LanguageCode { get; }
        string DisplayName { get; }
        string? TryGetString(string key);
        IReadOnlyDictionary<string, string> GetAllStrings();
    }
}
