using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Core.Schema;
using Utils;

namespace Core.Models {
    /// <summary>
    /// 保留注释与格式的 JSONC 配置更新器：
    /// 利用 Utf8JsonReader 的 JsonCommentHandling.Allow 精确定位字段的值 Token 字节区间，
    /// 仅对变动的配置值进行原地字节替换，完整保留现存的单行 //、多行 /* */ 注释及空白格式。
    /// </summary>
    internal static class JsoncConfigUpdater {
        public static string UpdateJsonc(
            string jsoncContent,
            IEnumerable<(ConfigFieldDefinition Field, string Value)> updates,
            bool ensureSchema = true
        ) {
            if (string.IsNullOrWhiteSpace(jsoncContent)) {
                jsoncContent = new ManifestResourceManager().GetResourceInString("DefaultConfig.jsonc");
            }

            var bytes = Encoding.UTF8.GetBytes(jsoncContent);
            var propertySpans = new Dictionary<string, (int start, int end)>(StringComparer.OrdinalIgnoreCase);
            var objectSpans = new Dictionary<string, (int openBrace, int closeBrace, bool hasChildren)>(StringComparer.OrdinalIgnoreCase);
            var pathStack = new Stack<string>();
            var openBraceStack = new Stack<int>();
            string? pendingProp = null;
            var childCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions {
                CommentHandling = JsonCommentHandling.Allow,
                AllowTrailingCommas = true
            });

            while (reader.Read()) {
                switch (reader.TokenType) {
                    case JsonTokenType.PropertyName:
                        pendingProp = reader.GetString();
                        break;

                    case JsonTokenType.StartObject:
                        string currentObjPath;
                        if (pendingProp != null) {
                            pathStack.Push(pendingProp);
                            currentObjPath = string.Join('.', pathStack.Reverse());
                            pendingProp = null;
                        } else {
                            currentObjPath = string.Empty; // 根对象
                        }
                        childCounts[currentObjPath] = 0;
                        openBraceStack.Push((int)reader.TokenStartIndex);
                        break;

                    case JsonTokenType.EndObject:
                        string closedObjPath = string.Join('.', pathStack.Reverse());
                        int openBrace = openBraceStack.Count > 0 ? openBraceStack.Pop() : 0;
                        int closeBrace = (int)reader.TokenStartIndex;
                        bool hasChildren = childCounts.GetValueOrDefault(closedObjPath, 0) > 0;
                        objectSpans[closedObjPath] = (openBrace, closeBrace, hasChildren);

                        if (pathStack.Count > 0) {
                            pathStack.Pop();
                            var parentPath = string.Join('.', pathStack.Reverse());
                            childCounts[parentPath] = childCounts.GetValueOrDefault(parentPath, 0) + 1;
                        }
                        break;

                    case JsonTokenType.StartArray:
                        if (pendingProp != null) {
                            var parentPath = string.Join('.', pathStack.Reverse());
                            childCounts[parentPath] = childCounts.GetValueOrDefault(parentPath, 0) + 1;
                            var fullPropPath = string.IsNullOrEmpty(parentPath) ? pendingProp : $"{parentPath}.{pendingProp}";

                            int start = (int)reader.TokenStartIndex;
                            int depth = 1;
                            while (depth > 0 && reader.Read()) {
                                if (reader.TokenType == JsonTokenType.StartArray) depth++;
                                else if (reader.TokenType == JsonTokenType.EndArray) depth--;
                            }
                            int end = (int)reader.BytesConsumed;
                            propertySpans[fullPropPath] = (start, end);
                            pendingProp = null;
                        }
                        break;

                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        if (pendingProp != null) {
                            var parentPath = string.Join('.', pathStack.Reverse());
                            childCounts[parentPath] = childCounts.GetValueOrDefault(parentPath, 0) + 1;
                            var fullPropPath = string.IsNullOrEmpty(parentPath) ? pendingProp : $"{parentPath}.{pendingProp}";
                            int start = (int)reader.TokenStartIndex;
                            int end = (int)reader.BytesConsumed;
                            propertySpans[fullPropPath] = (start, end);
                            pendingProp = null;
                        }
                        break;
                }
            }

            var edits = new List<(int Start, int End, byte[] Replacement)>();

            // 1. 处理 $schema
            if (ensureSchema && !propertySpans.ContainsKey("$schema") && objectSpans.TryGetValue(string.Empty, out var rootObj)) {
                var schemaInsert = Encoding.UTF8.GetBytes("\n\t\"$schema\": \"./DefaultConfig.schema.json\",");
                edits.Add((rootObj.openBrace + 1, rootObj.openBrace + 1, schemaInsert));
            }

