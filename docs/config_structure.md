# 配置文件结构说明

经过优化后的 `config.json` 配置文件采用了模块化的结构设计，按照功能板块清晰地组织配置项。

## 配置结构概览

```
config.json
├── _metadata          # 元数据信息
├── project            # 项目信息
├── build              # 构建设置
├── files              # 文件处理
├── page_layout        # 页面布局
├── typography         # 字体排版
├── code_style         # 代码样式
└── table_of_contents  # 目录配置
```

## 各模块详细说明

### `_metadata` - 元数据信息
存储配置文件的版本信息和描述。

```json
"_metadata": {
  "version": "2.0.0",
  "description": "ACM 算法竞赛模板构建配置文件"
}
```

### `project` - 项目信息
定义生成文档的基本信息。

```json
"project": {
  "title": "ACM 算法竞赛模板集合",
  "author": "Aiza", 
  "version": "2025"
}
```

### `build` - 构建设置
控制构建过程的核心参数。

```json
"build": {
  "layout": "landscape",                    // 页面布局：landscape/portrait
  "template_dir": "src",                    // 源码目录
  "output_filename": "ACM_Templates",       // 输出文件名
  "clean_after_build": true,               // 构建后清理临时文件
  "use_unified_template": true,            // 使用统一模板系统
  "unified_template_name": "unified_template.tex"
}
```

### `files` - 文件处理
定义支持的文件类型和排除模式。

```json
"files": {
  "supported_extensions": [".cpp", ".hpp", ".h", ".c", ".cc", ".cxx"],
  "exclude_patterns": ["*.tmp", "*.bak", "*~", "*.swp"]
}
```

### `page_layout` - 页面布局
控制页面边距和列间距。

```json
"page_layout": {
  "margins": {
    "landscape": {
      "left": "1.5cm",
      "right": "1.5cm", 
      "top": "2cm",
      "bottom": "2cm"
    }
  },
  "column_separation": "1cm"
}
```

### `typography` - 字体排版
定义字体和颜色设置。

```json
"typography": {
  "fonts": {
    "main": "Times New Roman",      // 主字体
    "cjk_main": "SimSun",          // 中文主字体
    "cjk_sans": "SimHei",          // 中文无衬线字体
    "cjk_mono": "SimSun",          // 中文等宽字体
    "code": "Fira Code"            // 代码字体
  },
  "colors": {
    "section": "blue!70!black",         // 一级标题颜色
    "subsection": "green!60!black",     // 二级标题颜色
    "subsubsection": "orange!80!black", // 三级标题颜色
    "code_background": "gray!5",        // 代码背景色
    "code_keyword": "blue",             // 代码关键字颜色
    "code_comment": "green!50!black",   // 代码注释颜色
    "code_string": "red",               // 代码字符串颜色
    "code_number": "gray"               // 代码行号颜色
  }
}
```

### `code_style` - 代码样式
分为外观、格式化和间距三个子模块。

#### 外观设置 (`appearance`)
```json
"appearance": {
  "font_size": "5pt",              // 字体大小
  "line_numbers": true,            // 显示行号
  "frame_style": "leftline",       // 边框样式
  "frame_round": true,             // 圆角边框
  "break_lines": true              // 自动换行
}
```

#### 格式化设置 (`formatting`)
控制代码的格式化和布局参数。

```json
"formatting": {
  "tab_size": 4,                   // 制表符大小：4个空格
  "columns": "flexible",           // 列对齐方式：flexible/fixed
  "base_width": "0.5em",          // 字符基础宽度
  "scale_factor": 0.85,           // 缩放因子：0.85倍缩放
  "line_spacing": "0.1"           // 行间距系数：额外行高 = 字体大小 × 行间距
}
```

**重要说明**：
- `line_spacing` 控制代码行间的紧密程度
- 计算公式：`实际行高 = 字体大小 + 字体大小 × line_spacing`
- 例如：字体5pt，行间距0.1，则行高 = 5pt + 5pt × 0.1 = 5.5pt
- 较小的值（如0.1）使代码更紧凑，较大的值（如0.5）使代码更疏松

