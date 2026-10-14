using System.Globalization;
using System.Text.Json;
using Utils.Exceptions;

namespace Utils {
    internal class ConfigValue(JsonElement defaultValue) {
        private JsonElement? _value;
        private JsonElement DefaultValue { get; } = defaultValue;
        public JsonElement Value {
            get => _value ?? DefaultValue;
            set => _value = value;
        }

        public string GetAsString() => Value.ValueKind == JsonValueKind.String ? Value.GetString() ?? string.Empty : Value.GetRawText();
        public string[] GetAsStringArray() {
            if (Value.ValueKind == JsonValueKind.Array) {
                return Value.EnumerateArray().Select(
                    ele => ele.ValueKind == JsonValueKind.String ? ele.GetString() ?? string.Empty : ele.GetRawText()
                ).ToArray();
            }
            throw new InvalidCastException($"无法将配置值 '{GetAsString()}' 转换为 string[]。");
        }
        public int GetAsInt() {
            if (Value.ValueKind == JsonValueKind.Number && Value.TryGetInt32(out int intValue)) { return intValue; }
            if (Value.ValueKind == JsonValueKind.String && int.TryParse(Value.GetString(), out intValue)) { return intValue; }
            throw new InvalidCastException($"无法将配置值 '{GetAsString()}' 转换为 int。");
        }
        public bool GetAsBool() {
            if (Value.ValueKind == JsonValueKind.True) { return true; }
            if (Value.ValueKind == JsonValueKind.False) { return false; }
            if (Value.ValueKind == JsonValueKind.String && bool.TryParse(Value.GetString(), out bool boolValue)) { return boolValue; }
            throw new InvalidCastException($"无法将配置值 '{GetAsString()}' 转换为 bool。");
        }
        /// <summary>
        /// 容错版 bool 读取：JSON true/false 字面量 → 直接返回；字符串 "true"/"false"/"1"/"0" → 解析；
        /// 其余（含缺失键、未注册键、非可解析字符串）一律返回 <paramref name="fallback"/>。
        /// 与无参 <see cref="GetAsBool()"/> 的区别：无参版本在无法转换时抛 InvalidCastException，
        /// 此版本用于「默认值兜底」场景（如全局开关类配置项）。
        /// </summary>
        public bool GetAsBool(bool fallback) {
            if (Value.ValueKind == JsonValueKind.True) { return true; }
            if (Value.ValueKind == JsonValueKind.False) { return false; }
            if (Value.ValueKind == JsonValueKind.String) {
                var s = Value.GetString();
                if (bool.TryParse(s, out bool boolValue)) { return boolValue; }
                if (s == "1") { return true; }
                if (s == "0") { return false; }
            }
            return fallback;
        }
    }

    internal class ReadonlyConfigValue(ConfigValue configValue) {
        private ConfigValue ConfigValue { get; } = configValue;
        public string GetAsString() => ConfigValue.GetAsString();
        public string[] GetAsStringArray() => ConfigValue.GetAsStringArray();
        public int GetAsInt() => ConfigValue.GetAsInt();
        public bool GetAsBool() => ConfigValue.GetAsBool();
        public bool GetAsBool(bool fallback) => ConfigValue.GetAsBool(fallback);
    }

    /// <summary>
    /// 配置文件解析器对未知 key 的处理策略。
    /// </summary>
    internal enum ConfigStrictness {
        /// <summary>未知 key 仅以 Warning 记录并跳过。</summary>
        Lax,
        /// <summary>未知 key 直接抛 <see cref="UnknownConfigKeyException"/>。</summary>
        Strict,
    }

    /// <summary>
    /// 严格模式下，用户配置中出现 DefaultConfig.json 未注册的 key 时抛出。
    /// </summary>
    internal class UnknownConfigKeyException : Exception {
        public UnknownConfigKeyException(string message) : base(message) { }
    }

    /// <summary>
    /// 负责解析配置文件的类
    /// <para>
    /// 只有在嵌入式资源的中的默认配置文件中出现过的配置项才能被解析 <br/>
    /// 最终每个配置项的键都是，顶层的json类名称依次展开到当前配置项的类名称，期间所有类名称的大写，并以'_'连接。
    /// </para>
    /// </summary>
    internal class ConfigParser : IConfigParser {
        private readonly Dictionary<string, ConfigValue> _configValues = [];
        private readonly ILogger? _logger;
        private readonly string _rootObjectName;
        private readonly ConfigStrictness _strictness;

        const string DefaultConfigFileName = "DefaultConfig.jsonc";

