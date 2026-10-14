# ACM 代码模板 PDF 构建器

[![Version](https://img.shields.io/badge/version-2.0.0--rc.1-blue.svg)](https://github.com/Aiza-Lee/template_builder) [![LaTeX](https://img.shields.io/badge/LaTeX-XeLaTeX-orange.svg)](https://www.latex-project.org/) [![Build and Publish](https://github.com/Aiza-Lee/template_builder/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/Aiza-Lee/template_builder/actions/workflows/build-and-release.yml) [![Docker](https://img.shields.io/badge/Docker-ghcr.io-blue.svg?logo=docker)](https://github.com/Aiza-Lee/template_builder/pkgs/container/template_builder)

将算法源码模板整理、排版并编译为 PDF 文档的命令行工具，支持代码高亮、多级目录、独立排版预设与终端交互模式。

**[配置文件详解](docs/config_structure.md)** · **[CI 与发布流程](docs/ci.md)**

---

## 快速开始

### 方式一：Docker 运行（推荐，免配环境）

镜像内置完整 XeLaTeX、Pygments 代码高亮引擎与中西文字体，无需在宿主机安装 TeX Live 或 .NET SDK。

#### 便捷脚本

```bash
# 1. 生成默认配置文件
./scripts/docker-run.sh init -o config.jsonc

# 2. 校验配置与源目录
./scripts/docker-run.sh validate -s ./my_templates -c config.jsonc

# 3. 编译 PDF
./scripts/docker-run.sh build -s ./my_templates -c config.jsonc -o ./output.pdf

# 4. 交互式 TUI 模式
./scripts/docker-run.sh build --tui
```

> **提示**：若拉取 GHCR 镜像受网络限制，可配置本地代理（如 `export https_proxy=http://127.0.0.1:7890 http_proxy=http://127.0.0.1:7890`）或使用环境变量覆盖镜像：`TEMPLATE_BUILDER_IMAGE=<镜像地址> ./scripts/docker-run.sh ...`。

#### 原生 Docker 命令

```bash
# 非交互编译
docker run --rm -u "$(id -u):$(id -g)" -v "$(pwd)":/workspace ghcr.io/aiza-lee/template_builder:latest \
    build -s ./my_templates -c config.jsonc -o ./output.pdf

# 交互式 TUI 模式（需 -it 伪终端）
docker run --rm -it -u "$(id -u):$(id -g)" -v "$(pwd)":/workspace ghcr.io/aiza-lee/template_builder:latest \
    build --tui
```

#### 本地字体扩展
将任意 `.ttf` / `.otf` 字体放入源码目录的 `fonts/` 或当前工作目录的 `./fonts/`，引擎将自动识别加载。

---

### 方式二：本地原生构建

#### 环境要求
- [.NET 9.0 SDK（9.0.3xx，与 global.json 一致）](https://dotnet.microsoft.com/download)
- [TeX Live](https://www.tug.org/texlive/)（包含 `xelatex` 与 `ctexart` 宏包）
- [Pygments](https://pygments.org/)（`pygmentize` 命令行工具）
- 字体：[Fira Code](https://github.com/tonsky/FiraCode)

#### 构建打包
```bash
git clone https://github.com/Aiza-Lee/template_builder.git
cd template_builder
bash ./scripts/build.sh
```
构建产物位于 `publish/template_builder-<平台>/` 目录下（`template_builder` 或 `template_builder.exe`），归档包与 `.sha256` 校验文件位于 `publish/`。GitHub Release 的 tar.gz / Zip 包均包含 `template_builder-<平台>/` 顶层目录；解压后进入该目录运行程序。

---

## 命令指南

工具提供三个子命令：`build`、`validate` 与 `init`。

### 1. `build` —— 编译生成 PDF

```bash
./template_builder build -s <源目录> -o <输出.pdf> [选项]
```

常用选项：
- `-c, --config <file>`：指定配置文件路径（默认使用用户配置或内置默认值）。
- `--preset <competition|reading|compact>`：快速应用排版预设：
  - `competition`（比赛打印）：横向 A4、双栏紧凑排版、6pt 代码字号；
  - `reading`（屏幕阅读）：纵向 A4、单栏、9pt 护眼字号、现代段间距；
  - `compact`（紧凑速查）：纵向 A4、双栏、7pt 代码字号、独立单栏目录。
- `--sample [COUNT]`：小样预览。仅抽取前 COUNT 个源文件（默认 3 个）快速编译，用于检验排版与字体。
- `-t, --template-dir <dir>`：指定外部模板目录（可覆盖 `Main.tex` 或 `CodeBlock.tex`）。
- `-v, --verbose`：输出详细调试日志。
- `--tui`：进入全屏交互模式。

#### TUI 交互模式快捷键
在终端中运行 `./template_builder build --tui` 进入交互界面：
- `Space` / `→` / `l`：进入子菜单或编辑当前项
- `Enter`：确认配置并开始构建
- `←` / `Esc` / `h`：返回上一级或取消
- `↑` / `↓` / `j` / `k`：上下移动光标（支持 Vim `hjkl`）
- `Del`：清空取值或重置为默认值

> **注意**：`--tui` 需要交互式终端（TTY）。CI、脚本管道或输出重定向环境下请使用非交互命令行模式。

---

### 2. `validate` —— 快速完整性校验

无需调用 `xelatex`，在秒级内校验源文件、配置语法与模板完整性，适合作为 CI 门禁：

```bash
./template_builder validate -s <源目录> -c <配置文件> [--format text|json] [--check-xelatex]
```

- `--format text|json`：输出人类可读文本（默认）或结构化 JSON；
- `--check-xelatex`：一并检查 `xelatex` 与 `pygmentize` 是否存在于系统 PATH。

---

### 3. `init` —— 生成默认配置与 Schema

```bash
./template_builder init -o template_builder.config.jsonc [--format jsonc|json] [--force]
```

- `-o, --output <file>`：目标配置文件路径；
- `--format jsonc|json`：默认为带注释的 `jsonc`，亦可选纯 `json`；
- `-f, --force`：覆盖已存在的文件（会自动创建 `.bak` 备份）。

`init` 执行后会在同级目录下交付 `DefaultConfig.schema.json`，在 VS Code、Rider 等编辑器中可自动激活语法补全与字段悬浮说明。

---

### 多语言支持 (`--lang / -l`)

全局支持中文与英文界面切换：

```bash
# 查看英文帮助与构建
./template_builder --lang en-US --help
./template_builder build --lang en-US -s ./src -o ./out.pdf
```
- 可选值：`zh-CN`（默认）、`en-US`；
- TUI 模式下亦可在菜单中随时切换语言。

---

## 退出码

| 退出码 | 含义 |
|:---:|---|
| 0 | 成功 |
| 1 | `xelatex` 编译失败 |
| 2 | 命令行参数或配置校验错误 |
| 3 | 模板存在未替换的占位符 |
| 4 | 配置文件 JSON 损坏 |
| 5 | 内嵌资源缺失 |
| 6 | 业务校验失败（如源码目录为空、层级超限等） |

---

## 核心特性与安全保障

- **三层配置机制**：内置默认 (Builtin) -> 项目配置 (Project) -> 运行时覆盖 (Override)，未指定项自动采用安全默认值；
- **配置持久化与注释保留**：保存配置时采用增量更新，完整保留原有的 `//` 与 `/* */` 注释，自动创建 `.bak` 备份与配套 Schema，写入采用原子替换防损坏；
- **构建文件绝对隔离**：编译中间文件统一存放于 `<源目录>/.template_builder/build/`，构建失败保留旧 PDF 产物与排查日志，绝不污染或误删源目录文件；
- **LaTeX 沙箱防护**：源码中的 `\end{minted}` 等环境关键字自动通过零宽空格隔离，默认禁用 `shell-escape`，自动拦截并排除软链接死循环与外部软链接；
- **自适应终端渲染**：动态感知终端宽高，24 行终端自适应视口滚动，方括号特殊路径（如 `[audit].json`）安全转义渲染。

---

## 平台支持

CI 在 Linux、Windows、macOS 上运行自动化测试，并在五个对应架构的原生 runner 上校验发布包的 SHA-256、解包目录、CLI 版本与配置校验协议。Linux 测试及两种架构的 Docker 镜像另行验证真实 PDF 生成：
- **Linux (x64 / arm64)**：提供自包含单文件二进制与 Docker 镜像；
- **Windows (x64)**：提供独立 Zip 绿色包，兼容 Windows Terminal 与 PowerShell；
- **macOS (x64 / arm64)**：提供独立 tar.gz 归档包。

---

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。
