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

在当前目录运行：

```bash
./template_builder build
```
pdf文件将生成在 `output` 目录下。

---

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

---

**如果这个项目对你有帮助，不妨给一个 Star⭐ 支持一下！**
