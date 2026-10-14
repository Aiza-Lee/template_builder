using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Core;
using Core.Commands;
using Core.Models;
using Core.Pipeline;
using Core.Validation;
using Spectre.Console;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests;

/// <summary>
/// 用户核心路径端到端回归验收测试套件 (UX-15)。
/// 覆盖 10 个关键用户路径与保障场景：
/// 1. 文件保护 (File Protection)
/// 2. 恢复默认后产物一致 (Restore Builtin Defaults Reproducibility)
/// 3. 显式清空 (Explicit Clear)
/// 4. 返回不丢修改 (Back Without Losing Edits)
/// 5. 保存后重启复现 (Save Session & Reopen Reproducibility)
/// 6. 失败保留旧 PDF (Failure Preserves Prior Output)
/// 7. 错误修复后重试 (Error Fix & Retry Flow)
/// 8. 空输入无法伪成功 (Empty Input Rejection)
/// 9. JSON 可直接解析 (Pure JSON Protocol Output)
/// 10. 快速开始与 Docker 脚本端到端可执行 (Quickstart & Scripts Validation)
/// 11. 真实 XeLaTeX 端到端元数据与原子保护 (Real XeLaTeX End-to-End Verification)
/// </summary>
public class UserPathRegressionTests {
    // -------------------------------------------------------------------------
    // 场景 1: 文件保护 (File Protection)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario01_FileProtection_UserFilesInBuildDirAndExistingConfigsAreProtected() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "my_src"));
        var userBuildDir = Directory.CreateDirectory(Path.Combine(srcDir.FullName, "build"));
        var userScript = Path.Combine(userBuildDir.FullName, "my_custom_script.sh");
        File.WriteAllText(userScript, "#!/usr/bin/env bash\necho 'do not delete me'");

