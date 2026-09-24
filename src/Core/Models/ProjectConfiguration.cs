using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Tui;
using Utils;
using Utils.Exceptions;

namespace Core.Models {
    /// <summary>
    /// 统一项目配置模型：
    /// 集中管理内置默认配置、项目配置文件与运行时覆盖，消除各模块对配置的割裂解析。
    /// 遵循“内置默认 (Builtin) -> 项目配置 (Project) -> 运行时修改 (Override)”的严格分层优先级。
    /// </summary>
    internal sealed class ProjectConfiguration {
        private readonly Dictionary<string, ConfigItem> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConfigParser _texParser;
        private readonly ConfigParser _programParser;
        private readonly ILogger? _logger;

        public ConfigParser TexParser => _texParser;
        public ConfigParser ProgramParser => _programParser;
        public IReadOnlyDictionary<string, ConfigItem> Items => _items;

        public ProjectConfiguration(ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax) {
            _logger = logger;
            _texParser = new ConfigParser("TEX", logger, strictness);
            _programParser = new ConfigParser("PROGRAM", logger, strictness);
            InitializeFromParsers();
        }

        private void InitializeFromParsers() {
            foreach (var (key, value) in _texParser.GetAllConfigsAsString()) {
                _items[key] = new ConfigItem(key, "TEX", value);
            }
            foreach (var (key, value) in _programParser.GetAllConfigsAsString()) {
                _items[key] = new ConfigItem(key, "PROGRAM", value);
            }
        }

        public void LoadProjectConfig(string jsonContent, string? sourcePath = null) {
            if (string.IsNullOrWhiteSpace(jsonContent)) {
                throw new MalformedConfigException("配置文件内容为空。", sourcePath);
            }

            // 首先通过两个 ConfigParser 执行语法与严格模式 key 校验
            _texParser.ParseConfigFile(jsonContent, sourcePath);
            _programParser.ParseConfigFile(jsonContent, sourcePath);

            // 清理原先的 ProjectValue，避免重新加载时残留
            foreach (var item in _items.Values) {
                item.SetProjectValue(null);
            }

            try {
                // 提取 JSON 中明确声明的配置键，赋值为 ProjectValue
                using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (doc.RootElement.ValueKind != JsonValueKind.Object) {
                    throw new MalformedConfigException("配置根节点必须为 JSON 对象。", sourcePath);
                }

                if (doc.RootElement.TryGetProperty("TEX", out var texElem) && texElem.ValueKind == JsonValueKind.Object) {
                    var texKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    ExtractKeys_R(texElem, [], texKeys);
                    foreach (var (k, v) in texKeys) {
                        if (_items.TryGetValue(k, out var item)) {
                            item.SetProjectValue(v);
                        } else {
                            _items[k] = new ConfigItem(k, "TEX", null, projectValue: v);
                        }
                    }
                }

                if (doc.RootElement.TryGetProperty("PROGRAM", out var progElem) && progElem.ValueKind == JsonValueKind.Object) {
                    var progKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    ExtractKeys_R(progElem, [], progKeys);
                    foreach (var (k, v) in progKeys) {
                        if (_items.TryGetValue(k, out var item)) {
                            item.SetProjectValue(v);
                        } else {
                            _items[k] = new ConfigItem(k, "PROGRAM", null, projectValue: v);
                        }
                    }
                }
            } catch (JsonException ex) {
                throw new MalformedConfigException($"解析 JSON 内容失败：{ex.Message}", sourcePath, ex);
            }
        }

        private static void ExtractKeys_R(JsonElement element, List<string> path, Dictionary<string, string> results) {
            if (element.ValueKind == JsonValueKind.Object) {
                foreach (var prop in element.EnumerateObject()) {
                    path.Add(prop.Name.ToUpperInvariant());
                    ExtractKeys_R(prop.Value, path, results);
                    path.RemoveAt(path.Count - 1);
                }
            } else {
                var key = string.Join('_', path);
                results[key] = element.ValueKind == JsonValueKind.String
                    ? (element.GetString() ?? string.Empty)
                    : element.GetRawText();
            }
        }

        public bool TrySetOverride(string key, string value) {
            var applied = _texParser.TrySetConfigValueFromString(key, value)
                       || _programParser.TrySetConfigValueFromString(key, value);
            if (applied) {
                if (_items.TryGetValue(key, out var item)) {
                    item.SetOverride(value);
                } else {
                    var section = _programParser.ContainsKey(key) ? "PROGRAM" : "TEX";
                    var newItem = new ConfigItem(key, section, null, overrideValue: value);
                    _items[key] = newItem;
                }
                return true;
            }
            return false;
        }

        public bool ClearOverride(string key) {
            if (_items.TryGetValue(key, out var item)) {
                item.ClearOverride();
                var fallbackVal = item.EffectiveValue ?? string.Empty;
                var applied = _texParser.TrySetConfigValueFromString(key, fallbackVal)
                           || _programParser.TrySetConfigValueFromString(key, fallbackVal);
                return applied;
            }
            return false;
        }

