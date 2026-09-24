# ACM 代码模板PDF构建器

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/Aiza-Lee/template_builder) [![LaTeX](https://img.shields.io/badge/LaTeX-XeLaTeX-orange.svg)](https://www.latex-project.org/) [![Build and Publish](https://github.com/Aiza-Lee/template_builder/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/Aiza-Lee/template_builder/actions/workflows/build-and-release.yml) [![Docker](https://img.shields.io/badge/Docker-ghcr.io-blue.svg?logo=docker)](https://github.com/Aiza-Lee/template_builder/pkgs/container/template_builder)

## 介绍

LaTeX 生成工具，将算法代码模板编译成PDF文档。

软件使用 C# 编写。提供了命令行工具 `template_builder`。有丰富的可配置项。

**[配置文件详解](docs/config_structure.md)**

---

## 快速开始

### 方式一：Docker 容器运行（推荐，开箱即用）

容器镜像内置完整现代 XeLaTeX 环境、Python Pygments 语法高亮引擎、Fira Code 编程字体以及中西文字体兼容层（Times New Roman、SimSun、SimHei、KaiTi、FangSong 等），**无需在宿主机安装任何 TeX Live 或 .NET SDK 环境**。

#### 1. 使用便捷脚本运行：

便捷脚本 `./scripts/docker-run.sh` 默认直接使用 GHCR 发布的预构建镜像 `ghcr.io/aiza-lee/template_builder:latest`（若本地有自构建镜像亦会自动识别），且会根据终端 TTY 状态智能分配交互参数（TTY 环境自动支持 TUI 交互，CI/管道/重定向环境自动禁用 `-t`，杜绝非交互报错）：

```bash
# 生成默认配置文件骨架
./scripts/docker-run.sh init -o config.jsonc

# 校验源目录与配置（支持 text 与 json 协议）
./scripts/docker-run.sh validate -s ./my_templates -c config.jsonc

# 编译生成 PDF
./scripts/docker-run.sh build -s ./my_templates -c config.jsonc -o ./output.pdf

# 启动全屏交互模式（终端自动启用 TTY 交互）
./scripts/docker-run.sh build --tui
```

若需指定其他镜像或自构建镜像，可通过环境变量覆盖：`TEMPLATE_BUILDER_IMAGE=my_image:tag ./scripts/docker-run.sh ...`。

#### 2. 或直接使用原生 Docker 命令：

```bash
# 非交互命令行编译（管道与 CI 友好）：
docker run --rm -u "$(id -u):$(id -g)" -v "$(pwd)":/workspace ghcr.io/aiza-lee/template_builder:latest \
    build -s ./my_templates -c config.jsonc -o ./output.pdf

# 启动全屏交互模式（需显式分配伪终端 -it）：
docker run --rm -it -u "$(id -u):$(id -g)" -v "$(pwd)":/workspace ghcr.io/aiza-lee/template_builder:latest \
    build --tui
```

#### 3. 自定义字体支持（免安装）：
- 将任意 `.ttf` / `.otf` 字体直接放入源目录下的 `fonts/`（如 `./my_templates/fonts/`）或当前工作目录下的 `./fonts/`。
- 引擎将通过 XeTeX 原生 `OSFONTDIR` 自动检索识别，无需管理员权限，零门槛扩展。
- 也可以通过挂载卷或环境变量映射宿主机字体目录：
  ```bash
  TEMPLATE_BUILDER_FONTS_DIR=/usr/share/fonts ./scripts/docker-run.sh build -s ./my_templates -o ./output.pdf
  ```

---

### 方式二：本地原生构建与安装

#### 环境要求
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [TeX Live](https://www.tug.org/texlive/)（包含 `xelatex` 与 `ctexart` 宏包）
- [Pygments](https://pygments.org/)（`pygmentize` 命令行工具）
- 字体：[Fira Code](https://github.com/tonsky/FiraCode)

#### 构建与打包
```bash
git clone https://github.com/Aiza-Lee/template_builder.git
cd template_builder
bash ./scripts/build.sh
```

在构建好的 `publish` 目录下找到对应平台的可执行文件 `template_builder`（Linux/macOS）或 `template_builder.exe`（Windows）。

工具提供三个子命令（必须显式指定）：

#### `build` —— 编译源目录为 PDF

```bash
./template_builder build -s "path/to/your/code/templates/folder" -o "output/file.pdf" [-c "path/to/config.json"] [-t "path/to/template/dir"] [-v]
```

##### 交互模式 `--tui`

加 `--tui` 进入全屏交互模式，适合不想记参数、或想临时试几组排版参数的时候：

```bash
./template_builder build --tui
```

流程为「源目录 → 输出路径 → 常用配置项 → 实时构建日志」四步：

- `-s` / `-o` 在 `--tui` 下可省略，改为在界面中询问（仍可传入作为默认值）。
- 界面先列出 12 项常用配置的当前值，勾选需要临时改动的项后逐项询问；**改动只作用于本次构建，不写回配置文件**。
- 构建阶段日志实时滚动；构建结束后结果与 PDF 路径保留在屏幕上。

> 终端需要支持全屏渲染（ANSI / TTY）。在 CI、管道或重定向输出等非交互环境下，`--tui` 会直接报错并返回退出码 `2`（而非崩溃），此时请改用普通命令行模式。

#### `validate` —— 校验源 / 配置 / 模板完整性（不调 xelatex）

CI 门禁友好，秒级返回。

```bash
./template_builder validate -s "path/to/src" -c "path/to/config.json" [--format text|json] [--check-xelatex]
```

输出示例（text 格式）：
```
[通过] source.exists  /path/to/src
[通过] config.parses  /path/to/config.json
[通过] resources.Main.tex  2584 字符
[通过] resources.CodeBlock.tex  50 字符
[通过] source.walk  最大深度 = 1
[通过] source.depth  1 ≤ 4
[通过] placeholders.##KEY##  25 个占位符全部成功解析
汇总：0 项错误，0 项警告
```

#### `init` —— 生成带注释的默认配置骨架与 Schema

```bash
./template_builder init -o "template_builder.config.jsonc" [--format jsonc|json] [--force]
```

- `--format jsonc`（默认）：带 inline `//` 注释，可直接编辑
- `--format json`：纯 JSON，无注释（适合程序化处理）
- `--force / -f`：覆盖已存在的文件（自动创建 `.bak` 备份），默认拒绝覆盖以保护已有配置与 Schema

`init` 执行成功后会同步交付两项产物并输出后续操作引导：
1. **配置文件**：写入 `-o` 指定的目标路径；
2. **配套 Schema**：在配置文件同目录下同步交付 `DefaultConfig.schema.json`（配置文件中的 `"$schema": "./DefaultConfig.schema.json"` 自动解析生效，VS Code、Rider 等编辑器开箱即享自动补全、悬浮说明与实时语法校验）；
3. **精准命令引导**：终端提示的下一步 `validate` 和 `build` 命令会自动附加带引号转义的 `-c "<刚生成的配置文件路径>"`，复制即用，杜绝构建错跑默认配置。

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
