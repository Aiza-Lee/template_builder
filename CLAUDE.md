# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

`template_builder` 是一个 C# 命令行工具，将算法/代码模板目录编译成 PDF 文档。它递归遍历源文件夹，按文件类型过滤并生成 LaTeX（minted 代码块），最后调用系统的 `xelatex` 编译两次生成最终的 PDF。

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
  └─ BuildCommandFactory.CreateCommand()       // System.CommandLine 解析
       └─ Command.SetAction(...)               // 校验参数、装配组合根、解析配置并调用 PdfBuilder
            └─ PdfBuilder.Build()
                 ├─ LoadUserConfig()           // ConfigParser("TEX" + "PROGRAM")
                 ├─ GenerateTexContent()       // ManifestResourceManager 读 Main.tex 模板
                 │    ├─ ReplaceMainPlaceholders()   // ##KEY## 替换
                 │    └─ CodeBlockGenerator.Generate() // 递归源码目录、生成 <<CONTENT>>
                 ├─ SaveTexFile()              // 写出 mid-output.tex
                 └─ CompileTexToPdf()          // 两次 xelatex 进程调用 + 清理 .aux/.log/.toc 等
```

### 关键模块

- **`src/Core/PdfBuilder.cs`**：总编排。两次 `xelatex` 编译（让 `hyperref` 书签/目录稳定）；stderr 累积到 `_xelatexStderr`，仅在失败且未生成 PDF 时升为 Error 输出，避免 `minted` 无害提示刷屏。`Cleanup` 与 `SaveTexFile` 抽成 `internal static` 便于测试。
- **`src/Core/CodeBlockGenerator.cs`**：递归遍历源目录，按深度插入 `\section` → `\subsection` → `\subsubsection` → `\paragraph` → `\subparagraph`（最大 5 层）。`EXTENSION_TO_LANGUAGE` 映射到 minted 语言名，未知名扩展会退化为 `PlainText` 并 warn 一次（`_warnedExtensions` 防刷屏）。`LATEX_ESCAPES` 处理 11 个 LaTeX 特殊字符。
- **`src/Utils/ConfigParser.cs`**：JSON 解析时只识别嵌入式 `DefaultConfig.json` 注册过的 key（`isDefaultConfig=true` 时注册；`=false` 时只覆盖值）。路径展开为 `UPPER_SNAKE_CASE`。`IConfigParser["KEY"]` 返回 `ReadonlyConfigValue`。
- **`src/Utils/ManifestResourceManager.cs`**：通过 `Assembly.GetManifestResourceStream` 读取嵌入资源。资源名在 csproj 中配置为 `LogicalName`。
- **`src/Utils/UserConfigPath.cs`**：跨平台用户配置目录（Windows: `%APPDATA%`，Linux: `~/.config`，macOS: `~/Library/Application Support`）。`ResolveBasePath` 抽成静态便于测试。

### 嵌入式资源（`Resources/`）

打包时通过 `<EmbeddedResource>` 编译进 DLL，对应 `LogicalName`：
- `DefaultConfig.json` → `DefaultConfig.json`
- `Resources/Templates/Main.tex` → `Templates.Main.tex`
- `Resources/Templates/CodeBlock.tex` → `Templates.CodeBlock.tex`

新增嵌入资源时记得在 `template_builder.csproj` 加 `<EmbeddedResource Include=...><LogicalName>...</LogicalName></EmbeddedResource>`。

### 配置与模板占位符

- JSON 配置分两个根对象：`TEX`（驱动 LaTeX 文档）和 `PROGRAM`（控制源码处理）。详见 `docs/config_structure.md`。
- 嵌套 key 在 `ConfigParser` 内被合并为大写下划线形式，如 `TEX.geometry.paper_size` → `GEOMETRY_PAPER_SIZE`。
- `Main.tex` 中的 `##KEY##` 占位符由 `ReplaceMainPlaceholders` 用 `TEX` 段替换。
- `<<CONTENT>>` / `<<MINTED_OUTPUTDIR>>` / `<<LANGUAGE>>` / `<<CODE>>` 是运行时替换符（大小写敏感的「双尖括号」）。
- 新增 `TEX` 配置项时：① 在 `DefaultConfig.json` 注册；② 在 `Main.tex` 引用 `##NEW_KEY##`（注意大小写）。
- `PROGRAM` 段的 `ignore_patterns` 使用 .NET glob（`Microsoft.Extensions.FileSystemGlobbing`），`Match(name).HasMatches=false` 表示被 exclude 命中。

### 测试约定

- 测试框架：xUnit 2.9.2，coverlet 收集覆盖率。
- `TestLogger`：实现 `ILogger` 并把消息入 `ConcurrentQueue`，供断言使用。
- 内部可见性已通过 `<InternalsVisibleTo Include="template_builder.Tests" />` 暴露给测试项目，便于断言 `internal static` 工具方法（如 `PdfBuilder.Cleanup`、`CodeBlockGenerator.IsIgnored`）。
- 测试用 `Path.GetTempPath() + Guid.NewGuid().ToString("N")` 建临时目录，`finally` 里 `Directory.Delete(..., recursive: true)`。

### 退出码与异常约定

退出码统一定义在 `src/Core/ExitCodes.cs`（`internal static class ExitCodes`）：

| 常量 | 值 | 触发场景 |
|---|:---:|---|
| `Success` | 0 | 构建成功 |
| `XelatexFailure` | 1 | `xelatex` 子进程返回非零 |
| `InvalidArguments` | 2 | CLI 参数错误或严格模式下 `UnknownConfigKeyException` |
| `UnresolvedPlaceholders` | 3 | 模板存在未替换的 `##KEY##` / `<<KEY>>` |
| `MalformedConfig` | 4 | 用户配置 JSON 损坏（`MalformedConfigException`） |
| `MissingEmbeddedResource` | 5 | 嵌入式资源缺失（`MissingEmbeddedResourceException`） |

`PdfBuilder` 仍保留 `ExitSuccess` / `ExitXelatexFailure` / `ExitUnresolvedPlaceholders` 旧名作为转发常量，避免破坏外部脚本期望。新增异常类放在 `src/Utils/Exceptions/`。

## 代码风格

`.editorconfig` 强制 C# 文件使用 **tab** 缩进（不要替换为空格），K&R 风格大括号，`(int)x` 之类的转型后保留空格，控制流关键字后保留空格。

## 重要注意事项

- 项目没有 `dotnet format` 配置或 lint 工具——CI 只跑 `dotnet build` + `dotnet test`，靠 `.editorconfig` 保证风格。
- `obj/`、`bin/`、`publish/` 已 `.gitignore`，不要提交。
- 修改 `Resources/DefaultConfig.json` 或 `Resources/Templates/*.tex` 后需要 `dotnet build`（嵌入式资源会被编译进 DLL），运行测试或重新发布才能生效。
- 不要绕过 `xelatex` 进程直接生成 PDF——所有 LaTeX 编译都通过 `Process` 子进程完成，错误信息依赖其 stderr。