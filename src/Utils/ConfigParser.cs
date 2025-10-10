using System.Text.Json;

namespace Utils {
	internal struct ConfigValue(string defaultValue) {
		public string? Value { get; set; } = defaultValue;
		public string DefaultValue { get; } = defaultValue;
	}
	internal class ConfigParser {
		private readonly Dictionary<string, ConfigValue> _configValues = [];
		private readonly ILogger? _logger;
		private readonly string _rootObjectName;

		const string DefaultConfigFileName = "DefaultConfig.json";

		/// <summary>
		/// 构造函数，传入默认配置文件路径，解析默认配置文件
		/// </summary>
		/// <param name="defaultConfigName">默认配置文件路径</param>
		public ConfigParser(string rootObjectName, ILogger? logger = null) {
			_logger = logger;
			_rootObjectName = rootObjectName;
			ParseDefaultConfig(DefaultConfigFileName);
		}

		public void ParseConfigFile(FileInfo fileInfo) {
			if (fileInfo.Extension != ".json") {
				_logger?.Error($"Config file '{fileInfo}' is not a JSON file.");
			}
			if (!fileInfo.Exists) {
				_logger?.Error($"Config file '{fileInfo}' does not exist.");
			} else {
				var jsonContent = fileInfo.OpenText().ReadToEnd();
				ParseJsonContent(jsonContent, false);
			}
		}

		public string QueryConfig(string key) {
			if (_configValues.TryGetValue(key, out var configValue)) {
				return configValue.Value ?? configValue.DefaultValue;
			} else {
				_logger?.Warning($"Key '{key}' is not registered. Returning empty string.");
				return string.Empty;
			}
		}

		private void ParseDefaultConfig(string defaultConfigName) {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(defaultConfigName);
			if (stream != null) {
				using var reader = new StreamReader(stream);
				string jsonContent = reader.ReadToEnd();
				ParseJsonContent(jsonContent, true);
			} else {
				_logger?.Error($"Default config file '{defaultConfigName}' not found in embedded resources.");
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
				_logger?.Warning($"Arrays are not supported in config files. Skipping array at '{string.Join('.', path)}'.");
			} else {
				string key = string.Join('_', path) + '_' + element.ValueKind.ToString().ToUpper();
				string value = element.GetRawText().Trim('"');
				if (isDefaultConfig) {
					RegisterConfig(key, value);
				} else {
					SetConfigValue(key, value);
				}
			}
		}

		/// <summary>
		/// 注册配置项，只有注册了的配置项才会被解析
		/// </summary>
		/// <param name="key">配置项键值</param>
		/// <param name="defaultValue">配置项默认值</param>
		private void RegisterConfig(string key, string defaultValue) {
			if (!_configValues.ContainsKey(key)) {
				_configValues[key] = new(defaultValue);
			} else {
				_logger?.Warning($"Key '{key}' is already registered. Skipping.");
			}
		}

		private void SetConfigValue(string key, string value) {
			if (_configValues.TryGetValue(key, out var configValue)) {
				_configValues[key] = new(configValue.DefaultValue) { Value = value };
			} else {
				_logger?.Warning($"Key '{key}' is not registered. Skipping.");
			}
		}
	}
}