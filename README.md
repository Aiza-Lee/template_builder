# ACM 代码模板PDF构建器

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/Aiza-Lee/template_builder) [![LaTeX](https://img.shields.io/badge/LaTeX-XeLaTeX-orange.svg)](https://www.latex-project.org/)

## 介绍

LaTeX 生成工具，将算法代码模板编译成PDF文档。

软件使用 C# 编写。提供了命令行工具 `template_builder`。有丰富的可配置项。

**[配置文件详解](docs/config_structure.md)**

---

## 快速开始

### 安装
1. 克隆仓库：

```bash
git clone https://github.com/Aiza-Lee/template_builder.git
cd template_builder
```
2. 构建项目：

```bash
bash ./build.sh
```
### 使用

在构建好的 `publish` 目录下找到可执行文件 `template_builder`（Linux/macOS）或 `template_builder.exe`（Windows）。

工具提供三个子命令（必须显式指定）：

#### `build` —— 编译源目录为 PDF

```bash
./template_builder build -s "path/to/your/code/templates/folder" -o "output/file.pdf" [-c "path/to/config.json"] [-t "path/to/template/dir"] [-v]
```

#### `validate` —— 校验源 / 配置 / 模板完整性（不调 xelatex）

CI 门禁友好，秒级返回。

```bash
./template_builder validate -s "path/to/src" -c "path/to/config.json" [--format text|json] [--check-xelatex]
```

输出示例（text 格式）：
```
[OK]    source.exists  /path/to/src
[OK]    config.parses  /path/to/config.json
[OK]    resources.Main.tex  2584 chars
[OK]    resources.CodeBlock.tex  50 chars
[OK]    source.walk  max depth = 1
[OK]    source.depth  1 ≤ 4
[OK]    placeholders.##KEY##  25 placeholders all resolve
Summary: 0 error(s), 0 warning(s)
```

#### `init` —— 生成带注释的默认配置骨架

```bash
./template_builder init -o "template_builder.config.jsonc" [--format jsonc|json]
```

- `jsonc`（默认）：带 inline `//` 注释，可直接编辑
- `json`：纯 JSON，无注释（适合程序化处理）

首次使用建议从 `init` 起步：跑一遍后编辑生成的 `*.jsonc` 再传给 `build -c`。

### 退出码

| 退出码 | 含义 |
|:---:|---|
| 0 | 成功 |
| 1 | `xelatex` 编译失败 |
| 2 | 命令行参数错误（缺源目录、输出路径无效、严格模式下未注册的配置 key 等） |
| 3 | 模板中存在未替换的占位符（`##KEY##` 或 `<<KEY>>`） |
| 4 | 用户配置文件 JSON 损坏或解析时发生意外异常 |
| 5 | 嵌入式资源缺失（理论上不会发生在发行包中，属编译期错误） |
| 6 | `validate` 业务校验失败（如源目录无文件、章节层级超限等，不属上述分类） |

---

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

---

**如果这个项目对你有帮助，不妨给一个 Star⭐ 支持一下！**
