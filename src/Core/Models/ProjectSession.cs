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

        public ProjectSession(ProjectConfiguration configuration) {
            Configuration = configuration;
        }

        public ProjectSession(ILogger? logger = null, ConfigStrictness strictness = ConfigStrictness.Lax) {
            Configuration = new ProjectConfiguration(logger, strictness);
        }

        public void LoadConfig(string jsonContent, string? sourcePath = null) {
            Configuration.LoadProjectConfig(jsonContent, sourcePath);
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