        // 验证 1.1: 即使源码目录下有名为 "build" 的用户目录，构建流水线也绝不误删该目录与用户文件
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var fakeRunner = new FakeXelatexRunner();
        fakeRunner.Results.Enqueue(new XelatexResult(0, "", false));
        fakeRunner.SideEffects.Enqueue((workingDir, _) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 test");
        });

        File.WriteAllText(Path.Combine(srcDir.FullName, "main.cpp"), "int main() { return 0; }");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var texParser = new ConfigParser("TEX", logger, ConfigStrictness.Lax);
        var progParser = new ConfigParser("PROGRAM", logger, ConfigStrictness.Lax);
        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var builder = new PdfBuilder(logger, options, texParser, progParser, resMgr, fakeRunner);
        var exitCode = builder.Build();

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(userScript), "用户源码目录下的 build/ 文件在构建后必须完好保留");
        Assert.Equal("#!/usr/bin/env bash\necho 'do not delete me'", File.ReadAllText(userScript));

        // 验证 1.2: init 拒绝在未加 --force 时覆盖用户已有配置
        var existingInitTarget = Path.Combine(tmp.Path, "init_target.jsonc");
        File.WriteAllText(existingInitTarget, "// existing user config");
        var initializer = new ConfigInitializer(logger, resMgr);
        var initExit = initializer.Run(new InitSubcommandOptions(new FileInfo(existingInitTarget), Format: "jsonc", Force: false));

        Assert.Equal(ExitCodes.InvalidArguments, initExit);
        Assert.Equal("// existing user config", File.ReadAllText(existingInitTarget));

        // 验证 1.3: init --force 会创建 .bak 备份
        var initForceExit = initializer.Run(new InitSubcommandOptions(new FileInfo(existingInitTarget), Format: "jsonc", Force: true));
        Assert.Equal(ExitCodes.Success, initForceExit);
        Assert.True(File.Exists(existingInitTarget + ".bak"));
        Assert.Equal("// existing user config", File.ReadAllText(existingInitTarget + ".bak"));
    }

    // -------------------------------------------------------------------------
    // 场景 2: 恢复默认后产物一致 (Restore Builtin Defaults Reproducibility)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario02_RestoreBuiltinDefaults_ProducesIdenticalTexToFreshSession() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "algo.cpp"), "// algo");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "doc.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();

        // 2.1 全新未修改的 Session 生成 TeX
        var virginSession = new ProjectSession {
            SourceDir = srcDir,
            OutputPdf = outPdf,
            ConfigFile = cfgFile
        };
        var virginBuilder = new PdfBuilder(logger, virginSession.ToBuildOptions(), virginSession.Configuration, resMgr);
        var virginTex = virginBuilder.GenerateTexContent_ForTest();

        // 2.2 经历了大量修改而后执行 ResetAllToBuiltin() 的 Session
        var dirtySession = new ProjectSession {
            SourceDir = srcDir,
            OutputPdf = outPdf,
            ConfigFile = cfgFile
        };
        dirtySession.Configuration.TrySetOverride("TITLE_CONTENT", "临时自定义标题");
        dirtySession.Configuration.TrySetOverride("LAYOUT_COLUMNS", "1");
        dirtySession.Configuration.TrySetOverride("CODE_FONT_SIZE", "12pt");
        dirtySession.Configuration.TrySetOverride("TITLE_ENABLE_COVER", "true");

        // 执行重置恢复
        dirtySession.Configuration.ResetAllToBuiltin();

        var dirtyBuilder = new PdfBuilder(logger, dirtySession.ToBuildOptions(), dirtySession.Configuration, resMgr);
        var restoredTex = dirtyBuilder.GenerateTexContent_ForTest();

        // 验证：恢复默认后生成的 TeX 与从未修改过的全新会话 100% 逐行完全一致
        Assert.Equal(virginTex, restoredTex);
    }

    // -------------------------------------------------------------------------
    // 场景 3: 显式清空 (Explicit Clear)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario03_ExplicitClear_RemovesAuthorSubjectAndNoteWithoutRemnants() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "my_special_templates_v2"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "main.cpp"), "int main(){}");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));

        // 显式将 author, subject, note 设为空字符串
        File.WriteAllText(cfgFile.FullName, """
        {
            "TEX": {
                "author": "",
                "subject": "",
                "title": {
                    "content": "",
                    "note": ""
                }
            }
        }
        """);

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var texParser = new ConfigParser("TEX", logger, ConfigStrictness.Lax);
        texParser.ParseConfigFile(File.ReadAllText(cfgFile.FullName));
        var progParser = new ConfigParser("PROGRAM", logger, ConfigStrictness.Lax);
        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        var builder = new PdfBuilder(logger, options, texParser, progParser, resMgr);
        var texContent = builder.GenerateTexContent_ForTest();

        // 验证：元数据中 pdfauthor 和 pdfsubject 为空
        Assert.Contains(@"pdfauthor={}", texContent);
        Assert.Contains(@"pdfsubject={}", texContent);

        // 验证：绝无历史占位符或开发者个人署名
        Assert.DoesNotContain("Aiza", texContent);
        Assert.DoesNotContain("文档主题", texContent);
        Assert.DoesNotContain("标题页额外内容", texContent);

        // 验证：主标题为空时从源码目录名安全推导并转义下划线
        Assert.Contains(@"\title{\Large\bfseries my\_special\_templates\_v2}", texContent);
        Assert.Contains(@"pdftitle={my\_special\_templates\_v2}", texContent);
    }

    // -------------------------------------------------------------------------
    // 场景 4: 返回不丢修改 (Back Without Losing Edits)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario04_BackNavigation_PreservesAllEditsAcrossMenus() {
        using var tmp = TempDir.Create();
        var src1 = Directory.CreateDirectory(Path.Combine(tmp.Path, "src1"));
        var src2 = Directory.CreateDirectory(Path.Combine(tmp.Path, "src2"));
        var out1 = new FileInfo(Path.Combine(tmp.Path, "out1.pdf"));
        var out2 = new FileInfo(Path.Combine(tmp.Path, "out2.pdf"));
        var cfg = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfg.FullName, "{}");

        var session = new ProjectSession {
            SourceDir = src1,
            OutputPdf = out1,
            ConfigFile = cfg
        };

        // 用户在步骤 2 设定了若干项覆盖
        session.Configuration.TrySetOverride("LAYOUT_COLUMNS", "1");
        session.Configuration.TrySetOverride("CODE_FONT_SIZE", "8pt");
        session.Configuration.TrySetOverride("TITLE_CONTENT", "我的算法库");

        // 模拟返回步骤 1 (MODIFY_PATHS)，修改了源目录与输出路径
        session.SourceDir = src2;
        session.OutputPdf = out2;

        // 再次获取构建选项并验证
        var buildOptions = session.ToBuildOptions();
        Assert.Equal(src2.FullName, buildOptions.SourceDir.FullName);
        Assert.Equal(out2.FullName, buildOptions.OutputPdf.FullName);

        // 验证先前设定的覆盖项毫发无损
        var overrides = session.GetOverrides();
        Assert.Equal(3, overrides.Count);
        Assert.Equal("1", overrides["LAYOUT_COLUMNS"]);
        Assert.Equal("8pt", overrides["CODE_FONT_SIZE"]);
        Assert.Equal("我的算法库", overrides["TITLE_CONTENT"]);
    }

    // -------------------------------------------------------------------------
    // 场景 5: 保存后重启复现 (Save Session & Reopen Reproducibility)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario05_SaveSessionAndReopen_ReproducesIdenticalBuildAndConfig() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "test.cpp"), "// test");
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var savedConfigFile = new FileInfo(Path.Combine(tmp.Path, "saved_config.json"));

        var session = new ProjectSession {
            SourceDir = srcDir,
            OutputPdf = outPdf,
            ConfigFile = savedConfigFile
        };

        // 用户在交互会话中应用预设并微调了参数
        LayoutPresets.Reading.Apply(session.Configuration);
        session.Configuration.TrySetOverride("TITLE_CONTENT", "持久化测试手册");
        session.Configuration.TrySetOverride("CODE_MINTED_STYLE", "autumn");

        // 生成保存前的 TeX
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var builderBefore = new PdfBuilder(logger, session.ToBuildOptions(), session.Configuration, resMgr);
        var texBefore = builderBefore.GenerateTexContent_ForTest();

        // 执行保存写回磁盘
        session.Configuration.SaveToFile(savedConfigFile.FullName);
        Assert.True(savedConfigFile.Exists);

        // 模拟重启：启动新实例加载该 saved_config.json，且没有任何运行时 override
        var reopenedConfig = ProjectConfiguration.FromFile(savedConfigFile.FullName);
        Assert.Equal("持久化测试手册", reopenedConfig.GetEffectiveValue("TITLE_CONTENT"));
        Assert.Equal("1", reopenedConfig.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("10pt", reopenedConfig.GetEffectiveValue("CODE_FONT_SIZE"));
        Assert.Equal("autumn", reopenedConfig.GetEffectiveValue("CODE_MINTED_STYLE"));

        // 使用重启加载的配置构建 TeX
        var optionsAfter = new BuildSubcommandOptions(srcDir, outPdf, savedConfigFile, Verbose: false, TemplateDir: null);
        var builderAfter = new PdfBuilder(logger, optionsAfter, reopenedConfig, resMgr);
        var texAfter = builderAfter.GenerateTexContent_ForTest();

        // 验证：保存后重启加载生成的 TeX 产物与保存前 100% 一致！
        Assert.Equal(texBefore, texAfter);
    }

    // -------------------------------------------------------------------------
    // 场景 6: 失败保留旧 PDF (Failure Preserves Prior Output)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario06_FailurePreservesPriorPdf_AndIsolatesDiagnosticArtifacts() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "code.cpp"), "// code");
        var outPdfPath = Path.Combine(tmp.Path, "precious_release.pdf");
        const string oldPdfBytes = "%PDF-1.4 previous good version with signature";
        File.WriteAllText(outPdfPath, oldPdfBytes);

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var fakeRunner = new FakeXelatexRunner();

        // 模拟 XeLaTeX 编译失败
        fakeRunner.Results.Enqueue(new XelatexResult(1, "! Undefined control sequence.\nl.10 \\badmacro", false));
        fakeRunner.SideEffects.Enqueue((workingDir, _) => {
            // 在中间工作目录产生了损坏的临时 pdf
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 broken partial content");
        });

        var options = new BuildSubcommandOptions(
            srcDir,
            new FileInfo(outPdfPath),
            new FileInfo(Path.Combine(tmp.Path, "cfg.json")),
            Verbose: false,
            TemplateDir: null
        );
        File.WriteAllText(options.ConfigFile.FullName, "{}");

        var texParser = new ConfigParser("TEX", logger, ConfigStrictness.Lax);
        var progParser = new ConfigParser("PROGRAM", logger, ConfigStrictness.Lax);
        var builder = new PdfBuilder(logger, options, texParser, progParser, resMgr, fakeRunner);

        var exitCode = builder.Build();

        // 验证 6.1: 退出码为 XelatexFailure
        Assert.Equal(ExitCodes.XelatexFailure, exitCode);

        // 验证 6.2: 目标 PDF 完好无损，绝未被覆盖或截断
        Assert.True(File.Exists(outPdfPath));
        Assert.Equal(oldPdfBytes, File.ReadAllText(outPdfPath));

        // 验证 6.3: 损坏的半成品 PDF 被安全隔离在 .template_builder/build/ 供后续诊断排查
        var buildDir = Path.Combine(srcDir.FullName, ".template_builder", "build");
        var isolatedPdf = Path.Combine(buildDir, "mid-output.pdf");
        Assert.True(File.Exists(isolatedPdf), "损坏的半成品 PDF 必须被隔离在 buildDir 供诊断");
        Assert.Equal("%PDF-1.4 broken partial content", File.ReadAllText(isolatedPdf));
    }

    // -------------------------------------------------------------------------
    // 场景 7: 错误修复后重试 (Error Fix & Retry Flow)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario07_ErrorFixAndRetryFlow_SucceedsAfterCorrection() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var sourceFile = Path.Combine(srcDir.FullName, "algo.tex");
        // 初始写入有错误的原始 LaTeX 语句
        File.WriteAllText(sourceFile, @"\brokenMacroWithoutDefinition");

        var outPdf = new FileInfo(Path.Combine(tmp.Path, "final.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var fakeRunner = new FakeXelatexRunner();

        // 第一次编译：失败
        fakeRunner.Results.Enqueue(new XelatexResult(1, "! Undefined control sequence \\brokenMacroWithoutDefinition", false));

        var runner = new BuildPipelineRunner(logger, resMgr, fakeRunner);
        var options = new BuildSubcommandOptions(srcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);

        // 第一次执行构建：预期失败
        var result1 = runner.RunBuild(options, userProvidedConfig: false);
        Assert.False(result1.IsSuccess);
        Assert.Equal(ExitCodes.XelatexFailure, result1.ExitCode);
        Assert.False(outPdf.Exists);

        // 模拟用户排查错误并修复源码文件
        File.WriteAllText(sourceFile, @"\textbf{Corrected valid LaTeX content}");

        // 为第二次编译配置预期的成功结果与生成的 PDF 产物（含第 1 轮与第 2 轮编译）
        fakeRunner.Results.Enqueue(new XelatexResult(0, "", false));
        fakeRunner.Results.Enqueue(new XelatexResult(0, "", false));
        fakeRunner.SideEffects.Enqueue((workingDir, _) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 valid corrected pdf");
        });
        fakeRunner.SideEffects.Enqueue((workingDir, _) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 valid corrected pdf");
        });

        // 同一会话/环境下再次重试构建
        var result2 = runner.RunBuild(options, userProvidedConfig: false);
        Assert.True(result2.IsSuccess, $"result2 failed: {result2.ErrorMessage} (exit {result2.ExitCode})");
        Assert.Equal(ExitCodes.Success, result2.ExitCode);

        // 验证：重试后成功生成交付 PDF
        outPdf.Refresh();
        Assert.True(outPdf.Exists);
        Assert.Equal("%PDF-1.4 valid corrected pdf", File.ReadAllText(outPdf.FullName));
    }

    // -------------------------------------------------------------------------
    // 场景 8: 空输入无法伪成功 (Empty Input Rejection)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario08_EmptyInput_RejectsBuildAdmissionWithExitCode6() {
        using var tmp = TempDir.Create();
        var emptySrcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "empty_src"));
        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        // 8.1 针对绝对空目录发起构建
        var optionsEmpty = new BuildSubcommandOptions(emptySrcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);
        var resultEmpty = runner.RunBuild(optionsEmpty, userProvidedConfig: false);

        Assert.False(resultEmpty.IsSuccess);
        Assert.Equal(ExitCodes.ValidationFailed, resultEmpty.ExitCode);
        Assert.Contains("为空，未找到任何可排版的文件", resultEmpty.ErrorMessage);

        // 8.2 源目录有文件但全部被 ignore 过滤
        var filteredSrcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "filtered_src"));
        File.WriteAllText(Path.Combine(filteredSrcDir.FullName, "ignored.tmp"), "temp");
        File.WriteAllText(Path.Combine(filteredSrcDir.FullName, "backup.bak"), "bak");

        var optionsFiltered = new BuildSubcommandOptions(filteredSrcDir, outPdf, cfgFile, Verbose: false, TemplateDir: null);
        var resultFiltered = runner.RunBuild(optionsFiltered, userProvidedConfig: false);

        Assert.False(resultFiltered.IsSuccess);
        Assert.Equal(ExitCodes.ValidationFailed, resultFiltered.ExitCode);
        Assert.Contains("未匹配到任何有效源文件", resultFiltered.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // 场景 9: JSON 可直接解析 (Pure JSON Protocol Output)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario09_JsonProtocol_PureParseableJsonOutputForValidateAndInit() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "algo.cpp"), "int main(){}");
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();

        // 9.1 validate --format json 成功输出
        using var stdoutWriterSuccess = new StringWriter();
        var valRunnerSuccess = new ValidationRunner(logger, resMgr, stdoutWriterSuccess);
        var valExit = valRunnerSuccess.Run(new ValidateSubcommandOptions(srcDir, cfgFile, Format: "json"));
        Assert.Equal(ExitCodes.Success, valExit);

        var valJson = stdoutWriterSuccess.ToString().Trim();
        Assert.StartsWith("{", valJson);
        Assert.EndsWith("}", valJson);
        Assert.DoesNotContain("[Error]", valJson);
        Assert.DoesNotContain("[INFO]", valJson);

        using (var doc = JsonDocument.Parse(valJson)) {
            var root = doc.RootElement;
            Assert.True(root.GetProperty("overallOk").GetBoolean());
            Assert.Equal(0, root.GetProperty("errorCount").GetInt32());
            Assert.True(root.GetProperty("checks").GetArrayLength() > 0);
        }

        // 9.2 init --format json 生成的文件是严格的标准 JSON
        var initJsonTarget = Path.Combine(tmp.Path, "init_pure.json");
        var initRunner = new ConfigInitializer(logger, resMgr);
        var initExit = initRunner.Run(new InitSubcommandOptions(new FileInfo(initJsonTarget), Format: "json"));
        Assert.Equal(ExitCodes.Success, initExit);

        var fileContent = File.ReadAllText(initJsonTarget).Trim();
        Assert.StartsWith("{", fileContent);
        using (var doc = JsonDocument.Parse(fileContent)) {
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("TEX", out var texProp));
            Assert.True(root.TryGetProperty("PROGRAM", out var progProp));
        }
    }

    // -------------------------------------------------------------------------
    // 场景 10: 快速开始与脚本端到端可执行 (Quickstart & Scripts Validation)
    // -------------------------------------------------------------------------
    [Fact]
    public void Scenario10_QuickstartCliAndDocker_CompletePipeline() {
        using var tmp = TempDir.Create();
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        // 10.1 验证 init 执行
        var cfgPath = Path.Combine(tmp.Path, "quick_config.jsonc");
        var initCode = root.Parse(["init", "-o", cfgPath]).Invoke();
        Assert.Equal(0, initCode);
        Assert.True(File.Exists(cfgPath));

        // 10.2 验证 validate 执行
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "test.cpp"), "int main(){}");
        var validateCode = root.Parse(["validate", "-s", srcDir, "-c", cfgPath]).Invoke();
        Assert.Equal(0, validateCode);

        // 10.3 验证 docker-run.sh 语法正确性 (bash -n)
        var dockerScriptPath = Path.Combine(AppContext.BaseDirectory, "../../../../../scripts/docker-run.sh");
        if (File.Exists(dockerScriptPath) && !OperatingSystem.IsWindows()) {
            try {
                var psi = new ProcessStartInfo("bash", $"-n \"{dockerScriptPath}\"") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                if (proc != null) {
                    proc.WaitForExit();
                    Assert.Equal(0, proc.ExitCode);
                }
            } catch (System.ComponentModel.Win32Exception) {
                // Ignore if bash is not available on this platform
            }
        }
    }

    // -------------------------------------------------------------------------
    // 场景 11: 真实 XeLaTeX 端到端元数据与原子替换保护 (Requires Real XeLaTeX + ctex + pygments)
    // -------------------------------------------------------------------------
    [RequiresRealPdfCompilationFact]
    public void Scenario11_RealXelatex_EndToEnd_MetadataAndAtomicPreservation() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "algo_lib_2026"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "math.cpp"), "// math sample\nint gcd(int a, int b) { return b == 0 ? a : gcd(b, a % b); }");
        var outPdf = Path.Combine(tmp.Path, "verified.pdf");
        var cfgPath = Path.Combine(tmp.Path, "cfg.jsonc");

        // 1. 初始化纯净配置
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        var initRes = root.Parse(["init", "-o", cfgPath]).Invoke();
        Assert.Equal(0, initRes);

        // 2. 真实编译构建小样 (UX-14 预设 + 小样能力)
        var buildRes = root.Parse(["build", "-s", srcDir.FullName, "-o", outPdf, "-c", cfgPath, "--sample", "1"]).Invoke();
        Assert.Equal(0, buildRes);
        Assert.True(File.Exists(outPdf));

        var initialPdfBytes = File.ReadAllBytes(outPdf);
        Assert.NotEmpty(initialPdfBytes);

        // 3. 模拟第二次编译发生致命错误（插入无法编译的原生 LaTeX 宏）
        File.WriteAllText(Path.Combine(srcDir.FullName, "bad.tex"), @"\thisMacroDefDoesNotExistAtAllAndMustFailXeLaTeXCompilation");
        var failBuildRes = root.Parse(["build", "-s", srcDir.FullName, "-o", outPdf, "-c", cfgPath]).Invoke();

        // 验证：编译失败返回 1，且原有 PDF 逐字节完好保留
        Assert.Equal(ExitCodes.XelatexFailure, failBuildRes);
        Assert.True(File.Exists(outPdf));
        var preservedBytes = File.ReadAllBytes(outPdf);
        Assert.Equal(initialPdfBytes, preservedBytes);
    }
}
