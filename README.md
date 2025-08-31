# ACM 算法竞赛模板构建器

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)](https://github.com/Aiza-Lee/template_builder) [![Python](https://img.shields.io/badge/python-3.7+-green.svg)](https://www.python.org/) [![LaTeX](https://img.shields.io/badge/LaTeX-XeLaTeX-orange.svg)](https://www.latex-project.org/)

一个高效的 LaTeX 模板生成工具，专为 ACM 算法竞赛选手设计，将算法代码模板自动编译成pdf文档

- **[快速开始指南](docs/quick_start.md)** - 5分钟上手教程
- **[配置文件详解](docs/config_structure.md)** - 完整配置说明
- **[常见问题解答](docs/quick_start.md#常见问题)** - 故障排除指南

## 实用命令 文档


## 实用命令

| 命令 | 功能 |
|------|------|
| `python main.py build` | 构建PDF模板 |
| `python main.py status` | 查看项目状态 |
| `python main.py validate-config` | 验证配置文件 |

## 项目结构

```
template_builder/
├── main.py                 # 主程序入口
├── config.json             # 配置文件
├── src/                    # 算法代码目录
├── scripts/                # 核心处理脚本
├── templates/              # LaTeX模板
├── build/                  # 构建输出
└── output/                 # 最终PDF输出
```

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

---

**如果这个项目对你有帮助，请给一个 Star 支持一下！**
