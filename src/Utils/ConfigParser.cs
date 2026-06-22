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
			return [GetAsString()];
		}
		public int GetAsInt() {
			if (Value.ValueKind == JsonValueKind.Number && Value.TryGetInt32(out int intValue)) { return intValue; }
			if (Value.ValueKind == JsonValueKind.String && int.TryParse(Value.GetString(), out intValue)) { return intValue; }
			throw new InvalidCastException($"Cannot convert config value '{GetAsString()}' to int.");
		}
		public bool GetAsBool() {
			if (Value.ValueKind == JsonValueKind.True) { return true; }
			if (Value.ValueKind == JsonValueKind.False) { return false; }
			if (Value.ValueKind == JsonValueKind.String && bool.TryParse(Value.GetString(), out bool boolValue)) { return boolValue; }
			throw new InvalidCastException($"Cannot convert config value '{GetAsString()}' to bool.");
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
		private readonly ILogger _logger;
		private readonly string _rootObjectName;
		private readonly ConfigStrictness _strictness;

		const string DefaultConfigFileName = "DefaultConfig.jsonc";

		// JSONC (含 // 注释) + 允许尾随逗号
		static readonly JsonDocumentOptions _jsonDocumentOptions = new() {
			CommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="rootObjectName">配置文件的根对象名称</param>
		/// <param name="logger">日志记录器，可选</param>
		/// <param name="strictness">对未知 key 的处理策略，默认为 Lax</param>
		public ConfigParser(string rootObjectName, ILogger logger, ConfigStrictness strictness = ConfigStrictness.Lax) {
			_logger = logger;
			_rootObjectName = rootObjectName;
			_strictness = strictness;

			// 此处的默认配置是指在潜入文件中的json配置文件
			ParseJsonContent(new ManifestResourceManager(logger).GetResourceInString(DefaultConfigFileName), true);
		}

		public ReadonlyConfigValue this[string key] {
			get {
				if (_configValues.TryGetValue(key, out var configValue)) {
					return new ReadonlyConfigValue(configValue);
				} else {
					_logger?.Error($"Key '{key}' is not registered. Returning empty ReadonlyConfigValue.");
					return new ReadonlyConfigValue(new ConfigValue(JsonDocument.Parse("{}").RootElement));
				}
			}
		}

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
					_logger?.Error($"Config content does not contain '{_rootObjectName}' root element.");
				}
			} catch (JsonException ex) {
				throw new MalformedConfigException(
					$"Failed to parse JSON content: {ex.Message}",
					sourcePath,
					ex
				);
			} catch (UnknownConfigKeyException) {
				throw;
			} catch (MalformedConfigException) {
				throw;
			} catch (Exception ex) {
				throw new MalformedConfigException(
					$"An unexpected error occurred while parsing config content: {ex.Message}",
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
			if (_rootObjectName == "PROGRAM") {
				_logger?.Debug($"Registering config key: '{key}' with default value: '{configValue.GetAsString()}'");
			}
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
			if (_rootObjectName == "PROGRAM") {
				_logger?.Debug($"Setting config key: '{key}' with value: '{configValue.GetAsString()}'");
			}
			if (_configValues.TryGetValue(key, out var existingConfigValue)) {
				existingConfigValue.Value = configValue.Value;
			} else {
				if (_strictness == ConfigStrictness.Strict) {
					throw new UnknownConfigKeyException(
						$"Configuration key '{key}' is not registered in the embedded default configuration. Remove the typo or remove '{key}' from your config file."
					);
				}
				_logger?.Warning($"Key '{key}' is not registered. Skipping.");
			}
		}
	}
}