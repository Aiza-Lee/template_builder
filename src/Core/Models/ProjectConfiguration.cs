using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Schema;
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
        private ConfigParser _texParser;
        private ConfigParser _programParser;
        private readonly ILogger? _logger;
        private readonly ConfigStrictness _strictness;

        public ConfigParser TexParser => _texParser;
        public ConfigParser ProgramParser => _programParser;
        public IReadOnlyDictionary<string, ConfigItem> Items => _items;
        public string? SourcePath { get; private set; }

        public ProjectConfiguration(ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax) {
            _logger = logger;
            _strictness = strictness;
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
                throw new MalformedConfigException("配置文件内容不能为空。", sourcePath);
            }

            // 1. 在临时解析器中初始化内置默认值（此时尚未加载用户 JSON，保留纯正 BuiltinValue）
            var newTexParser = new ConfigParser("TEX", _logger, _strictness);
            var newProgParser = new ConfigParser("PROGRAM", _logger, _strictness);

            var newItems = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in newTexParser.GetAllConfigsAsString()) {
                newItems[key] = new ConfigItem(key, "TEX", value);
            }
            foreach (var (key, value) in newProgParser.GetAllConfigsAsString()) {
                newItems[key] = new ConfigItem(key, "PROGRAM", value);
            }

            var newTexProjectKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newTexProjectRawElements = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var newProgProjectKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newProgProjectRawElements = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            // 2. 执行根结构语法解析与严格模式根 key 校验
            try {
                using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (doc.RootElement.ValueKind != JsonValueKind.Object) {
                    throw new MalformedConfigException("配置根节点必须为 JSON 对象。", sourcePath);
                }

                bool hasTex = doc.RootElement.TryGetProperty("TEX", out var texElem);
                bool hasProg = doc.RootElement.TryGetProperty("PROGRAM", out var progElem);

                if (_strictness == ConfigStrictness.Strict) {
                    foreach (var prop in doc.RootElement.EnumerateObject()) {
                        if (!string.Equals(prop.Name, "TEX", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(prop.Name, "PROGRAM", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(prop.Name, "$schema", StringComparison.OrdinalIgnoreCase)) {
                            throw new MalformedConfigException($"配置文件根节点包含未知属性 '{prop.Name}'。只允许 'TEX'、'PROGRAM' 及 '$schema'。", sourcePath);
                        }
                    }
                }

                if (hasTex) {
                    if (texElem.ValueKind != JsonValueKind.Object) {
                        throw new MalformedConfigException("配置节点 'TEX' 必须为 JSON 对象。", sourcePath);
                    }
                    ExtractKeys_R(texElem, [], newTexProjectKeys, newTexProjectRawElements);
                }

                if (hasProg) {
                    if (progElem.ValueKind != JsonValueKind.Object) {
                        throw new MalformedConfigException("配置节点 'PROGRAM' 必须为 JSON 对象。", sourcePath);
                    }
                    ExtractKeys_R(progElem, [], newProgProjectKeys, newProgProjectRawElements);
                }
            } catch (JsonException ex) {
                throw new MalformedConfigException($"解析 JSON 内容失败：{ex.Message}", sourcePath, ex);
            }

            // 3. 执行语法解析与严格模式内部 key 校验（原子性保证：若语法或 key 错误，不污染当前实例状态）
            newTexParser.ParseConfigFile(jsonContent, sourcePath);
            newProgParser.ParseConfigFile(jsonContent, sourcePath);

            foreach (var (k, v) in newTexProjectKeys) {
                newTexProjectRawElements.TryGetValue(k, out var raw);
                if (newItems.TryGetValue(k, out var item)) {
                    item.SetProjectValue(v, raw);
                } else {
                    newItems[k] = new ConfigItem(k, "TEX", null, projectValue: v, rawElement: raw);
                }
            }

            foreach (var (k, v) in newProgProjectKeys) {
                newProgProjectRawElements.TryGetValue(k, out var raw);
                if (newItems.TryGetValue(k, out var item)) {
                    item.SetProjectValue(v, raw);
                } else {
                    newItems[k] = new ConfigItem(k, "PROGRAM", null, projectValue: v, rawElement: raw);
                }
            }

            // 4. 暂存当前活跃覆盖项，供重载后重新应用（重载须原子重建各层并重新应用覆盖）
            var activeOverrides = GetActiveOverrides();

            // 5. 提交原子替换
            _texParser = newTexParser;
            _programParser = newProgParser;
            SourcePath = sourcePath;

            _items.Clear();
            foreach (var (k, item) in newItems) {
                _items[k] = item;
            }

            // 6. 重新应用活跃覆盖项
            foreach (var (k, v) in activeOverrides) {
                TrySetOverride(k, v);
            }
        }

        private static void ExtractKeys_R(
            JsonElement element,
            List<string> path,
            Dictionary<string, string> results,
            Dictionary<string, JsonElement> rawElements) {
            if (element.ValueKind == JsonValueKind.Object) {
                foreach (var prop in element.EnumerateObject()) {
                    path.Add(prop.Name.ToUpperInvariant());
                    ExtractKeys_R(prop.Value, path, results, rawElements);
                    path.RemoveAt(path.Count - 1);
                }
            } else {
                var key = string.Join('_', path);
                results[key] = element.ValueKind == JsonValueKind.String
                    ? (element.GetString() ?? string.Empty)
                    : element.GetRawText();
                rawElements[key] = element.Clone();
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
            if (string.IsNullOrWhiteSpace(targetPath)) {
                throw new ArgumentException("保存路径不能为空。", nameof(targetPath));
            }

            var fullTargetPath = Path.GetFullPath(targetPath);
            var dir = Path.GetDirectoryName(fullTargetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            // 交付配套 Schema 文件（同 init 规范，使编辑器开箱即用支持自动补全与校验）
            if (!string.IsNullOrEmpty(dir)) {
                var schemaPath = Path.Combine(dir, "DefaultConfig.schema.json");
                if (!File.Exists(schemaPath)) {
                    try {
                        var schemaText = new ManifestResourceManager().GetResourceInString("DefaultConfig.schema.json");
                        File.WriteAllText(schemaPath, schemaText, new System.Text.UTF8Encoding(false));
                    } catch {
                        // 忽略 Schema 写入异常
                    }
                }
            }

            var isSaveAs = string.IsNullOrEmpty(SourcePath) ||
                !string.Equals(Path.GetFullPath(SourcePath), fullTargetPath, StringComparison.OrdinalIgnoreCase);

            string existingContent = string.Empty;
            if (File.Exists(fullTargetPath)) {
                existingContent = File.ReadAllText(fullTargetPath);
                // 损坏目标检查：若已有目标文件存在语法错误或非 JSON 对象，拒绝静默覆盖
                try {
                    using var doc = JsonDocument.Parse(existingContent, new JsonDocumentOptions {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    if (doc.RootElement.ValueKind != JsonValueKind.Object) {
                        throw new MalformedConfigException($"目标配置文件根节点必须为 JSON 对象：\"{fullTargetPath}\"。", fullTargetPath);
                    }
                } catch (JsonException ex) {
                    throw new MalformedConfigException($"目标配置文件格式损坏（JSON 语法错误）：{ex.Message}。已中止写入以防止覆盖破坏现有文件。请手动修复或另存为其他路径。", fullTargetPath, ex);
                }

                // 创建 .bak 备份
                var backupPath = fullTargetPath + ".bak";
                File.Copy(fullTargetPath, backupPath, overwrite: true);
            }

            // 收集需要写入的配置项
            var updates = new List<(ConfigFieldDefinition Field, string Value)>();
            foreach (var field in ConfigSchemaCatalog.AllFields) {
                var item = GetItem(field.Key);
                if (item == null) continue;

                if (isSaveAs) {
                    // 另存为：保存会话完整有效状态，杜绝混入目标旧值
                    updates.Add((field, item.EffectiveValue ?? string.Empty));
                } else {
                    // 原地保存：仅当用户覆盖了该项，或者该项原本就有项目值时写回
                    if (item.IsOverridden || item.HasProjectValue) {
                        updates.Add((field, item.EffectiveValue ?? string.Empty));
                    }
                }
            }

            var finalContent = JsoncConfigUpdater.UpdateJsonc(existingContent, updates);

            // 原子化写入（写失败不截断旧文件）
            var tempFile = Path.Combine(dir ?? ".", $".{Path.GetFileName(fullTargetPath)}.{Guid.NewGuid():N}.tmp");
            try {
                File.WriteAllText(tempFile, finalContent, new System.Text.UTF8Encoding(false));
                File.Move(tempFile, fullTargetPath, overwrite: true);
            } finally {
                if (File.Exists(tempFile)) {
                    try { File.Delete(tempFile); } catch { }
                }
            }

            SourcePath = fullTargetPath;

            // 保存后将所有已生效的覆盖项转变为 ProjectValue，清空 OverrideValue
            foreach (var item in _items.Values) {
                if (item.IsOverridden && item.OverrideValue != null) {
                    item.SetProjectValue(item.OverrideValue);
                    item.ClearOverride();
                }
            }
        }
    }
}