#### 间距设置 (`spacing`)
```json
"spacing": {
  "left_margin": "0pt",           // 左边距
  "right_margin": "0pt",          // 右边距  
  "above_skip": "0.5em",          // 上间距
  "below_skip": "0.5em"           // 下间距
}
```

### `table_of_contents` - 目录配置
分为结构、样式和间距三个子模块。

#### 结构设置 (`structure`)
```json
"structure": {
  "depth": 3,                     // 目录深度
  "show_page_numbers": true,      // 显示页码
  "show_dots": true,              // 显示引导点
  "font_size": "\\tiny"           // 整体目录字体大小
}
```

#### 样式设置 (`styling`)
控制目录中各级标题的字体样式和缩进。

```json
"styling": {
  "fonts": {
    "section": "\\scriptsize\\bfseries",      // 一级标题字体：小字体+粗体
    "subsection": "\\tiny\\bfseries",         // 二级标题字体：极小字体+粗体
    "subsubsection": "\\tiny"                 // 三级标题字体：极小字体
  },
  "indent": {
    "section": "0em",                        // 一级标题缩进：无缩进
    "subsection": "1.5em",                   // 二级标题缩进：1.5倍字符宽度
    "subsubsection": "3em"                   // 三级标题缩进：3倍字符宽度
  }
}
```

**字体大小说明**：

- `\scriptsize`：约为正常字体的70%大小
- `\tiny`：最小的LaTeX字体大小，约为正常字体的50%大小
- `\bfseries`：粗体样式，使标题更突出

**缩进层次说明**：

- 一级标题：顶格显示，无缩进
- 二级标题：缩进1.5em，表示层次关系
- 三级标题：缩进3em，形成清晰的层次结构


#### 间距设置 (`spacing`)
```json
"spacing": {
  "before_section": "0.2pt",      // 一级标题前间距
  "before_subsection": "0.1pt",   // 二级标题前间距
  "before_subsubsection": "0pt",  // 三级标题前间距
  "before_toc": "-1.5cm",         // 目录前间距
  "after_toc": "-1.2cm"           // 目录后间距
}
```

### `layout` - 布局兼容性设置
为了向后兼容而保留的顶层布局设置。

```json
"layout": "landscape"                   // 页面布局模式（推荐使用 build.layout）
```

**注意**：此配置项主要用于向后兼容，建议优先使用 `build.layout` 配置。

## 重点配置项说明

### 目录字体样式配置

目录提供两种字体大小控制方式：

#### 整体目录字体大小控制
通过 `structure.font_size` 设置整个目录的基础字体大小：

```json
"structure": {
  "font_size": "\\tiny"           // 控制整个目录的字体大小
}
```

这个设置会应用到整个目录显示，包括所有标题和页码。

#### 分级目录字体样式控制  
通过 `styling.fonts` 为不同级别的目录条目设置个性化字体样式：

```json
"fonts": {
  "section": "\\scriptsize\\bfseries",      // 一级标题：小字体+粗体
  "subsection": "\\tiny\\bfseries",         // 二级标题：极小字体+粗体  
  "subsubsection": "\\tiny"                 // 三级标题：极小字体
}
```

**推荐配置策略**：
- 使用 `structure.font_size` 设置目录的整体大小（如 `\\tiny` 节省空间）
- 使用 `styling.fonts` 在整体基础上调整各级标题的样式差异

**可选的LaTeX字体大小**：
- `\tiny`：最小字体（约50%正常大小）
- `\scriptsize`：脚本字体（约70%正常大小）
- `\footnotesize`：脚注字体（约80%正常大小）
- `\small`：小字体（约90%正常大小）
- `\normalsize`：正常字体（100%）
- `\large`：大字体（约120%正常大小）

**字体样式选项**：
- `\bfseries`：粗体
- `\mdseries`：中等粗细（默认）
- `\itshape`：斜体
- `\upshape`：正体（默认）

### 代码行间距优化
行间距配置直接影响代码的可读性和页面利用率：

```json
"line_spacing": "0.1"    // 紧凑型布局，节省空间
"line_spacing": "0.3"    // 标准布局，平衡可读性和空间
"line_spacing": "0.5"    // 宽松布局，提高可读性
```

