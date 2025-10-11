using System.Text.Json;

namespace Utils {
	internal struct ConfigValue(string[] defaultValue) {
		public string[]? Value { get; set; } = defaultValue;
		public string[] DefaultValue { get; } = defaultValue;
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

		public IEnumerable<KeyValuePair<string, string[]>> GetAllConfigs() {
			foreach (var kvp in _configValues) {
				yield return new(
					kvp.Key,
					kvp.Value.Value ?? kvp.Value.DefaultValue
				);
			}
		}

		public void ParseConfigFile(string content) {
			if (string.IsNullOrWhiteSpace(content)) {
				_logger?.Error($"Config file content is empty.");
				return;
			}
			ParseJsonContent(content, false);
		}

		public string[] QueryConfig(string key) {
			if (_configValues.TryGetValue(key, out var configValue)) {
				return configValue.Value ?? configValue.DefaultValue;
			} else {
				_logger?.Warning($"Key '{key}' is not registered. Returning empty array.");
				return [];
			}
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
				if (json.TryGetProperty(_rootObjectName, out var texElement)) {
					ParseJsonElement_R(texElement, [], isDefaultConfig);
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
			} else if (element.ValueKind == JsonValueKind.Array) {
				var values = new List<string>();
				foreach (var item in element.EnumerateArray()) {
					if (item.ValueKind == JsonValueKind.String) {
						values.Add(item.GetString() ?? string.Empty);
					} else {
						_logger?.Warning($"Unsupported array item type '{item.ValueKind}' at '{string.Join('_', path)}'. Skipping item.");
					}
				}
				string key = string.Join('_', path);
				if (isDefaultConfig) {
					RegisterConfig(key, [.. values]);
				} else {
					SetConfigValue(key, [.. values]);
				}
			} else {
				string key = string.Join('_', path);

				string OtherValueAction() {
					_logger?.Warning($"Unsupported JSON value type '{element.ValueKind}' at '{string.Join('_', path)}'. Storing raw text.");
					return element.GetRawText();
				}

				string value = element.ValueKind switch {
					JsonValueKind.String => element.GetString() ?? string.Empty,
					JsonValueKind.Number => element.GetRawText(),
					JsonValueKind.True => "true",
					JsonValueKind.False => "false",
					JsonValueKind.Null => string.Empty,
					_ => OtherValueAction()
				};

				if (isDefaultConfig) {
					RegisterConfig(key, [value]);
				} else {
					SetConfigValue(key, [value]);
				}
			}
		}

		/// <summary>
		/// 注册配置项，只有注册了的配置项才会被解析
		/// </summary>
		/// <param name="key">配置项键值</param>
		/// <param name="defaultValue">配置项默认值</param>
		private void RegisterConfig(string key, string[] defaultValue) {
			if (!_configValues.ContainsKey(key)) {
				_configValues[key] = new(defaultValue);
			} else {
				_logger?.Warning($"Key '{key}' is already registered. Skipping.");
			}
		}

		private void SetConfigValue(string key, string[] value) {
			if (_configValues.TryGetValue(key, out var configValue)) {
				_configValues[key] = new(configValue.DefaultValue) { Value = value };
			} else {
				_logger?.Warning($"Key '{key}' is not registered. Skipping.");
			}
		}
	}
}