        public bool ResetToBuiltin(string key) {
            if (_items.TryGetValue(key, out var item) && item.BuiltinValue != null) {
                if (item.ProjectValue != null) {
                    return TrySetOverride(key, item.BuiltinValue);
                } else {
                    return ClearOverride(key);
                }
            }
            return false;
        }

        /// <summary>
        /// 将所有配置项重置回内置默认值（清空所有运行时覆盖并清除项目配置覆盖效果）。
        /// </summary>
        public void ResetAllToBuiltin() {
            foreach (var key in _items.Keys.ToList()) {
                ResetToBuiltin(key);
            }
        }

        /// <summary>
        /// 从指定的 JSON 配置文件路径加载并初始化统一项目配置模型。
        /// </summary>
        public static ProjectConfiguration FromFile(string path, ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax) {
            var config = new ProjectConfiguration(logger, strictness);
            config.LoadProjectConfig(File.ReadAllText(path), path);
            return config;
        }

        public bool ResetToProject(string key) => ClearOverride(key);

        public IReadOnlyDictionary<string, string> GetActiveOverrides() {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items.Values) {
                if (item.Source == ConfigSource.Override && item.OverrideValue != null) {
                    dict[item.Key] = item.OverrideValue;
                }
            }
            return dict;
        }

        public IReadOnlyDictionary<string, string> GetEffectiveValues() {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, item) in _items) {
                dict[k] = item.EffectiveValue ?? string.Empty;
            }
            return dict;
        }

        public ConfigItem? GetItem(string key) => _items.GetValueOrDefault(key);
        public string? GetEffectiveValue(string key) => GetItem(key)?.EffectiveValue;
        public ConfigSource? GetSource(string key) => GetItem(key)?.Source;
        public bool IsOverridden(string key) => GetItem(key)?.IsOverridden == true;

        /// <summary>
        /// 将当前项目配置与全部生效覆盖持久化保存到指定的 JSON 配置文件。
        /// 保存后，所有运行时覆盖将晋升为项目配置值（HasProjectValue = true），覆盖状态被清空。
        /// </summary>
        public void SaveToFile(string targetPath) {
            JsonObject rootNode;
            if (File.Exists(targetPath)) {
                try {
                    var existingJson = File.ReadAllText(targetPath);
                    rootNode = JsonNode.Parse(existingJson, null, new JsonDocumentOptions {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) as JsonObject ?? new JsonObject();
                } catch {
                    rootNode = new JsonObject();
                }
            } else {
                try {
                    var defaultJson = new ManifestResourceManager().GetResourceInString("DefaultConfig.jsonc");
                    rootNode = JsonNode.Parse(defaultJson, null, new JsonDocumentOptions {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) as JsonObject ?? new JsonObject();
                } catch {
                    rootNode = new JsonObject();
                }
            }

            if (rootNode["$schema"] == null) {
                rootNode["$schema"] = "./DefaultConfig.schema.json";
            }

            // 更新所有有项目值或覆盖值的配置项
            foreach (var field in TuiSchemaCatalog.AllFields) {
                var item = GetItem(field.Key);
                if (item == null) continue;

                // 仅当用户覆盖了该项，或者该项原本就有项目值时写回
                if (!item.IsOverridden && !item.HasProjectValue) continue;

                var val = item.EffectiveValue ?? string.Empty;
                SetNodeValueByPath(rootNode, field, val);
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(targetPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(targetPath, rootNode.ToJsonString(options));

            // 保存后将所有已生效的覆盖项转变为 ProjectValue，清空 OverrideValue
            foreach (var item in _items.Values) {
                if (item.IsOverridden && item.OverrideValue != null) {
                    item.SetProjectValue(item.OverrideValue);
                    item.ClearOverride();
                }
            }
        }

        private static void SetNodeValueByPath(JsonObject root, TuiConfigField field, string val) {
            if (string.IsNullOrEmpty(field.JsonPath)) return;

            var parts = field.JsonPath.Split('.');
            if (parts.Length < 2) return;

            JsonObject current = root;
            for (int i = 0; i < parts.Length - 1; i++) {
                var part = parts[i];
                if (current[part] is JsonObject childObj) {
                    current = childObj;
                } else {
                    var newObj = new JsonObject();
                    current[part] = newObj;
                    current = newObj;
                }
            }

            var leafKey = parts[^1];
            if (field.Kind == TuiFieldKind.Toggle || field.ExpectedType == "boolean") {
                if (bool.TryParse(val, out var b)) {
                    current[leafKey] = b;
                    return;
                }
            } else if (field.Kind == TuiFieldKind.Number || field.ExpectedType is "integer" or "number") {
                if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) {
                    current[leafKey] = n;
                    return;
                }
                if (decimal.TryParse(val, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) {
                    current[leafKey] = d;
                    return;
                }
            } else if (field.Kind == TuiFieldKind.Array || field.ExpectedType == "array") {
                try {
                    var parsed = JsonNode.Parse(val);
                    if (parsed is JsonArray arr) {
                        current[leafKey] = arr;
                        return;
                    }
                } catch { }

                var items = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var jsonArr = new JsonArray();
                foreach (var s in items) {
                    jsonArr.Add(s);
                }
                current[leafKey] = jsonArr;
                return;
            }

            current[leafKey] = val;
        }
    }
}
