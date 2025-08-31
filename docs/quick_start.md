# 快速开始指南

## 环境准备

### 必需组件
- **Python 3.7+**
- **XeLaTeX**（TeX Live 或 MiKTeX）

### 可选组件
- **Fira Code 字体**（提升代码显示效果）

## 5分钟上手

### 步骤 1：准备代码文件
将你的算法代码放入 `src` 目录：

```
src/
├── 数据结构/
│   └── 线段树.cpp
├── 图论/
│   └── 最短路径.cpp
└── 字符串/
    └── KMP.cpp
```

### 步骤 2：生成PDF
```bash
python main.py build
```

### 步骤 3：查看结果
生成的PDF文件位于 `output/ACM_Templates.pdf`

## 基础定制

### 调整代码字体大小
编辑 `config.json`：
```json
{
  "code_style": {
    "appearance": {
      "font_size": "6pt"
    }
  }
}
```

### 修改项目信息
```json
{
  "project": {
    "title": "我的算法模板",
    "author": "你的姓名"
  }
}
```

### 调整目录字体
```json
{
  "table_of_contents": {
    "structure": {
      "font_size": "\\small"
    }
  }
}
```

## 常用命令

| 命令 | 功能 |
|------|------|
| `python main.py build` | 构建PDF |
| `python main.py build --no-open` | 构建但不打开PDF |
| `python main.py status` | 查看项目状态 |
| `python main.py validate-config` | 验证配置文件 |

## 常见问题

### Q: PDF中文显示异常？
A: 确保系统已安装中文字体，如SimSun、SimHei等。

### Q: 代码显示太小？
A: 调整 `config.json` 中的 `code_style.appearance.font_size`。

### Q: 想要更紧凑的布局？
A: 减小 `code_style.formatting.line_spacing` 的值。

### Q: 如何添加新的代码文件？
A: 直接将 `.cpp` 文件放入 `src` 的任意子目录即可。

## 更多帮助

- **详细配置说明**：[config_structure.md](config_structure.md)
- **项目主页**：[GitHub](https://github.com/Aiza-Lee/template_builder)
