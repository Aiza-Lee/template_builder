using System;
using System.Collections.Generic;
using System.IO;
using Utils;

namespace Core.Models {
    /// <summary>
    /// 统一项目与编辑会话模型：贯穿向导、配置调优、构建执行与结果查看，
    /// 保障跨步骤返回与多次构建不丢失用户输入。
    /// </summary>
    internal sealed class ProjectSession {
        public DirectoryInfo? SourceDir { get; set; }
        public FileInfo? OutputPdf { get; set; }
        public FileInfo? ConfigFile { get; set; }
        public bool UserProvidedConfig { get; set; }
        public DirectoryInfo? TemplateDir { get; set; }
        public bool Verbose { get; set; }
        public ProjectConfiguration Configuration { get; set; }
        public BuildResult? LastBuildResult { get; set; }
        public string? LastFocusedCategoryId { get; set; }

        /// <summary>是否已实际从磁盘或默认源完成首次项目配置加载</summary>
        public bool IsConfigurationLoaded { get; set; }

        /// <summary>当前已加载的配置文件绝对路径，用于感知向导重选配置或返回路径操作</summary>
        public string? LoadedConfigPath { get; set; }

        public ProjectSession(ProjectConfiguration configuration) {
            Configuration = configuration;
            IsConfigurationLoaded = true;
        }

        public ProjectSession(ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax) {
            Configuration = new ProjectConfiguration(logger, strictness);
            IsConfigurationLoaded = false;
        }

        public void LoadConfig(string jsonContent, string? sourcePath = null) {
            Configuration.LoadProjectConfig(jsonContent, sourcePath);
            IsConfigurationLoaded = true;
            LoadedConfigPath = sourcePath;
        }

        public BuildSubcommandOptions ToBuildOptions() {
            if (SourceDir == null) throw new InvalidOperationException("未设置源目录");
            if (OutputPdf == null) throw new InvalidOperationException("未设置输出 PDF 路径");
            if (ConfigFile == null) throw new InvalidOperationException("未设置配置文件路径");
            return new BuildSubcommandOptions(
                SourceDir,
                OutputPdf,
                ConfigFile,
                Verbose,
                TemplateDir,
                Configuration.GetActiveOverrides()
            );
        }

        public IReadOnlyDictionary<string, string> GetOverrides() => Configuration.GetActiveOverrides();
    }
}
