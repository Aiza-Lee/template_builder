# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

`template_builder` 是一个 C# 命令行工具，将算法/代码模板目录编译成 PDF 文档。它递归遍历源文件夹，按文件类型过滤并生成 LaTeX（minted 代码块），最后调用系统的 `xelatex` 编译（默认 2 pass，可通过 `PROGRAM.build.pass_count` 降到 1）生成最终的 PDF。

**版本与依赖**：`.NET 9.0`（`net9.0`）；运行时依赖系统的 `xelatex`（TeX Live）。核心 NuGet：`System.CommandLine 2.0.0-rc.1`、`Microsoft.Extensions.FileSystemGlobbing 9.0.0`。

## 构建与测试

```bash
# 开发用构建（推荐日常使用）
dotnet build template_builder.csproj

# 完整发布打包（脚本会自动检测平台并输出 tar.gz + sha256）
bash ./build.sh

# 跑测试（xUnit）
dotnet test tests/template_builder.Tests/template_builder.Tests.csproj

# 单个测试类
dotnet test --filter "FullyQualifiedName~CodeBlockGeneratorTests"

# 单个测试方法
dotnet test --filter "FullyQualifiedName=template_builder.Tests.CodeBlockGeneratorTests.Generate_HonorsIgnorePatterns_AsGlobs"
```

CI 流程见 `.github/workflows/build-and-release.yml`：PR 触发 restore/build/test；GitHub Release 发布时构建 linux-x64 / osx-x64 / win-x64 三个 self-contained 归档并上传。

## 架构

```
Program.Main
  └─ RootCommandFactory.CreateRootCommand()   // 顶层 RootCommand + 三个 subcommand
       ├─ build subcommand
       │    └─ BuildPipelineRunner.Run()       // 解析配置并调用 PdfBuilder
       │         └─ PdfBuilder.Build()        // 2 xelatex pass（可配 1）+ 清理
       ├─ validate subcommand
       │    └─ ValidationRunner.Run()         // 7 项检查，不调 xelatex
       └─ init subcommand
            └─ ConfigInitializer.Run()        // 复制嵌入式 DefaultConfig.jsonc 到 -o

无 subcommand → error: required command not specified → ExitCodes.InvalidArguments (2)
```

CLI 用法：
```bash
template_builder build    -s <src> -o <out.pdf> [-c <cfg>] [-t <template-dir>] [-v]
template_builder validate -s <src> -c <cfg>     [-t <template-dir>] [--format text|json] [--check-xelatex]
template_builder init     -o <path>             [--format jsonc|json]
```

### 关键模块

