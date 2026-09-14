# ACM 代码模板PDF构建器

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/Aiza-Lee/template_builder) [![LaTeX](https://img.shields.io/badge/LaTeX-XeLaTeX-orange.svg)](https://www.latex-project.org/)

## 介绍

LaTeX 生成工具，将算法代码模板编译成PDF文档。

软件使用 C# 编写。提供了命令行工具 `template_builder`。有丰富的可配置项。

**[配置文件详解](docs/config_structure.md)**

---

## 快速开始

### 方式一：Docker 容器运行（推荐，开箱即用）

容器镜像内置完整现代 XeLaTeX 环境、Python Pygments 语法高亮引擎、Fira Code 编程字体以及中西文字体兼容层（Times New Roman、SimSun、SimHei、KaiTi、FangSong 等），**无需在宿主机安装任何 TeX Live 或 .NET SDK 环境**。

#### 1. 使用便捷脚本运行：

```bash
# 生成默认配置文件骨架
./scripts/docker-run.sh init -o config.jsonc

# 校验源目录与配置
./scripts/docker-run.sh validate -s ./my_templates -c config.jsonc

# 编译生成 PDF
./scripts/docker-run.sh build -s ./my_templates -c config.jsonc -o ./output.pdf
```

#### 2. 或直接使用原生 Docker 命令：

```bash
docker run --rm -u "$(id -u):$(id -g)" -v "$(pwd)":/workspace ghcr.io/aiza-lee/template_builder:latest \
    build -s ./my_templates -c config.jsonc -o ./output.pdf
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
