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
./template_builder build -s "path/to/your/code/templates/folder" -o "output/file.pdf" [-c "path/to/config.json"] [--preset competition|reading|compact] [--sample [COUNT]] [-t "path/to/template/dir"] [-v]
```

核心构建选项：
- `--preset <competition|reading|compact>`：直接套用排版预设（比赛打印 / 屏幕阅读 / 紧凑速查），快速切换纸张方向、栏数、字号与行距；
- `--sample [COUNT]`：排版小样预览，仅抽取前 COUNT 个源文件（默认 2 个）极速编译 1~2 页 PDF，秒级验证中文字体、超长行折行与版面效果；
- 默认 PDF 无任何开发者个人署名或占位说明，文档主标题未指定时自动从源码目录名合理推导并转义；独立封面与目录均具备独立开关且不产生空白页。

##### 交互模式 `--tui`

加 `--tui` 进入全屏交互模式，适合不想记参数、或想直观调试排版与即时预览：

```bash
./template_builder build --tui
```

流程基于「参数输入 → 导航控制中心 → 实时构建与日志 → 结果操作中心」闭环状态机：

- `-s` / `-o` 在 `--tui` 下可省略，改为在界面中询问（仍可传入作为默认值）；
- 导航中心支持：直接构建、编译排版小样（`--sample`）、应用排版预设（`--preset`）、预览收录文件与章节树、调整核心常用排版配置、搜索并修改全量 90+ 项高级配置、保存修改至配置文件；
- 构建结束后可在结果中心查看产物元数据、打开系统阅读器查看 PDF，或直接返回修改配置继续编译。

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

### 配置优先级与关键保障 (UX-15)

- **三层配置模型**：内置默认 (Builtin) -> 项目配置 (Project) -> 运行时覆盖 (Runtime Override)；
- **纯净元数据**：默认产物不带任何开发者个人署名或文档主题占位符；标题为空时自动从源目录名推导；封面与目录独立开关，全组合 0 空白页；
- **产物与用户文件保护**：编译中间文件严格限制于 `<sourceDir>/.template_builder/build/`，绝不污染源目录，绝不删除用户在 `sourceDir/build` 的自建文件；编译失败完好保留旧 PDF 产物，并将损坏半成品隔离保留为诊断文件；
- **错误诊断与重试闭环**：精确反查 `! LaTeX Error`、行号与问题源文件；TUI 支持错误焦点自动重定向与原地修复后重新编译；
- **标准 JSON 协议**：`validate --format json` 与 `init --format json` 在 stdout 输出严格合法的纯 JSON 报告（诊断日志自动隔离至 stderr），可直接供 CI / 脚本 / `jq` 解析。

### 跨终端适配与平台验证记录

- **终端尺寸全覆盖**：经无头终端引擎与单测验证，覆盖极窄屏（35~40 列）、窄屏（50~60 列）、基准（80 列）及宽屏（120~160 列），Banner 自动降级、面板自适应收缩、无溢出崩溃；
- **全角与 CJK 编辑**：TUI 行编辑器完整支持 CJK 全角字符宽度计算（2 字符单元）与中英混排光标自由移动、回删及清空；
- **验证平台说明**：
  - ✔ **Linux (x86_64 Ubuntu / Debian) 与 Docker**：已完成全量自动化测试（453 项测试全通过）及真实 XeLaTeX / Pygments / 字体端到端实测验证；
  - ℹ **Windows (conhost / Windows Terminal) 与 macOS (Terminal.app / iTerm2)**：底层键盘与 ANSI 渲染依托 Spectre.Console 跨平台层支持，已保证逻辑路径与异常防护，平台原生按键细节若有差异以真机表现为准。

---

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

---

**如果这个项目对你有帮助，不妨给一个 Star⭐ 支持一下！**