        // JSONC (含 // 注释) + 允许尾随逗号
        static readonly JsonDocumentOptions _jsonDocumentOptions = new() {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly Lazy<JsonDocument> s_defaultConfigDocument = new(() => {
            var raw = new ManifestResourceManager().GetResourceInString(DefaultConfigFileName);
            return JsonDocument.Parse(raw, _jsonDocumentOptions);
        });

        /// <summary>
        /// 嵌入式默认配置的共享 JsonDocument 单例
        /// </summary>
        public static JsonDocument DefaultConfigDocument => s_defaultConfigDocument.Value;

        /// <summary>
        /// 构造函数，使用共享的嵌入式默认配置
        /// </summary>
        /// <param name="rootObjectName">配置文件的根对象名称</param>
        /// <param name="logger">日志记录器，可选</param>
        /// <param name="strictness">对未知 key 的处理策略，默认为 Lax</param>
        public ConfigParser(string rootObjectName, ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax)
            : this(rootObjectName, logger, DefaultConfigDocument, strictness) {
        }

        /// <summary>
        /// 构造函数，支持传入指定的默认配置 JsonDocument
        /// </summary>
        /// <param name="rootObjectName">配置文件的根对象名称</param>
        /// <param name="logger">日志记录器，可选</param>
        /// <param name="defaultDocument">默认配置 JsonDocument</param>
        /// <param name="strictness">对未知 key 的处理策略，默认为 Lax</param>
        public ConfigParser(string rootObjectName, ILogger? logger, JsonDocument defaultDocument, ConfigStrictness strictness = ConfigStrictness.Lax) {
            ArgumentNullException.ThrowIfNull(defaultDocument);
            _logger = logger;
            _rootObjectName = rootObjectName;
            _strictness = strictness;

            ParseDefaultDocument(defaultDocument);
        }

        private void ParseDefaultDocument(JsonDocument defaultDocument) {
            var json = defaultDocument.RootElement;
            if (json.TryGetProperty(_rootObjectName, out var jsonElement)) {
                ParseJsonElement_R(jsonElement, [], isDefaultConfig: true);
            } else {
                _logger?.Error($"Config content does not contain '{_rootObjectName}' root element.");
            }
        }

        public ReadonlyConfigValue this[string key] {
            get {
                if (_configValues.TryGetValue(key, out var configValue)) {
                    return new ReadonlyConfigValue(configValue);
                } else {
                    _logger?.Error($"Key '{key}' is not registered. Returning empty ReadonlyConfigValue.");
                    return new ReadonlyConfigValue(new ConfigValue(JsonDocument.Parse("\"\"").RootElement));
                }
            }
        }

        /// <summary>
        /// 尝试获取指定配置项。若配置项未注册，返回 false，且不记录 Error 日志。
        /// </summary>
        public bool TryGetConfigValue(string key, out ReadonlyConfigValue value) {
            if (_configValues.TryGetValue(key, out var configValue)) {
                value = new ReadonlyConfigValue(configValue);
                return true;
            }
            value = default!;
            return false;
        }

        /// <summary>
        /// 判断指定配置项是否已注册。
        /// </summary>
        public bool ContainsKey(string key) => _configValues.ContainsKey(key);

        /// <summary>
        /// 获取所有配置项，键值对都为字符串类型
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, string>> GetAllConfigsAsString() {
            foreach (var kvp in _configValues) {
                yield return new(kvp.Key, kvp.Value.GetAsString());
            }
        }

        /// <summary>
        /// 以字符串形式覆盖一个<b>已注册</b>配置项的值，并按该键在默认配置中的 JSON 类型自动强制转换
        /// （字符串 / 数值 / 布尔）。供需要在运行时注入配置值的上层使用（如 TUI 交互模式的快速配置项）。
        /// <para>
        /// 与 <see cref="ParseConfigFile"/> 的区别：此方法不解析文件，也不会因未知键而走
        /// Strict/Lax 分支——键未注册时直接返回 false，由调用方决定如何提示。
        /// </para>
        /// </summary>
        /// <param name="key">配置项键（相对根对象，大写、下划线分隔，如 <c>GLOBAL_MAIN_FONT</c>）</param>
        /// <param name="rawValue">字符串形式的原始值</param>
        /// <returns>键已注册且转换成功返回 true；键未注册、值非法或目标为复合类型（数组/对象）返回 false</returns>
        public bool TrySetConfigValueFromString(string key, string rawValue) {
            if (!_configValues.TryGetValue(key, out var existing)) return false;
            if (!TryCoerceValue(rawValue, existing.Value.ValueKind, out var element)) return false;
            existing.Value = element;
            _logger?.Debug($"Overriding config key: '{key}' with value: '{rawValue}'");
            return true;
        }

        /// <summary>
        /// 把字符串按目标 JSON 类型强制转换。支持标量（字符串 / 数值 / 布尔）以及字符串数组（逗号分隔或 JSON 数组语法）。
        /// </summary>
        private static bool TryCoerceValue(string raw, JsonValueKind targetKind, out JsonElement element) {
            element = default;
            try {
                switch (targetKind) {
                    case JsonValueKind.Number:
                        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return false;
                        element = JsonDocument.Parse(number.ToString(CultureInfo.InvariantCulture)).RootElement.Clone();
                        return true;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        if (!bool.TryParse(raw, out var flag)) return false;
                        element = JsonDocument.Parse(flag ? "true" : "false").RootElement.Clone();
                        return true;
                    case JsonValueKind.String:
                        element = JsonDocument.Parse(JsonSerializer.Serialize(raw)).RootElement.Clone();
                        return true;
                    case JsonValueKind.Array:
                        var trimmed = raw.Trim();
                        if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) {
                            var doc = JsonDocument.Parse(trimmed);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array) {
                                element = doc.RootElement.Clone();
                                return true;
                            }
                        }
                        var items = string.IsNullOrWhiteSpace(trimmed)
                            ? Array.Empty<string>()
                            : trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        element = JsonSerializer.SerializeToElement(items);
                        return true;
                    default:
                        return false;
                }
            } catch (JsonException) {
                return false;
            }
        }

