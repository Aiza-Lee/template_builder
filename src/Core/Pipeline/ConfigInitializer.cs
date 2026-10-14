using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utils;
using Utils.Exceptions;

namespace Core.Pipeline {
    /// <summary>
    /// init 子命令的执行器：把嵌入式 <c>DefaultConfig.jsonc</c> 与配套 <c>DefaultConfig.schema.json</c> 写入用户指定路径与同级目录。
    /// <para>
    /// <c>--format jsonc</c>（默认）原样复制资源（含 inline <c>//</c> 注释）。<br/>
    /// <c>--format json</c> 走 <see cref="JsonNode"/> 解析再序列化以剥除注释，得到纯 JSON。
    /// </para>
    /// </summary>
    internal sealed class ConfigInitializer(ILogger logger, ManifestResourceManager resMgr) {
        private readonly ILogger _logger = logger;
        private readonly ManifestResourceManager _resMgr = resMgr;

        /// <summary>
        /// 执行 init。返回退出码（0 成功；2 参数错误；5 嵌入式资源缺失）。
        /// </summary>
        public int Run(InitSubcommandOptions options) {
            if (!string.Equals(options.Format, "jsonc", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)) {
                _logger.Error($"--format 参数值 \"{options.Format}\" 无效。合法值为 \"jsonc\" 或 \"json\"。");
                return ExitCodes.InvalidArguments;
            }

            try {
                // 读嵌入式资源：配置文件与配套 Schema
                var resourceText = _resMgr.GetResourceInString("DefaultConfig.jsonc");
                var schemaResourceText = _resMgr.GetResourceInString("DefaultConfig.schema.json");

                string outputText = string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase)
                    ? StripCommentsToJson(resourceText)
                    : resourceText;

                // 检查目标文件是否已存在：默认拒绝覆盖，显式指定 Force 时创建备份后再覆盖
                if (File.Exists(options.OutputPath.FullName)) {
                    if (!options.Force) {
                        _logger.Error($"目标配置文件 \"{options.OutputPath.FullName}\" 已存在。为防止已有配置被意外覆盖，默认拒绝写入。若确认覆盖并自动生成备份，请使用 --force (-f) 选项。");
                        return ExitCodes.InvalidArguments;
                    }

                    var backupPath = options.OutputPath.FullName + ".bak";
                    File.Copy(options.OutputPath.FullName, backupPath, overwrite: true);
                    _logger.Info($"检测到已有配置文件，已备份原配置至 \"{backupPath}\"。");
                }

                // 确保父目录存在
                var targetDir = options.OutputPath.Directory?.FullName ?? Directory.GetCurrentDirectory();
                if (options.OutputPath.Directory is { } parent && !parent.Exists) {
                    parent.Create();
                }

                File.WriteAllText(options.OutputPath.FullName, outputText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                _logger.Info($"配置文件已生成：\"{options.OutputPath.FullName}\"。");

                // 同步交付配套 Schema 文件（使 VS Code 等编辑器开箱即用支持自动补全与校验）
                var schemaPath = Path.Combine(targetDir, "DefaultConfig.schema.json");
                DeliverSchemaFile(schemaPath, schemaResourceText, options.Force, options.OutputPath.FullName);

                // 生成带准确 -c 参数的后续操作引导（含必要路径转义与引号包裹）
                var configCliArg = FormatPathForCli(options.OutputPath);
                _logger.Info($"下一步：① 编辑配置文件，按需调整选项  ② 运行 validate -s <源目录> -c {configCliArg} 校验配置  ③ 运行 build -s <源目录> -c {configCliArg} -o <输出.pdf> 生成 PDF");
                _logger.Info("提示：你也可以通过 --template-dir <dir> 为每次构建覆盖 Main.tex 和/或 CodeBlock.tex。");
                return ExitCodes.Success;
            } catch (MissingEmbeddedResourceException ex) {
                _logger.Error(ex.Message);
                return ExitCodes.MissingEmbeddedResource;
            } catch (UnauthorizedAccessException ex) {
                _logger.Error($"无法写入配置文件 \"{options.OutputPath.FullName}\"：无访问权限（{ex.Message}）。请检查目标路径权限。");
                return ExitCodes.InvalidArguments;
            } catch (DirectoryNotFoundException ex) {
                _logger.Error($"无法写入配置文件 \"{options.OutputPath.FullName}\"：目标目录不存在（{ex.Message}）。");
                return ExitCodes.InvalidArguments;
            } catch (IOException ex) {
                _logger.Error($"写入配置文件 \"{options.OutputPath.FullName}\" 时发生 I/O 错误：{ex.Message}。");
                return ExitCodes.InvalidArguments;
            } catch (Exception ex) {
                _logger.Error($"初始化配置文件失败：{ex.Message}");
                return ExitCodes.InvalidArguments;
            }
        }

        private void DeliverSchemaFile(string schemaPath, string schemaContent, bool force, string outputPathFullName) {
            if (string.Equals(outputPathFullName, schemaPath, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            try {
                if (File.Exists(schemaPath)) {
                    if (force) {
                        var backupPath = schemaPath + ".bak";
                        File.Copy(schemaPath, backupPath, overwrite: true);
                        _logger.Info($"检测到已有 Schema 文件，已备份原文件至 \"{backupPath}\"。");
                        File.WriteAllText(schemaPath, schemaContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        _logger.Info($"配套 Schema 已交付：\"{schemaPath}\"（供编辑器自动补全与校验）。");
                    } else {
                        var existingContent = File.ReadAllText(schemaPath);
                        if (string.Equals(existingContent, schemaContent, StringComparison.Ordinal)) {
                            _logger.Info($"检测到已存在匹配的 Schema 文件，保持使用：\"{schemaPath}\"。");
                        } else {
                            _logger.Info($"检测到已存在 Schema 文件，已保留现有文件：\"{schemaPath}\"。若需覆盖更新并自动生成备份，请使用 --force (-f) 选项。");
                        }
                    }
                } else {
                    File.WriteAllText(schemaPath, schemaContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    _logger.Info($"配套 Schema 已交付：\"{schemaPath}\"（供编辑器自动补全与校验）。");
                }
            } catch (Exception ex) {
                _logger.Warning($"交付配套 Schema 文件 \"{schemaPath}\" 失败：{ex.Message}。此问题不影响配置文件生成。");
            }
        }

        /// <summary>
        /// 把含 JSONC 注释的文本解析为 <see cref="JsonElement"/> 再缩进序列化为纯 JSON。
        /// 使用 <see cref="JsonDocument"/> 配合 <see cref="JsonCommentHandling.Skip"/> 跳过注释。
        /// </summary>
        internal static string StripCommentsToJson(string jsoncText) {
            var options = new JsonDocumentOptions {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            using var doc = JsonDocument.Parse(jsoncText, options);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// 将路径格式化为安全的 CLI 参数形式，确保含有空格或特殊字符时外层带双引号且内部双引号被转义。
        /// </summary>
        internal static string FormatPathForCli(FileInfo fileInfo) {
            var raw = !string.IsNullOrEmpty(fileInfo.ToString()) ? fileInfo.ToString() : fileInfo.FullName;
            return $"\"{raw.Replace("\"", "\\\"")}\"";
        }
    }
}