- **`src/Core/Pipeline/OutputPathResolver.cs`**：校验并规范化 `-s` / `-o`，建父目录、强制 `.pdf` 后缀；失败抛 `InvalidArgumentException`。
- **`src/Core/Pipeline/ConfigPathResolver.cs`**：处理 `-c` 路径回退与严格模式判定，返回 `(FileInfo, bool UserProvided)`。
- **`src/Core/Pipeline/BuildPipelineRunner.cs`**：读取 JSON、构造两个 `ConfigParser`、调用 `PdfBuilder.Build()`，并捕 `MalformedConfigException` / `UnknownConfigKeyException` 映射退出码。
- **`src/Core/Pipeline/ValidationRunner.cs`**：跑 7 项检查（源目录 / 配置解析 / 严格 key 白名单 / 嵌入式资源 / 源码树可走 + 章节深度 / `Main.tex` 的 `##KEY##` 解析 / 可选 xelatex PATH），不调 xelatex，产出 `ValidationReport`（text / json 两种格式）。
- **`src/Core/Pipeline/ConfigInitializer.cs`**：把嵌入式 `DefaultConfig.jsonc` 资源复制到 `-o` 路径。`--format json` 走 `JsonDocument` + `JsonCommentHandling.Skip` 剥除注释。
- **`src/Core/PdfBuilder.cs`**：总编排。`pass_count` 次 `xelatex` 编译（默认 2；让 `hyperref` 书签/目录稳定）；stderr 累积到 `_xelatexStderr`，仅在失败且未生成 PDF 时升为 Error 输出，避免 `minted` 无害提示刷屏。`Cleanup` 与 `SaveTexFile` 抽成 `internal static` 便于测试。Round 3b 后 ctor 接受可选 `IXelatexRunner`（默认 real `XelatexRunner`），便于测试注入 fake。
- **`src/Core/XelatexRunner.cs`**：Round 3b 抽象。`IXelatexRunner.Run(workingDir, arguments, timeoutSeconds)` → `XelatexResult(ExitCode, Stderr, TimedOut)`。真实实现用 `Process.Start` + `WaitForExit(timeoutMs)` + `Kill(entireProcessTree=true)` 防 hang。
- **`src/Core/CodeBlockGenerator.cs`**：通过 `SourceTreeWalker.Walk` 遍历源目录，按深度插入 `\section` → `\subsection` → `\subsubsection` → `\paragraph` → `\subparagraph`（深度由 `LAYOUT_SECTION_DEPTH` 截断，clamp [1, 5]，超出 clamp 到最后一层而非报错）。Round 3a 重构：24 条扩展名→语言默认映射从硬编码提取为 `_defaultExtMap`（static）+ 实例 `_languageMap`（合并 `PROGRAM.code_language_overrides` 增量覆盖）；未知名扩展退化为 `PlainText` 并 warn 一次（`_warnedExtensions` 防刷屏）。`LAYOUT_ESCAPE_SECTION_NAMES` toggle 控制章节名 LatexEscaper。
- **`src/Utils/LatexEscaper.cs`**：转义 LaTeX 11 个保留字符（`\` `{` `}` `#` `$` `%` `&` `_` `^` `~`），给用户提供的标题/作者/备注/章节名做安全处理。`CodeBlockGenerator` 与 `PdfBuilder` 共用。
- **`src/Utils/SourceTreeWalker.cs`**：深度优先遍历源码目录，吐出按 (目录优先 / 字母序) 排序的 `SourceEntry`（`Info` / `Depth` / `IsDirectory`）。隐藏项（以 `.` 开头）和 ignore glob 命中项被跳过。
- **`src/Utils/ConfigParser.cs`**：JSON 解析时只识别嵌入式 `DefaultConfig.jsonc` 注册过的 key（`isDefaultConfig=true` 时注册；`=false` 时只覆盖值）。路径展开为 `UPPER_SNAKE_CASE`。`IConfigParser["KEY"]` 返回 `ReadonlyConfigValue`。解析走 `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }`，天然支持 JSONC 与尾随逗号。
- **`src/Utils/ManifestResourceManager.cs`**：通过 `Assembly.GetManifestResourceStream` 读取嵌入资源。资源名在 csproj 中配置为 `LogicalName`。
- **`src/Utils/UserConfigPath.cs`**：跨平台用户配置目录（Windows: `%APPDATA%`，Linux: `~/.config`，macOS: `~/Library/Application Support`）。`ResolveBasePath` 抽成静态便于测试。

### 嵌入式资源（`Resources/`）

打包时通过 `<EmbeddedResource>` 编译进 DLL，对应 `LogicalName`：
- `DefaultConfig.jsonc` → `DefaultConfig.jsonc`（内联 `//` 注释，`init` 子命令直接复制此资源）
- `Resources/Templates/Main.tex` → `Templates.Main.tex`
- `Resources/Templates/CodeBlock.tex` → `Templates.CodeBlock.tex`

新增嵌入资源时记得在 `template_builder.csproj` 加 `<EmbeddedResource Include=...><LogicalName>...</LogicalName></EmbeddedResource>`。

### 配置与模板占位符