        /// <summary>
        /// 传入字符串格式的json文件内容
        /// </summary>
        /// <param name="content">字符串形式的json文件内容</param>
        /// <param name="sourcePath">可选，源文件路径，用于在 <see cref="MalformedConfigException"/> 中提供上下文</param>
        public void ParseConfigFile(string content, string? sourcePath = null) {
            if (string.IsNullOrWhiteSpace(content)) {
                _logger?.Error($"Config file content is empty.");
                return;
            }
            ParseJsonContent(content, false, sourcePath);
        }

        private void ParseJsonContent(string jsonContent, bool isDefaultConfig, string? sourcePath = null) {
            try {
                var json = JsonDocument.Parse(jsonContent, _jsonDocumentOptions).RootElement;
                if (json.TryGetProperty(_rootObjectName, out var jsonElement)) {
                    ParseJsonElement_R(jsonElement, [], isDefaultConfig);
                } else {
                    if (isDefaultConfig) {
                        throw new InvalidOperationException($"默认配置文件必须包含 '{_rootObjectName}' 根节点。");
                    }
                    _logger?.Debug($"配置文件未包含 '{_rootObjectName}' 根节点，将沿用默认配置。");
                }
            } catch (JsonException ex) {
                throw new MalformedConfigException(
                    $"解析 JSON 内容失败：{ex.Message}",
                    sourcePath,
                    ex
                );
            } catch (UnknownConfigKeyException) {
                throw;
            } catch (MalformedConfigException) {
                throw;
            } catch (Exception ex) {
                throw new MalformedConfigException(
                    $"解析配置内容时发生未预期错误：{ex.Message}",
                    sourcePath,
                    ex
                );
            }
        }

        /// <summary>
        /// 解析JsonElement,并记录路径以对配置项命名
        /// </summary>
        /// <param name="element"></param>
        /// <param name="path"></param>
        /// <param name="isDefaultConfig"></param>
        private void ParseJsonElement_R(JsonElement element, List<string> path, bool isDefaultConfig) {
            if (element.ValueKind == JsonValueKind.Object) {
                foreach (var property in element.EnumerateObject()) {
                    path.Add(property.Name.ToUpper());
                    ParseJsonElement_R(property.Value, path, isDefaultConfig);
                    path.RemoveAt(path.Count - 1);
                }
            } else {
                string key = string.Join('_', path);
                var configValue = new ConfigValue(element);
                if (isDefaultConfig) {
                    RegisterConfig(key, configValue);
                } else {
                    SetConfigValue(key, configValue);
                }
            }
        }

        /// <summary>
        /// 注册配置项，只有注册了的配置项才会被解析
        /// </summary>
        /// <param name="key">配置项键</param>
        /// <param name="defaultValue">配置项默认值</param>
        private void RegisterConfig(string key, ConfigValue configValue) {
            _logger?.Debug($"Registering config key: '{key}' with default value: '{configValue.GetAsString()}'");
            if (!_configValues.ContainsKey(key)) {
                _configValues[key] = configValue;
            } else {
                _logger?.Warning($"Key '{key}' is already registered. Skipping.");
            }
        }

        /// <summary>
        /// 对于实际的配置，设置配置项的值
        /// </summary>
        /// <param name="key">配置项键</param>
        /// <param name="configValue">配置项值</param>
        private void SetConfigValue(string key, ConfigValue configValue) {
            _logger?.Debug($"Setting config key: '{key}' with value: '{configValue.GetAsString()}'");
            if (_configValues.TryGetValue(key, out var existingConfigValue)) {
                existingConfigValue.Value = configValue.Value;
            } else {
                if (_strictness == ConfigStrictness.Strict) {
                    throw new UnknownConfigKeyException(
                        $"配置项 \"{key}\" 未在默认配置中注册，请检查是否拼写错误或删除该配置项。"
                    );
                }
                _logger?.Warning($"[未知配置项] \"{key}\" 不在默认配置中，已跳过。如需自定义此行为，请运行 validate 子命令或检查配置文件。");
            }
        }
    }
}