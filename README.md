# ACM 算法竞赛模板 LaTeX/PDF 生成器

**项目完全由AI实现**

一个用于将 ACM 算法竞赛 C++ 模板代码自动生成为精美 PDF 文档的工具。采用横版双列布局，自动生成目录，高亮显示代码，适合打印和比赛使用。

## ✨ 主要特性

- **横版双列布局**: 专为比赛打印优化的紧凑布局
- **自动化处理**: 递归扫描目录结构，自动生成章节和目录
- **代码高亮**: 使用 LaTeX listings 包进行 C++ 语法高亮
- **中文支持**: 完美支持中文注释和文档
- **字体优化**: 针对代码阅读优化的字体配置
- **PDF 生成**: 一键生成高质量 PDF 文档
- **模块化设计**: 清晰的项目结构，易于维护和扩展

## 📁 项目结构

```text
template_builder/
├── main.py                 # 主入口脚本
├── config.json            # 配置文件
├── install.bat            # Windows 一键安装脚本
├── src/                   # C++ 模板代码目录
│   ├── Data Structure/    # 数据结构
│   ├── Graph Theory/      # 图论算法
│   ├── Math/              # 数学算法
│   ├── String/            # 字符串算法
│   ├── Tree Problems/     # 树相关问题
│   └── ...
├── scripts/               # 核心脚本
│   ├── generate_latex.py  # LaTeX 生成器
│   ├── config_manager.py  # 配置管理
│   ├── file_processor.py  # 文件处理
│   ├── latex_styler.py    # LaTeX 样式
│   └── build_manager.py   # 构建管理
├── templates/             # LaTeX 模板
│   └── landscape_template.tex  # 横版模板
├── build/                 # 构建临时文件
├── output/                # 输出 PDF 文件
└── docs/                  # 文档说明
```

## 🚀 快速开始

### Windows 用户（推荐）

1. **一键安装**: 双击运行 `install.bat`
   - 自动检查 Python 和 LaTeX 环境
   - 自动安装依赖（如果使用 Scoop）
   - 自动生成 PDF

2. **手动安装**:

   ```bash
   # 安装 Python
   scoop install python
   
   # 安装 LaTeX (推荐 MiKTeX)
   scoop install latex
   ```

### 其他系统

1. **安装依赖**:

   ```bash
   # Python 3.7+
   # XeLaTeX (TeX Live 或 MiKTeX)
   ```

2. **运行**:

   ```bash
   python main.py
   ```

## 💡 使用方法

### 基本使用

```bash
# 生成 PDF（默认横版布局）
python main.py build
```

### 高级使用

```bash
# 使用自定义配置文件
python scripts/generate_latex.py --config custom_config.json

# 自定义输出文件名
python scripts/generate_latex.py --output MyTemplates

# 指定模板目录
python scripts/generate_latex.py --template-dir my_templates
```

## ⚙️ 配置说明

编辑 `config.json` 文件来自定义生成效果：

```json
{
  "layout": "landscape",              // 固定为横版布局
  "template_dir": "src",              // 模板代码目录
  "output_filename": "ACM_Templates", // 输出文件名
  "clean_after_build": true,          // 构建后清理临时文件
  
  "fonts": {
    "main": "Times New Roman",        // 主字体
    "cjk_main": "SimHei",            // 中文主字体
    "code": "Fira Code"              // 代码字体
  },
  
  "colors": {
    "section": "blue!70!black",       // 章节颜色
    "code_keyword": "blue",           // 关键字颜色
    "code_comment": "green!50!black"  // 注释颜色
  }
}
```

### 字体配置

支持多种字体大小配置方式：

```json
{
  "code_style": {
    "font_size": "9pt",        // 精确磅值
    "font_size": "small",      // LaTeX 预定义大小
    "font_size": "8"           // 数字（自动添加pt）
  }
}
```

更多配置说明请参考 `docs/font_size_guide.md`

## 📝 模板组织

### 目录结构规范

- 使用清晰的目录层次结构
- 每个算法类别一个子目录
- 支持中英文目录名
- 自动生成对应的 LaTeX 章节

### 代码文件要求

- 支持的文件类型：`.cpp`, `.hpp`, `.h`, `.c`, `.cc`, `.cxx`
- 使用 UTF-8 编码
- 代码中的注释会被保留并高亮显示
- 文件名会作为子章节标题

## 🎨 样式特性

### 横版布局 (landscape)

- A4 横向双列排版
- 适合打印，信息密度高
- 优化的字体大小和行距
- 彩色章节标题
- 完整的目录结构包含所有文件

## 🔧 开发和扩展

### 模块说明

- `config_manager.py`: 配置文件管理
- `file_processor.py`: 文件扫描和处理
- `latex_styler.py`: LaTeX 样式应用
- `build_manager.py`: PDF 构建管理
- `generate_latex.py`: 主生成逻辑

### 自定义模板

可以修改 `templates/landscape_template.tex` 模板文件来自定义页面布局和样式。

## 📋 系统要求

- **Python**: 3.7 或更高版本
- **LaTeX**: XeLaTeX (TeX Live 或 MiKTeX)
- **字体**: 推荐安装 Fira Code 等编程字体
- **操作系统**: Windows/Linux/macOS

## 🐛 故障排除

### 常见问题

1. **编码错误**: 确保所有 C++ 文件使用 UTF-8 编码
2. **字体缺失**: 检查系统是否安装了配置文件中指定的字体
3. **LaTeX 错误**: 查看构建日志，检查 LaTeX 语法错误

### 获取帮助

- 查看 `docs/` 目录下的详细文档
- 检查控制台输出的错误信息
- 确保所有依赖正确安装

## 📄 许可证

本项目采用 MIT 许可证，详情请参阅 LICENSE 文件。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

**快速开始**: 运行 `install.bat` (Windows) 或 `python main.py` 开始使用！