- JSON 配置分两个根对象：`TEX`（驱动 LaTeX 文档）和 `PROGRAM`（控制源码处理）。详见 `docs/config_structure.md`。
- 嵌套 key 在 `ConfigParser` 内被合并为大写下划线形式，如 `TEX.geometry.paper_size` → `GEOMETRY_PAPER_SIZE`。
- `Main.tex` 中的 `##KEY##` 占位符由 `ReplaceMainPlaceholders` 用 `TEX` 段替换。
- `<<CONTENT>>` / `<<MINTED_OUTPUTDIR>>` / `<<METADATA_KEYWORDS>>` / `<<DOC_CLASS_COLUMNS>>` / `<<LAYOUT_TOC_OPENING>>` / `<<LAYOUT_BODY_OPENING>>` / `<<CJK_FONT_BLOCK>>` / `<<TOC_DOT_LEADERS_LINE>>` / `<<TYPESETTING_PARSKIP_LINE>>` / `<<LANGUAGE>>` / `<<CODE>>` 是运行时替换符（大小写敏感的「双尖括号」），由 `PdfBuilder.GenerateTexContent` 在 `ReplaceMainPlaceholders` 之前单独替换。
- 新增 `TEX` 配置项时：① 在 `DefaultConfig.jsonc` 注册；② 在 `Main.tex` 引用 `##NEW_KEY##`（注意大小写）。
- `PROGRAM` 段的 `ignore_patterns` 使用 .NET glob（`Microsoft.Extensions.FileSystemGlobbing`），`Match(name).HasMatches=false` 表示被 exclude 命中。

### 测试约定

- 测试框架：xUnit 2.9.2，coverlet 收集覆盖率。
- `TestLogger`：实现 `ILogger` 并把消息入 `ConcurrentQueue`，供断言使用。
- 内部可见性已通过 `<InternalsVisibleTo Include="template_builder.Tests" />` 暴露给测试项目，便于断言 `internal static` 工具方法（如 `PdfBuilder.Cleanup`、`PdfBuilder.BuildXelatexArguments`、`XelatexRunner` 的 stderr 累积逻辑）。
- `tests/template_builder.Tests/Fixtures/FakeXelatexRunner.cs`（Round 3b）：记录所有 xelatex 调用的 fake runner，单测 PdfBuilder 的 xelatex 路径无需真的 spawn 进程。
- 测试用 `Path.GetTempPath() + Guid.NewGuid().ToString("N")` 建临时目录，`finally` 里 `Directory.Delete(..., recursive: true)`。

### 退出码与异常约定

退出码统一定义在 `src/Core/ExitCodes.cs`（`internal static class ExitCodes`）：

| 常量 | 值 | 触发场景 |
|---|:---:|---|
| `Success` | 0 | 构建成功 |
| `XelatexFailure` | 1 | `xelatex` 子进程返回非零 / 超时被 Kill / 启动失败 |
| `InvalidArguments` | 2 | CLI 参数错误或严格模式下 `UnknownConfigKeyException` |
| `UnresolvedPlaceholders` | 3 | 模板存在未替换的 `##KEY##` / `<<KEY>>` |
| `MalformedConfig` | 4 | 用户配置 JSON 损坏（`MalformedConfigException`） |
| `MissingEmbeddedResource` | 5 | 嵌入式资源缺失（`MissingEmbeddedResourceException`） |
| `ValidationFailed` | 6 | `validate` 子命令自身检查失败（如 xelatex PATH 不可达） |

新增异常类放在 `src/Utils/Exceptions/`。

## 代码风格

`.editorconfig` 强制 C# 文件使用 **tab** 缩进（不要替换为空格），K&R 风格大括号，`(int)x` 之类的转型后保留空格，控制流关键字后保留空格。

## 重要注意事项

- 项目没有 `dotnet format` 配置或 lint 工具——CI 只跑 `dotnet build` + `dotnet test`，靠 `.editorconfig` 保证风格。
- `obj/`、`bin/`、`publish/` 已 `.gitignore`，不要提交。
- 修改 `Resources/DefaultConfig.jsonc` 或 `Resources/Templates/*.tex` 后需要 `dotnet build`（嵌入式资源会被编译进 DLL），运行测试或重新发布才能生效。
- 不要绕过 `xelatex` 进程直接生成 PDF——所有 LaTeX 编译都通过 `Process` 子进程完成，错误信息依赖其 stderr。