            // 2. 依次生成配置更新 Edits
            var missingInserts = new Dictionary<string, List<(string SubPath, string FormattedVal)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (field, val) in updates) {
                if (string.IsNullOrEmpty(field.JsonPath)) continue;

                var formattedVal = FormatValue(field, val);
                var formattedBytes = Encoding.UTF8.GetBytes(formattedVal);

                if (propertySpans.TryGetValue(field.JsonPath, out var span)) {
                    var currentSpanBytes = bytes.AsSpan(span.start, span.end - span.start);
                    if (!currentSpanBytes.SequenceEqual(formattedBytes)) {
                        edits.Add((span.start, span.end, formattedBytes));
                    }
                } else {
                    // 该属性尚不存在，计算其挂载的父节点
                    var parts = field.JsonPath.Split('.');
                    if (parts.Length < 2) continue;

                    var section = parts[0];
                    var subPath = string.Join('.', parts.Skip(1));

                    if (!missingInserts.TryGetValue(section, out var list)) {
                        list = new List<(string, string)>();
                        missingInserts[section] = list;
                    }
                    list.Add((subPath, formattedVal));
                }
            }

            // 3. 处理缺失字段的结构化补全写入
            foreach (var (section, items) in missingInserts) {
                if (objectSpans.TryGetValue(section, out var secObj)) {
                    var sb = new StringBuilder();
                    foreach (var (subPath, fVal) in items) {
                        if (secObj.hasChildren || sb.Length > 0) {
                            sb.Append(",\n\t\t");
                        } else {
                            sb.Append("\n\t\t");
                        }
                        AppendNestedProperty(sb, subPath.Split('.'), fVal, 2);
                    }
                    var insertBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    edits.Add((secObj.closeBrace, secObj.closeBrace, insertBytes));
                } else if (objectSpans.TryGetValue(string.Empty, out var rootObjSpan)) {
                    var sb = new StringBuilder();
                    if (rootObjSpan.hasChildren) {
                        sb.Append(",\n\t\"");
                    } else {
                        sb.Append("\n\t\"");
                    }
                    sb.Append(section).Append("\": {\n");
                    for (int i = 0; i < items.Count; i++) {
                        var (subPath, fVal) = items[i];
                        if (i > 0) sb.Append(",\n");
                        sb.Append("\t\t");
                        AppendNestedProperty(sb, subPath.Split('.'), fVal, 2);
                    }
                    sb.Append("\n\t}");
                    var insertBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    edits.Add((rootObjSpan.closeBrace, rootObjSpan.closeBrace, insertBytes));
                }
            }

            if (edits.Count == 0) return jsoncContent;

            // 4. 按起始偏移升序重组字节流
            edits.Sort((a, b) => a.Start.CompareTo(b.Start));
            using var ms = new MemoryStream();
            int currentPos = 0;

            foreach (var edit in edits) {
                if (edit.Start > currentPos) {
                    ms.Write(bytes, currentPos, edit.Start - currentPos);
                }
                ms.Write(edit.Replacement, 0, edit.Replacement.Length);
                currentPos = edit.End;
            }

            if (currentPos < bytes.Length) {
                ms.Write(bytes, currentPos, bytes.Length - currentPos);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static void AppendNestedProperty(StringBuilder sb, string[] parts, string formattedVal, int indentLevel) {
            if (parts.Length == 1) {
                sb.Append('"').Append(parts[0]).Append("\": ").Append(formattedVal);
            } else {
                sb.Append('"').Append(parts[0]).Append("\": {\n");
                sb.Append(new string('\t', indentLevel + 1));
                AppendNestedProperty(sb, parts.Skip(1).ToArray(), formattedVal, indentLevel + 1);
                sb.Append('\n').Append(new string('\t', indentLevel)).Append('}');
            }
        }

        private static string FormatValue(ConfigFieldDefinition? field, string rawValue) {
            if (field != null) {
                if (field.ExpectedType == "boolean") {
                    return string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
                }
                if (field.ExpectedType is "integer" or "number") {
                    if (long.TryParse(rawValue, out var num)) return num.ToString();
                    if (decimal.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dec)) {
                        return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    return rawValue;
                }
                if (field.ExpectedType == "array") {
                    var trimmed = rawValue.Trim();
                    if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) {
                        return trimmed;
                    }
                    var items = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return JsonSerializer.Serialize(items);
                }
            }
            return JsonSerializer.Serialize(rawValue);
        }
    }
}