## 设计优势

### 模块化设计
- 按功能分组，逻辑清晰
- 易于理解和维护
- 减少配置项冲突

### 语义化命名
- 配置项名称明确表达用途
- 减少歧义和混淆
- 提高可读性

### 灵活性
- 支持细粒度控制
- 易于扩展新功能
- 向后兼容性好

### 维护性
- 结构化的组织方式
- 便于批量修改
- 减少重复配置

## 使用建议

1. **修改单个配置项**：定位到对应模块，修改具体配置
2. **批量调整样式**：在对应的样式模块中统一修改
3. **添加新功能**：在合适的模块中添加新的配置项
4. **性能调优**：重点关注 `code_style` 和 `page_layout` 模块

## 配置验证

使用以下命令验证配置文件的正确性和生效情况：

```bash
# 验证配置格式和生效性
python main.py validate-config

# 查看项目状态
python main.py status

# 分析配置使用情况  
python main.py analyze-config
```

**验证输出示例**：
```text
✅ 配置验证通过
✅ 配置格式验证通过

[项目信息模块验证]
  [OK] 文档标题: ACM 算法竞赛模板集合
  [OK] 作者信息: Aiza

[字体排版模块验证]
  [OK] 主字体: Times New Roman
  [OK] 代码字体: Fira Code
  [OK] CJK字体配置完整

[代码样式模块验证]
  [OK] 代码字体大小: 5pt
  [OK] 代码行间距: 0.1 (行高: 5.5pt)
  [OK] 代码边距设置: 0pt

[目录配置模块验证]
  [OK] 目录深度: 3级
  [OK] 目录整体字体: \tiny
  [OK] 各级标题样式配置完整

[页面布局模块验证]
  [OK] 页面模式: landscape (横向)
  [OK] 页边距配置: 左右0.8cm, 上下1.5cm
  [OK] 分栏间距: 1cm

验证结果: 12/12 项通过
所有配置都已正确生效！
```

**配置修改后务必运行验证命令**，确保所有设置都正确应用到生成的PDF中。

## 故障排除

### 常见配置问题

#### 字体问题
**现象**: PDF中字体显示异常或乱码
**解决方案**:
1. 确认系统已安装配置中指定的字体
2. 检查字体名称拼写是否正确
3. 对于中文字体，确保使用正确的字体名称

#### 行间距问题
**现象**: 代码显示过于紧密或稀疏
**解决方案**:
1. 调整 `code_style.formatting.line_spacing` 值
2. 较小值（0.1）更紧密，较大值（0.5）更宽松
3. 建议范围：0.05 - 0.8

#### 目录显示问题
**现象**: 目录字体过大或过小
**解决方案**:
1. 调整 `table_of_contents.structure.font_size`
2. 可选值：`\\tiny`, `\\scriptsize`, `\\footnotesize`, `\\small`, `\\normalsize`
3. 同时检查 `styling.fonts` 中各级标题的设置

#### 页面布局问题
**现象**: 内容超出页面边界或留白过多
**解决方案**:
1. 检查 `page_layout.margins` 设置
2. 调整 `page_layout.column_separation` 分栏间距
3. 考虑调整 `code_style.appearance.font_size`

### 配置文件格式问题

#### JSON语法错误
**常见错误**:
- 缺少逗号或多余逗号
- 引号配对错误  
- 花括号或方括号不匹配

**解决方法**:
1. 使用JSON验证工具检查语法
2. 使用支持JSON语法高亮的编辑器
3. 运行 `python main.py validate-config` 检查

#### 配置值类型错误
**常见错误**:
- 字符串值缺少引号
- 数值类型使用引号包围
- 布尔值使用错误格式

**正确格式示例**:
```json
{
  "font_size": "5pt",           // 字符串，需要引号
  "tab_size": 4,                // 数值，无需引号
  "line_numbers": true,         // 布尔值，无需引号
  "margins": {                  // 对象，使用花括号
    "left": "0.8cm"
  },
  "extensions": [".cpp", ".h"]  // 数组，使用方括号
}
```
