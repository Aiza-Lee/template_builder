# 配置文件结构

**配置文件位置**：默认在用户目录下的 `~/.config/NightingaleStudio/TemplateBuilder/config.json`（Linux/macOS）或 `%APPDATA%\NightingaleStudio\TemplateBuilder\config.json`（Windows）。可通过 `-c` 指定自定义路径。

配置文件采用 **JSONC 格式**（允许 `//` 与 `/* */` 注释以及尾随逗号）。配置时无需添加所有配置项，没有出现的配置项将使用默认值。

## 获取当前默认值

运行 `./template_builder init -o template_builder.config.jsonc` 即可获得一份带注释的当前默认配置（含所有 key 与 inline 注释）。这是与内置 `DefaultConfig.jsonc` 同步的权威来源。

## 配置结构

`TEX` 段驱动 LaTeX 文档布局与代码渲染，`PROGRAM` 段控制源码树处理。下表列出全部可配置项（截至 Round 3c）。

### TEX 段

| 路径 | 默认值 | 说明 |
|---|---|---|
| `TEX.author` | `"Aiza"` | PDF 作者（`pdfauthor` + `\author`）。`escape_latex_specials=true` 时自动转义 `_` `%` `&` 等 |
| `TEX.subject` | `"文档主题"` | PDF 主题（`pdfsubject`）。自动转义 |
| `TEX.title.content` | `"文档标题"` | 文档主标题（`\title` + `pdftitle`）。自动转义 |
| `TEX.title.note` | `"标题页额外内容"` | 标题页额外内容。**Round 3a**：使用 `\begin{<alignment>}…\end{<alignment>}`（center/flushleft/flushright） |
| `TEX.title.date` | `"\\today"` | `\date{}` 的值 |
| `TEX.title.font_size_cmd` | `"\\Large\\bfseries"` | **Round 3a**：`\title{...}` 内嵌的字号/字重命令 |
| `TEX.title.alignment` | `"center"` | **Round 3a**：标题页 note 区域 LaTeX 环境名：`center` / `flushleft` / `flushright` |
| `TEX.title.escape_latex_specials` | `true` | 是否对 author/subject/title 三个字段做 LaTeX 转义。设 `false` 保留 LaTeX 字面量（如 `$\frac{1}{2}$`） |
| `TEX.code.minted_style` | `"bw"` | pygments 样式：[https://pygments.org/styles/](https://pygments.org/styles/) |
| `TEX.code.font_family` | `"Fira Code"` | 等宽字体（`\newfontfamily`） |
| `TEX.code.font_size` | `"6pt"` | 代码字号 |
| `TEX.code.line_height` | `"6pt"` | 行高 |
| `TEX.code.tab_size` | `4` | Tab 展开列数。**双重身份**：被 C# 当 int 读出用于代码展开；同时被 LaTeX 模板当字面量（`tabsize=##CODE_TAB_SIZE##`）。修改时两处效果同步 |
| `TEX.code.bg_color` | `"gray!3"` | 背景色（`xcolor` 语法） |
| `TEX.code.break_lines` | `true` | `\setminted{breaklines=...}`：是否自动换行 |
| `TEX.code.line_numbers` | `true` | `\setminted{linenos=...}`：是否显示行号 |
| `TEX.code.numbers_sep` | `"2pt"` | `\setminted{numbersep=...}`：行号与代码间距 |
| `TEX.code.frame` | `"none"` | `\setminted{frame=...}`：`none` / `leftline` / `lines` / `single` |
| `TEX.code.autogobble` | `false` | `\setminted{autogobble=...}`：自动剥离前导空白 |
| `TEX.code.highlight_color` | `"yellow!10"` | `\setminted{highlightcolor=...}`：选区高亮色（minted 2.x） |
| `TEX.code.mathescape` | `false` | **Round 3a**：minted 内识别 `$...$` 为数学模式 |
| `TEX.code.escapeinside` | `""` | **Round 3a**：minted 内可逃逸区间（如 `"|_"`）。空 = 不输出该选项 |
| `TEX.code.xleftmargin` | `""` | **Round 3a**：minted 左边距（如 `"10pt"`） |
| `TEX.code.xrightmargin` | `""` | **Round 3a**：minted 右边距 |
| `TEX.code.firstnumber` | `"auto"` | **Round 3a**：起始行号；`"auto"` 续接上一个 block，否则用整数 |
| `TEX.code.stepnumber` | `"1"` | **Round 3a**：行号间隔 |
| `TEX.code.numberstyle` | `""` | **Round 3a**：行号样式命令（如 `"\\tiny\\color{red}"`） |
| `TEX.code.showspaces` | `false` | **Round 3a**：可视化空格字符 |
| `TEX.code.showtabs` | `false` | **Round 3a**：可视化 Tab 字符 |
| `TEX.hyperref.enable_colorlinks` | `true` | `colorlinks=...` |
| `TEX.hyperref.link_color` | `"blue"` | 内部链接颜色 |
| `TEX.hyperref.file_color` | `"magenta"` | 文件链接颜色 |
| `TEX.hyperref.url_color` | `"cyan"` | URL 颜色 |
| `TEX.hyperref.cite_color` | `"green"` | **Round 3a**：`citecolor=...`：引用链接颜色 |
| `TEX.hyperref.anchor_color` | `"black"` | **Round 3a**：`anchorcolor=...`：锚点链接颜色 |
| `TEX.hyperref.pdf_border` | `"{0 0 0}"` | **Round 3a**：`pdfborder=...`：链接边框。值需自带 `{}`（如 `"{0 0 0}"` 表示无边） |
| `TEX.hyperref.pdf_lang` | `"zh-CN"` | **Round 3a**：`pdflang=...`：PDF 语言标识 |
| `TEX.hyperref.numbered_bookmarks` | `true` | 书签显示章节编号 |
| `TEX.hyperref.open_bookmarks` | `false` | 打开 PDF 时展开书签 |
| `TEX.hyperref.bookmarks_depth` | `5` | PDF 书签深度 |
| `TEX.geometry.paper_size` | `"a4paper"` | 纸张大小 |
| `TEX.geometry.left_margin` | `"1cm"` | 左边距 |
| `TEX.geometry.right_margin` | `"1cm"` | 右边距 |
| `TEX.geometry.top_margin` | `"1cm"` | 上边距 |
| `TEX.geometry.bottom_margin` | `"1.2cm"` | 下边距 |
| `TEX.geometry.column_sep` | `"1cm"` | 双栏间距 |
| `TEX.geometry.headheight` | `"12pt"` | **Round 3a**：`\geometry{headheight=...}`：页眉高度 |
| `TEX.geometry.headsep` | `"20pt"` | **Round 3a**：`\geometry{headsep=...}`：页眉与正文间距 |
| `TEX.geometry.footskip` | `"30pt"` | **Round 3a**：`\geometry{footskip=...}`：页脚基线到正文底部距离 |
| `TEX.geometry.column_rule` | `false` | **Round 3a**：`\geometry{columnrule=...}`：twocolumn 时是否画分隔线（onecolumn 模式无效） |
| `TEX.layout.section_depth` | `5` | `\setcounter{secnumdepth}{...}` 编号深度。**Round 3a 双重身份**：同时决定 CodeBlockGenerator 截取的章节层级（深度超出 clamp 到最后一层而非报错） |
| `TEX.layout.toc_depth` | `5` | `\setcounter{tocdepth}{...}` 目录深度 |
| `TEX.layout.columns` | `2` | 1 或 2：正文总列数。`1` 适合演示/草稿，`2` 适合代码密集 |
| `TEX.layout.toc_in_columns` | `false` | `true` = 目录单栏显示（更宽的章节标题）；`false` = 目录与正文同列数 |
| `TEX.layout.escape_section_names` | `true` | **Round 3a**：CodeBlockGenerator 是否对章节名（目录名）做 LaTeX 转义。设 `false` 时目录名原样输出（若含 `_` `%` 等特殊字符由 xelatex 报错） |
| `TEX.fancy.head_left` | `""` | 启用 fancy 后，header 左侧文本 |
| `TEX.fancy.head_center` | `""` | header 中间 |
| `TEX.fancy.head_right` | `""` | header 右侧 |
| `TEX.fancy.foot_left` | `""` | footer 左侧 |
| `TEX.fancy.foot_center` | `""` | footer 中间 |
| `TEX.fancy.foot_right` | `""` | footer 右侧 |
| `TEX.fancy.rule_width` | `"0.4pt"` | `\headrulewidth`：header 下分隔线粗细；`0pt` 隐藏 |
| `TEX.section.format_section` | `""` | `\titleformat{\section}{<this>}{...}{<separator>}{...}`：section 标题格式（`\Large\bfseries\color{blue}` 等） |
| `TEX.section.format_subsection` | `""` | subsection 格式 |
| `TEX.section.format_subsubsection` | `""` | subsubsection 格式 |
| `TEX.section.format_paragraph` | `""` | paragraph 格式 |
| `TEX.section.format_subparagraph` | `""` | subparagraph 格式 |
| `TEX.section.format_separator` | `"1em"` | **Round 3a**：`\titleformat{<level>}{...}{...}{<this>}{...}` 第 4 个参数（标题与编号之间的水平间距），全部 5 个层级共用 |
| `TEX.metadata.keywords` | `[]` | PDF 关键词数组。写入 `pdfkeywords={kw1, kw2}` |
| `TEX.toc.title` | `"目录"` | **Round 3a**：`\renewcommand{\contentsname}{...}`：目录标题 |
| `TEX.toc.dot_leaders` | `true` | **Round 3a**：`true` = 保留 LaTeX 默认引导点；`false` = `\def\@dotsep{10000}` 取消引导点 |
| `TEX.global.main_font` | `"Times New Roman"` | 西文主字体（`\setmainfont`） |
| `TEX.global.cjk_main_font` | `"SimSun"` | CJK 主字体（`\setCJKmainfont`） |
| `TEX.global.cjk_sans_font` | `"SimHei"` | **Round 3a**：CJK sans 字体（`\setsansfont`） |
| `TEX.global.cjk_main_bold_font` | `""` | **Round 3a**：`\setCJKmainfont{...}[BoldFont=...]`；空 = 不输出该选项（依赖 AutoFakeBold 或其他字体机制） |
| `TEX.global.cjk_main_italic_font` | `""` | **Round 3a**：`\setCJKmainfont{...}[ItalicFont=...]`；空 = 不输出该选项 |
| `TEX.global.cjk_auto_fake_bold` | `true` | **Round 3a**：`\setCJKmainfont{...}[AutoFakeBold=...]`：未指定 BoldFont 时自动用粗体算法模拟 |
| `TEX.global.cjk_auto_fake_slant` | `true` | **Round 3a**：`\setCJKmainfont{...}[AutoFakeSlant=...]`：未指定 ItalicFont 时自动用斜体算法模拟 |
| `TEX.global.page_style` | `"plain"` | `\pagestyle{...}`：`plain` / `headings` / `empty` / `fancy` |
| `TEX.docclass.base_font_size` | `"10pt"` | **Round 3a**：`\documentclass[<this>,...]{ctexart}` 主字号 |
| `TEX.docclass.orientation` | `"landscape"` | **Round 3a**：`\documentclass[...,<this>,...]{ctexart}` 与 `\geometry{...}`：landscape / portrait |
| `TEX.typesetting.microtype.protrusion` | `true` | **Round 3c**：`microtype` 字符伸出（标点悬挂到 margin 外）。代码密集文档（minted blocks）受益最明显 |
| `TEX.typesetting.microtype.expansion` | `true` | **Round 3c**：`microtype` 字体微扩展（改善断行、减少 underfull `\hbox` 警告） |
| `TEX.typesetting.microtype.kerning` | `true` | **Round 3c**：`microtype` 字偶距增强（XeLaTeX 下对部分字体生效） |
| `TEX.typesetting.parskip.enabled` | `false` | **Round 3c**：true 时插入 `\usepackage{parskip}`，段间用垂直空白替代 LaTeX 默认的段首缩进（modern 风格） |

### PROGRAM 段

| 路径 | 默认值 | 说明 |
|---|---|---|
| `PROGRAM.include_file_types` | `[".cpp", ".c", ...]` | 包含的源文件扩展名（白名单） |
| `PROGRAM.ignore_patterns` | `["*ignore*"]` | 排除的 glob 模式（`Microsoft.Extensions.FileSystemGlobbing` 语法） |
| `PROGRAM.code_language_overrides` | `[]` | **Round 3a**：扩展名→minted 语言映射的增量覆盖。字符串对数组 `["ext:lang"]`（如 `[".md:markdown", ".cpp:cpp"]`）。合并到代码内置 24 条默认映射上；非法条目（缺 `:` / 非 `.` 前缀 / 字段空）会被跳过 |
| `PROGRAM.build.timeout_seconds` | `120` | **Round 3b**：单次 xelatex pass 的超时秒数。超时则 Kill 整个进程树（防 minted/pygmentize 子进程残留）+ 报 `XelatexFailure` 退出。`0` = 不限时（pass 0 给 `WaitForExit`，等同 Round 2 行为）。值会被 clamp 到 `[0, 600]` |
| `PROGRAM.build.pass_count` | `2` | **Round 3b**：xelatex 编译 pass 数（clamp 到 `[1, 5]`）。`1` 可省 ~50% xelatex 时间，但若保留 `\tableofcontents` 会导致页码显示 "?"（xelatex 自然行为，需 pass 2 解析交叉引用）。**用户责任**：选 1 时需自行确认能接受不完整的 ToC |

## 启用 fancy 头脚示例

```jsonc
"GLOBAL": { "page_style": "fancy" },
"FANCY": {
    "head_right": "Page \\thepage",
    "foot_center": "Generated by template_builder"
}
```

## 自定义 section 标题样式

```jsonc
"SECTION": {
    "format_section": "\\Large\\bfseries\\color{blue}",
    "format_subsection": "\\large\\bfseries\\color{black!70}",
    "format_separator": "2em"
}
```

## 自定义 CJK 字体（Round 3a）

```jsonc
"GLOBAL": {
    "cjk_main_font": "Noto Serif CJK SC",
    "cjk_main_bold_font": "Noto Serif CJK SC Bold",
    "cjk_auto_fake_bold": false
}
```

## 自定义 extension→language 映射（Round 3a）

```jsonc
"PROGRAM": {
    "include_file_types": [".py", ".cpp", ".md"],
    "code_language_overrides": [".md:markdown", ".py:python3"]
}
```

注：扩展名必须同时存在于内置 `CODE_LANGUAGES_EXTENSIONS` 白名单（26 种）才会走 minted 高亮；白名单外的扩展名仅作为 raw text 输出。

## 排版与设计刷新（Round 3c）

### 关闭 microtype（恢复 LaTeX 默认排版）

microtype 默认三选项全开。如需彻底关闭以避免某些老 TeX Live 的兼容问题：

```jsonc
"TYPESETTING": {
    "microtype": {
        "protrusion": false,
        "expansion": false,
        "kerning": false
    }
}
```

三选项全 `false` 时 `\usepackage[false,false,false]{microtype}` 仍会发出（LaTeX 合法 no-op），任意一项改回 `true` 即激活对应功能。

### 现代段间空白（parskip）

启用后 LaTeX 段首缩进改为段间垂直空白，对代码速查表特别友好：

```jsonc
"TYPESETTING": {
    "parskip": {
        "enabled": true
    }
}
```

注：parskip 是「激进」选项，会改变文档整体节奏；与 ctexart 章节间距可能有视觉重叠，建议对实际文档预览后再决定。

## 占位符

模板中的 `##KEY##` 由 `PdfBuilder.ReplaceMainPlaceholders` 用 `TEX` 段对应大写下划线 key 的值替换（如 `TEX.geometry.paper_size` → `##GEOMETRY_PAPER_SIZE##`）。

`<<KEY>>` 是 **runtime** 占位符，由 `PdfBuilder.GenerateTexContent` 在 `ReplaceMainPlaceholders` 之前单独替换：
- `<<MINTED_OUTPUTDIR>>` → minted 输出目录
- `<<METADATA_KEYWORDS>>` → `TEX.metadata.keywords` 数组拼接为 `"kw1, kw2"`
- `<<DOC_CLASS_COLUMNS>>` / `<<LAYOUT_TOC_OPENING>>` / `<<LAYOUT_BODY_OPENING>>` → 来自 `TEX.layout.columns` + `toc_in_columns` 的布局控制
- `<<CJK_FONT_BLOCK>>` → **Round 3a**：动态拼装的 `\setCJKmainfont{...}[...]` 块（含 BoldFont/ItalicFont 空值剔除 + AutoFake 开关）
- `<<TOC_DOT_LEADERS_LINE>>` → **Round 3a**：默认空；`TEX.toc.dot_leaders=false` 时替换为 `\def\@dotsep{10000}`
- `<<CONTENT>>` → CodeBlockGenerator 生成的代码块正文（在 `ReplaceMainPlaceholders` 之后才替换）

## 性能调优（Round 3b）

### 单 pass 加速（pass_count=1）

去掉 ToC 时的最快构建路径：

```jsonc
"PROGRAM": {
    "build": {
        "pass_count": 1   // 跳过 pass 2，省 ~50% xelatex 时间
    }
}
```

适用场景：草稿 / 快速预览 / 不需要 ToC 的代码速查表。**注意**：xelatex 自然需要两次 pass 来解析 `\tableofcontents` 与 hyperref 书签；设为 1 时保留 `\tableofcontents` 会导致 ToC 页码显示 "?"。如需 ToC 仍正常工作，请同时移除 Main.tex 中的 `\tableofcontents` 行（可通过 `--template-dir` 覆盖 Main.tex）。

### 进程超时保护

大文档场景下 xelatex 偶尔会 hang（如字体解析死锁）：

```jsonc
"PROGRAM": {
    "build": {
        "timeout_seconds": 600   // 10 分钟硬上限；超时则 Kill 整个进程树 + 报 XelatexFailure
    }
}
```

`timeout_seconds=0` 等同「不限时」（Round 2 行为），仅在调试场景使用。值会自动 clamp 到 `[0, 600]`。

## 新增配置项 checklist

新增 `TEX` 配置项时需要：
1. 在 `Resources/DefaultConfig.jsonc` 注册（同时给出注释）。**默认值遵循 Round 2 行为**（保证老用户零差异）
2. 在 `Resources/Templates/Main.tex` 引用 `##NEW_KEY##`（注意大写）。**空值特殊处理**：若新选项的值可能为空（如 `cjk_main_bold_font`、`escapeinside`），用 runtime `<<BLOCK>>` 占位符 + PdfBuilder 条件拼装，避免 `BoldFont=,` 这种 LaTeX 非法语法
3. 跑 `dotnet run -- validate -s <src> -c <your-cfg.jsonc>` 确认无 `unresolved ##NEW_KEY##` 报告

`PROGRAM` 段的 `ignore_patterns` 走 .NET glob（`Microsoft.Extensions.FileSystemGlobbing`）。`Matcher.Match(name).HasMatches = false` 表示被 exclude 命中。

`PROGRAM.code_language_overrides` 走字符串对数组（`["ext:lang"]`）；扩展名必须先在代码内置的 `CODE_LANGUAGES_EXTENSIONS` 白名单里才会走 minted 高亮路径，否则仅以 raw text 输出。

## 覆盖 LaTeX 模板

无需改 C# 代码：在 `build` 时加 `--template-dir <dir>`，把同名 `Main.tex` 和/或 `CodeBlock.tex` 放进该目录即可。`init` 输出的末尾也会提示此用法。
