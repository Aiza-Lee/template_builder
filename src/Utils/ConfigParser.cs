using System.Text.Json;

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
	}

	internal class ReadonlyConfigValue(ConfigValue configValue) {
		private ConfigValue ConfigValue { get; } = configValue;
		public string GetAsString() => ConfigValue.GetAsString();
		public string[] GetAsStringArray() => ConfigValue.GetAsStringArray();
		public int GetAsInt() => ConfigValue.GetAsInt();
		public bool GetAsBool() => ConfigValue.GetAsBool();
	}

	internal class ConfigParser : IConfigParser {
		private readonly Dictionary<string, ConfigValue> _configValues = [];
		private readonly ILogger? _logger;
		private readonly string _rootObjectName;

		const string DefaultConfigFileName = "DefaultConfig.json";

		/// <summary>
		/// 构造函数
		/// <param name="rootObjectName">配置文件的根对象名称</param>
		/// <param name="logger">日志记录器，可选</param>
		/// </summary>
		public ConfigParser(string rootObjectName, ILogger? logger = null) {
			_logger = logger;
			_rootObjectName = rootObjectName;
			ParseDefaultConfig();
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

		public IEnumerable<KeyValuePair<string, string>> GetAllConfigsAsString() {
			foreach (var kvp in _configValues) {
				yield return new(kvp.Key, kvp.Value.GetAsString());
			}
		}

		public void ParseConfigFile(string content) {
			if (string.IsNullOrWhiteSpace(content)) {
				_logger?.Error($"Config file content is empty.");
				return;
			}
			ParseJsonContent(content, false);
		}

		private void ParseDefaultConfig() {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(DefaultConfigFileName);
			if (stream != null) {
				using var reader = new StreamReader(stream);
				string jsonContent = reader.ReadToEnd();
				ParseJsonContent(jsonContent, true);
			} else {
				_logger?.Error($"Default config file '{DefaultConfigFileName}' not found in embedded resources.");
			}
		}

		private void ParseJsonContent(string jsonContent, bool isDefaultConfig) {
			try {
				var json = JsonDocument.Parse(jsonContent).RootElement;
				if (json.TryGetProperty(_rootObjectName, out var jsonElement)) {
					ParseJsonElement_R(jsonElement, [], isDefaultConfig);
				} else {
					_logger?.Error($"Config content does not contain '{_rootObjectName}' root element.");
				}
			} catch (JsonException ex) {
				_logger?.Error($"Failed to parse JSON content: {ex.Message}");
			} catch (Exception ex) {
				_logger?.Error($"An unexpected error occurred while parsing config content: {ex.Message}");
			}
		}

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
		/// <param name="key">配置项键值</param>
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

		private void SetConfigValue(string key, ConfigValue configValue) {
			if (_rootObjectName == "PROGRAM") {
				_logger?.Debug($"Setting config key: '{key}' with value: '{configValue.GetAsString()}'");
			}
			if (_configValues.TryGetValue(key, out var existingConfigValue)) {
				existingConfigValue.Value = configValue.Value;
			} else {
				_logger?.Warning($"Key '{key}' is not registered. Skipping.");
			}
		}
	}
}