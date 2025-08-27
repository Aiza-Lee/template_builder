# 字体大小配置指南

## 支持的字体大小格式

### 1. 数字格式 (推荐)
直接使用数字值，系统会自动处理单位：

```json
{
  "code_style": {
    "font_size": "9pt",     // 9磅字体
    "font_size": "10.5pt",  // 10.5磅字体
    "font_size": "8",       // 8磅字体 (自动添加pt单位)
  }
}
```

**优势：**
- 精确控制字体大小
- 可以使用小数点（如10.5pt）
- 更直观的大小控制

### 2. LaTeX预定义大小
使用LaTeX标准大小命令：

```json
{
  "code_style": {
    "font_size": "tiny",        // 约5pt
    "font_size": "scriptsize",  // 约7pt
    "font_size": "footnotesize", // 约8pt
    "font_size": "small",       // 约9pt
    "font_size": "normalsize",  // 约10pt
    "font_size": "large",       // 约12pt
    "font_size": "Large",       // 约14.4pt
    "font_size": "LARGE",       // 约17.28pt
    "font_size": "huge",        // 约20.74pt
    "font_size": "Huge"         // 约24.88pt
  }
}
```

## 字体大小对照表

| 配置值 | 实际大小 | 适用场景 |
|--------|----------|----------|
| "6pt"  | 6磅      | 极小代码块 |
| "7pt"  | 7磅      | 密集代码 |
| "8pt"  | 8磅      | 小型代码 |
| "9pt"  | 9磅      | 标准代码 (推荐) |
| "10pt" | 10磅     | 中等代码 |
| "11pt" | 11磅     | 大号代码 |
| "12pt" | 12磅     | 演示用代码 |

## 技术实现

### 数字格式实现
```latex
% 数字格式会生成如下LaTeX代码：
\newcommand{\firatextstyle}{\firafont\fontsize{9pt}{1.2\baselineskip}\selectfont}
```

### 预定义格式实现
```latex
% 预定义格式会生成如下LaTeX代码：
\newcommand{\firatextstyle}{\firafont\small}
```

## 使用建议

1. **横版双列布局**：建议使用 8pt-10pt
2. **竖版单列布局**：建议使用 9pt-12pt
3. **打印输出**：建议使用 8pt-10pt
4. **屏幕查看**：建议使用 9pt-11pt

## 配置示例

### 紧凑型代码布局
```json
{
  "code_style": {
    "font_size": "8pt",
    "line_spacing": "0.5",
    "above_skip": "0.3em",
    "below_skip": "0.3em"
  }
}
```

### 舒适型代码布局
```json
{
  "code_style": {
    "font_size": "10pt",
    "line_spacing": "0.8",
    "above_skip": "0.6em",
    "below_skip": "0.6em"
  }
}
```

### 演示型代码布局
```json
{
  "code_style": {
    "font_size": "12pt",
    "line_spacing": "1.0",
    "above_skip": "0.8em",
    "below_skip": "0.8em"
  }
}
```
