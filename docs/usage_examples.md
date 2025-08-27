# 使用示例

本文档提供了ACM模板生成器的详细使用示例。

## 基本使用流程

### 1. 准备模板文件

将您的C++算法模板文件放入`src/`目录，建议按算法类别组织：

```text
src/
├── Data Structure/
│   ├── Segment Tree/
│   │   └── BitTree.cpp
│   └── Heap/
│       └── PairingHeap.cpp
├── Graph Theory/
│   ├── ShortestPath/
│   │   ├── Dijkstra.cpp
│   │   └── SPFA.cpp
│   └── FlowNetwork/
│       └── Dinic.cpp
└── Math/
    └── Number Theory/
        └── EulerSieve.cpp
```

### 2. 配置生成选项

编辑`config.json`文件：

```json
{
  "layout": "landscape",
  "template_dir": "src",
  "output_filename": "MyTemplates",
  "fonts": {
    "code": "Consolas"
  }
}
```

### 3. 生成PDF

```bash
# 基本生成
python main.py

# 生成时选择布局
python main.py --layout portrait
```

## 高级配置示例

### 自定义字体配置

```json
{
  "fonts": {
    "main": "Times New Roman",
    "cjk_main": "Microsoft YaHei",
    "code": "Fira Code"
  },
  "code_style": {
    "font_size": "8pt"
  }
}
```

### 自定义颜色主题

```json
{
  "colors": {
    "section": "red!60!black",
    "subsection": "blue!60!black",
    "code_keyword": "purple",
    "code_comment": "gray!60"
  }
}
```

## 常见使用场景

### 场景1：快速生成比赛用模板手册

1. 使用横版布局（默认）
2. 小字体设置（如8pt或9pt）
3. 紧密的行距配置

### 场景2：制作教学材料

1. 使用竖版布局
2. 较大的字体（如11pt或12pt）
3. 添加更多说明性注释

### 场景3：个人学习笔记

1. 混合布局（根据内容选择）
2. 彩色标题和高亮
3. 详细的目录结构

## 故障排除

### 编译错误处理

如果遇到LaTeX编译错误：

1. 检查C++代码中的特殊字符
2. 确保文件编码为UTF-8
3. 查看build目录下的.log文件

### 字体问题

如果字体显示异常：

1. 确认系统已安装指定字体
2. 尝试使用系统默认字体
3. 检查中文字体配置

### 性能优化

对于大型模板集合：

1. 使用`clean_after_build: true`清理临时文件
2. 考虑分批生成不同模块
3. 适当调整字体大小和行距
