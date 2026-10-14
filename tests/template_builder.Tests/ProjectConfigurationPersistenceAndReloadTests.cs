using System;
using System.IO;
using Core.Models;
using template_builder.Tests.Fixtures;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests {
    public class ProjectConfigurationPersistenceAndReloadTests {
        [Fact]
        public void Reload_DeletedKey_ResetsModelAndParserConsistently() {
            var cfg = new ProjectConfiguration();
            cfg.LoadProjectConfig("""{"TEX":{"layout":{"columns":1}},"PROGRAM":{}}""");
            Assert.Equal("1", cfg.GetEffectiveValue("LAYOUT_COLUMNS"));
            Assert.Equal(1, cfg.TexParser["LAYOUT_COLUMNS"].GetAsInt());

            // 删键重载：第二轮不含 columns，模型与解析器均应重置回内置默认值 2
            cfg.LoadProjectConfig("""{"TEX":{},"PROGRAM":{}}""");
            Assert.Equal("2", cfg.GetEffectiveValue("LAYOUT_COLUMNS"));
            Assert.Equal(2, cfg.TexParser["LAYOUT_COLUMNS"].GetAsInt());
        }

        [Fact]
        public void Reload_WithActiveOverrides_ReappliesOverridesAtomically() {
            var cfg = new ProjectConfiguration();
            cfg.LoadProjectConfig("""{"TEX":{"author":"Original"},"PROGRAM":{}}""");
            cfg.TrySetOverride("AUTHOR", "OverriddenAuthor");
            Assert.Equal("OverriddenAuthor", cfg.GetEffectiveValue("AUTHOR"));

            // 重载其他配置，活跃覆盖项应被重新应用
            cfg.LoadProjectConfig("""{"TEX":{"subject":"NewSubject"},"PROGRAM":{}}""");
            Assert.Equal("OverriddenAuthor", cfg.GetEffectiveValue("AUTHOR"));
            Assert.Equal("NewSubject", cfg.GetEffectiveValue("SUBJECT"));
        }

        [Fact]
        public void Reload_WhenMalformed_PreservesPreviousStateWithoutCorruption() {
            var cfg = new ProjectConfiguration();
            cfg.LoadProjectConfig("""{"TEX":{"author":"OriginalAuthor"},"PROGRAM":{}}""");

            Assert.Throws<MalformedConfigException>(() => {
                cfg.LoadProjectConfig("{ invalid json content }");
            });

            // 验证先前的有效状态未被破坏
            Assert.Equal("OriginalAuthor", cfg.GetEffectiveValue("AUTHOR"));
            Assert.Equal("OriginalAuthor", cfg.TexParser["AUTHOR"].GetAsString());
        }

        [Fact]
        public void SaveAs_ToExistingFileWithConflictingValues_OverwritesWithSessionEffectiveState() {
            using var tmp = TempDir.Create();
            var target = Path.Combine(tmp.Path, "existing.json");
            File.WriteAllText(target, """{"TEX":{"layout":{"columns":1}},"PROGRAM":{}}""");

            // 当前会话为默认双栏 (columns=2)
            var cfg = new ProjectConfiguration();
            Assert.Equal(2, cfg.TexParser["LAYOUT_COLUMNS"].GetAsInt());

            // 另存为至 target
            cfg.SaveToFile(target);

            // 验证重载后的文件完整代表当前会话状态，而不是混入目标旧值 1
            var reloaded = ProjectConfiguration.FromFile(target);
            Assert.Equal(2, reloaded.TexParser["LAYOUT_COLUMNS"].GetAsInt());
            Assert.Equal("2", reloaded.GetEffectiveValue("LAYOUT_COLUMNS"));
        }

        [Fact]
        public void SaveToFile_PreservesOperatorComments_CreatesBackup_AndDeliversSchema() {
            using var tmp = TempDir.Create();
            var target = Path.Combine(tmp.Path, "config.jsonc");
            const string initialContent = """
            // operator important comment
            {
                "$schema": "./DefaultConfig.schema.json",
                "TEX": {
                    // layout section comment
                    "layout": {
                        "columns": 1 // inline comment
                    }
                },
                "PROGRAM": {}
            }
            """;
            File.WriteAllText(target, initialContent);

            var cfg = ProjectConfiguration.FromFile(target);
            cfg.TrySetOverride("LAYOUT_COLUMNS", "2");
            cfg.SaveToFile(target);

            var savedText = File.ReadAllText(target);
            // 验证注释保留契约
            Assert.Contains("// operator important comment", savedText);
            Assert.Contains("// layout section comment", savedText);
            Assert.Contains("// inline comment", savedText);
            Assert.Contains("2", savedText);

            // 验证 .bak 备份生成
            var backupPath = target + ".bak";
            Assert.True(File.Exists(backupPath));
            Assert.Equal(initialContent, File.ReadAllText(backupPath));

            // 验证配套 Schema 交付
            var schemaPath = Path.Combine(tmp.Path, "DefaultConfig.schema.json");
            Assert.True(File.Exists(schemaPath));
        }

        [Fact]
        public void SaveToFile_WhenTargetCorrupted_ThrowsAndDoesNotOverwrite() {
            using var tmp = TempDir.Create();
            var target = Path.Combine(tmp.Path, "corrupted.jsonc");
            const string corruptContent = "{ corrupted json without closing";
            File.WriteAllText(target, corruptContent);

            var cfg = new ProjectConfiguration();
            Assert.Throws<MalformedConfigException>(() => {
                cfg.SaveToFile(target);
            });

            // 损坏文件完好保留，未被静默覆盖或截断
            Assert.Equal(corruptContent, File.ReadAllText(target));
        }
    }
}
