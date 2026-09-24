# 配置文件结构

**配置文件位置**：默认在用户目录下的 `~/.config/NightingaleStudio/TemplateBuilder/config.json`（Linux/macOS）或 `%APPDATA%\NightingaleStudio\TemplateBuilder\config.json`（Windows）。可通过 `-c` 指定自定义路径。

配置文件采用 **JSONC 格式**（允许 `//` 与 `/* */` 注释以及尾随逗号）。配置时无需添加所有配置项，没有出现的配置项将使用默认值。

## 获取当前默认值与 Schema

运行以下命令即可获得一份带注释的当前默认配置骨架：

```bash
./template_builder init -o template_builder.config.jsonc [--format jsonc|json] [--force]
```

`init` 执行完成后：
- **配置文件**：写出带全部默认值与注释的配置文件（与内置 `DefaultConfig.jsonc` 保持同步的权威来源）；
- **配套 Schema**：在配置文件所在同级目录同步交付 `DefaultConfig.schema.json`。配置文件首行的 `"$schema": "./DefaultConfig.schema.json"` 会被主流编辑器（VS Code / Rider / IntelliJ）自动识别，直接提供字段自动补全、悬停文档提示与语法验证；
- **安全覆盖**：默认拒绝覆盖已存在的文件；显式指定 `--force / -f` 时会自动备份原文件为 `.bak`；若同目录下已有 Schema 文件，非 `--force` 模式下绝不破坏用户已有文件；
- **下一步引导**：控制台输出的 `validate` 与 `build` 命令自动附带刚刚生成的准确 `-c "<配置文件路径>"`，含必要路径转义与引号包裹。

## 配置分层优先级与空值语义

template_builder 统一采用严格的三层配置优先级模型：

```
内置默认值 (Builtin)  ──>  项目配置文件 (Project)  ──>  运行时覆盖 (Runtime Override)
   (底底兜底保障)              (-c 指定的配置)             (CLI 参数 / TUI 临时调试)
```

1. **内置默认 (Builtin)**：嵌入在二进制程序中的官方基线配置（`DefaultConfig.jsonc`），确保即使完全不传配置文件亦可开箱构建；
2. **项目配置 (Project)**：用户通过 `-c / --config` 显式指定的配置文件（或默认用户配置文件）；
3. **运行时覆盖 (Override)**：用户在 TUI 交互会话或命令行指定 `--preset`、参数覆盖时生成的临时覆盖项。覆盖项仅作用于当前构建，不破坏磁盘上的原始配置文件（可在 TUI 导航中心主动选择「保存修改至配置文件」完成写回晋升）。

### 默认值与空值/留空语义 (UX-14 & UX-15)

- **作者与主题 (`TEX.author` / `TEX.subject`)**：默认值为空字符串 `""`，PDF 元数据中不会出现任何开发者个人署名或占位文字；
- **主标题 (`TEX.title.content`)**：默认值为空字符串 `""`。留空时构建流水线会自动从源码目录名称安全推导并转义特殊字符（如 `my_algo` 自动转义为 `my\_algo`）；
- **独立封面页与目录页 (`TEX.title.enable_cover` / `TEX.toc.enable_toc`)**：
  - `title.enable_cover` 默认为 `false`（不生成独立封面页，正文从首页紧凑开始）；
  - `toc.enable_toc` 默认为 `true`（生成文档目录）；
  - 当封面与目录均关闭时，代码正文直接从第 1 页双栏排版开始，**全量 10 种排版/分栏组合实测保证 0 空白页**；
- **产物与文件保护机制**：
  - 临时编译文件与 minted 缓存集中隔离在 `<sourceDir>/.template_builder/build/`，构建成功时通过原子移动安全发布至目标路径；
  - 若编译中途失败，已有的旧版本目标 PDF 得到完好保留，损坏的半成品 PDF 作为 `DiagnosticPdfPath` 隔离保留供排查；
  - 空源码目录或所有文件被过滤时直接在预检阶段拦截（退出码 6），杜绝假阳性。

## 配置结构

`TEX` 段驱动 LaTeX 文档布局与代码渲染，`PROGRAM` 段控制源码树处理。下表列出全部可配置项。

### TEX 段

| 路径 | 默认值 | 说明 |
|---|---|---|
| `TEX.author` | `""` | PDF 作者（`pdfauthor` + `\author`）。默认留空；`escape_latex_specials=true` 时自动转义 `_` `%` `&` 等 |
| `TEX.subject` | `""` | PDF 主题（`pdfsubject`）。默认留空；自动转义 |
| `TEX.title.content` | `""` | 文档主标题（`\title` + `pdftitle`）。默认留空时自动从源码目录名合理推导并转义 |
| `TEX.title.enable_cover` | `false` | 是否生成独立封面页（`\maketitle` 与独立分页）。默认 `false`，正文直接从首页紧凑开始，彻底消除空白页 |
| `TEX.title.note` | `""` | 标题页额外内容。默认留空；使用 `\begin{<alignment>}…\end{<alignment>}`（center/flushleft/flushright） |
| `TEX.title.date` | `"\\today"` | `\date{}` 的值 |
| `TEX.title.font_size_cmd` | `"\\Large\\bfseries"` | `\title{...}` 内嵌的字号/字重命令 |
| `TEX.title.alignment` | `"center"` | 标题页 note 区域 LaTeX 环境名：`center` / `flushleft` / `flushright` |
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
| `TEX.code.mathescape` | `false` | minted 内识别 `$...$` 为数学模式 |
| `TEX.code.escapeinside` | `""` | minted 内可逃逸区间（如 `"|_"`）。空 = 不输出该选项 |
| `TEX.code.xleftmargin` | `""` | minted 左边距（如 `"10pt"`） |
| `TEX.code.xrightmargin` | `""` | minted 右边距 |
| `TEX.code.firstnumber` | `"auto"` | 起始行号；`"auto"` 续接上一个 block，否则用整数 |
| `TEX.code.stepnumber` | `"1"` | 行号间隔 |
| `TEX.code.numberstyle` | `""` | 行号样式命令（如 `"\\tiny\\color{red}"`） |
| `TEX.code.showspaces` | `false` | 可视化空格字符 |
| `TEX.code.showtabs` | `false` | 可视化 Tab 字符 |
| `TEX.code.minted_outputdir` | `""` | minted 缓存目录覆盖。空 = 跟随 PDF 输出目录（原行为，写入 `_minted/`）；非空 = 直接用作 `\usepackage[outputdir=...]{minted}` 的参数。**CI 用法**：指向稳定路径（如 `${{ runner.temp }}/tex-minted`）配合 `actions/cache` 跨 run 复用 `_minted/` 缓存，省去首遍 xelatex 的 pygmentize 重新高亮 |
| `TEX.hyperref.enable_colorlinks` | `true` | `colorlinks=...` |
| `TEX.hyperref.link_color` | `"blue"` | 内部链接颜色 |
| `TEX.hyperref.file_color` | `"magenta"` | 文件链接颜色 |
| `TEX.hyperref.url_color` | `"cyan"` | URL 颜色 |
| `TEX.hyperref.cite_color` | `"green"` | `citecolor=...`：引用链接颜色 |
| `TEX.hyperref.anchor_color` | `"black"` | `anchorcolor=...`：锚点链接颜色 |
| `TEX.hyperref.pdf_border` | `"{0 0 0}"` | `pdfborder=...`：链接边框。值需自带 `{}`（如 `"{0 0 0}"` 表示无边） |
| `TEX.hyperref.pdf_lang` | `"zh-CN"` | `pdflang=...`：PDF 语言标识 |
| `TEX.hyperref.numbered_bookmarks` | `true` | 书签显示章节编号 |
| `TEX.hyperref.open_bookmarks` | `false` | 打开 PDF 时展开书签 |
| `TEX.hyperref.bookmarks_depth` | `5` | PDF 书签深度 |
| `TEX.geometry.paper_size` | `"a4paper"` | 纸张大小 |
| `TEX.geometry.left_margin` | `"1cm"` | 左边距 |
| `TEX.geometry.right_margin` | `"1cm"` | 右边距 |
| `TEX.geometry.top_margin` | `"1cm"` | 上边距 |
| `TEX.geometry.bottom_margin` | `"1.2cm"` | 下边距 |
| `TEX.geometry.column_sep` | `"1cm"` | 双栏间距 |
| `TEX.geometry.headheight` | `"12pt"` | `\geometry{headheight=...}`：页眉高度 |
| `TEX.geometry.headsep` | `"20pt"` | `\geometry{headsep=...}`：页眉与正文间距 |
| `TEX.geometry.footskip` | `"30pt"` | `\geometry{footskip=...}`：页脚基线到正文底部距离 |
| `TEX.geometry.column_rule` | `false` | twocolumn 时是否画分隔线（`\setlength{\columnseprule}{0.4pt}`；onecolumn 模式无效） |
| `TEX.layout.section_depth` | `5` | `\setcounter{secnumdepth}{...}` 编号深度。同时决定 CodeBlockGenerator 截取的章节层级（深度超出 clamp 到最后一层而非报错） |
| `TEX.layout.toc_depth` | `5` | `\setcounter{tocdepth}{...}` 目录深度 |
| `TEX.layout.columns` | `2` | 1 或 2：正文总列数。`1` 适合长行代码/普通屏幕阅读，`2` 适合代码高密排版（竞赛打印推荐） |
| `TEX.layout.toc_in_columns` | `false` | 目录排版布局：`false` = 跟随正文（默认与正文列数相同，双栏正文下目录亦为双栏）；`true` = 独立单栏（目录独占单栏全宽排版，章节标题更宽展） |
| `TEX.layout.escape_section_names` | `true` | CodeBlockGenerator 是否对章节名（目录名）做 LaTeX 转义。设 `false` 时目录名原样输出（若含 `_` `%` 等特殊字符由 xelatex 报错） |
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
| `TEX.section.format_separator` | `"1em"` | `\titleformat{<level>}{...}{...}{<this>}{...}` 第 4 个参数（标题与编号之间的水平间距），全部 5 个层级共用 |
| `TEX.metadata.keywords` | `[]` | PDF 关键词数组。写入 `pdfkeywords={kw1, kw2}` |
| `TEX.toc.enable_toc` | `true` | 是否生成文档目录（`\tableofcontents`）。设为 `false` 可完全跳过目录生成并消除目录分页 |
| `TEX.toc.title` | `"目录"` | `\renewcommand{\contentsname}{...}`：目录标题 |
| `TEX.toc.dot_leaders` | `true` | `true` = 保留 LaTeX 默认引导点；`false` = `\def\@dotsep{10000}` 取消引导点 |
| `TEX.global.main_font` | `"Times New Roman"` | 西文主字体（`\setmainfont`） |
| `TEX.global.cjk_main_font` | `"SimSun"` | CJK 主字体（`\setCJKmainfont`） |
| `TEX.global.cjk_sans_font` | `"SimHei"` | CJK sans 字体（`\setsansfont`） |
| `TEX.global.cjk_main_bold_font` | `""` | `\setCJKmainfont{...}[BoldFont=...]`；空 = 不输出该选项（依赖 AutoFakeBold 或其他字体机制） |
| `TEX.global.cjk_main_italic_font` | `""` | `\setCJKmainfont{...}[ItalicFont=...]`；空 = 不输出该选项 |
| `TEX.global.cjk_auto_fake_bold` | `true` | `\setCJKmainfont{...}[AutoFakeBold=...]`：未指定 BoldFont 时自动用粗体算法模拟 |
| `TEX.global.cjk_auto_fake_slant` | `true` | `\setCJKmainfont{...}[AutoFakeSlant=...]`：未指定 ItalicFont 时自动用斜体算法模拟 |
| `TEX.global.custom_font_dirs` | `[]` | 额外字体目录列表，免安装自动识别本地 TTF/OTF 字体 |
| `TEX.global.page_style` | `"plain"` | `\pagestyle{...}`：`plain` / `headings` / `empty` / `fancy` |
| `TEX.docclass.base_font_size` | `"10pt"` | `\documentclass[<this>,...]{ctexart}` 主字号 |
| `TEX.docclass.orientation` | `"landscape"` | `\documentclass[...,<this>,...]{ctexart}` 与 `\geometry{...}`：landscape / portrait |
| `TEX.docclass.fontset` | `"auto"` | ctex 字体集预设：`auto` / `fandol` / `ubuntu` / `windows` / `mac` / `none` |
| `TEX.typesetting.microtype.protrusion` | `true` | `microtype` 字符伸出（标点悬挂到 margin 外）。代码密集文档（minted blocks）受益最明显 |
| `TEX.typesetting.microtype.expansion` | `false` | `microtype` 字体微扩展（XeLaTeX 引擎不支持，默认 false 避免编译报错） |
| `TEX.typesetting.microtype.kerning` | `false` | `microtype` 字偶距增强（XeLaTeX 引擎原生由 fontspec 处理，默认 false 避免编译报错） |
| `TEX.typesetting.parskip.enabled` | `false` | true 时插入 `\usepackage{parskip}`，段间用垂直空白替代 LaTeX 默认的段首缩进（modern 风格） |

### PROGRAM 段

| 路径 | 默认值 | 说明 |
|---|---|---|
| `PROGRAM.include_file_types` | `[".cpp", ".c", ...]` | 包含的源文件扩展名（白名单） |
| `PROGRAM.ignore_patterns` | `["*ignore*"]` | 排除的 glob 模式（`Microsoft.Extensions.FileSystemGlobbing` 语法） |
| `PROGRAM.code_language_overrides` | `[]` | 扩展名→minted 语言映射与渲染模式的增量覆盖。字符串对数组 `["ext:lang"]`（如 `[".md:markdown", ".tex:latex", ".snippet:raw"]`）。内置默认映射包含常见编程语言、Markdown 及普通文本（`.txt`）；普通文本及未知扩展名默认以保真纯文本代码块（`text`）渲染；`.tex` 或指定为 `raw` 的文件按原生 LaTeX 语法解析 |
| `PROGRAM.build.timeout_seconds` | `120` | 单次 xelatex pass 的超时秒数。超时则 Kill 整个进程树（防 minted/pygmentize 子进程残留）+ 报 `XelatexFailure` 退出。`0` = 不限时（pass 0 给 `WaitForExit`，）。值会被 clamp 到 `[0, 600]` |
| `PROGRAM.build.pass_count` | `2` | xelatex 编译 pass 数（clamp 到 `[1, 5]`）。`1` 可省 ~50% xelatex 时间，但若保留 `\tableofcontents` 会导致页码显示 "?"（xelatex 自然行为，需 pass 2 解析交叉引用）。**用户责任**：选 1 时需自行确认能接受不完整的 ToC |

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

## 自定义 CJK 字体

```jsonc
"GLOBAL": {
    "cjk_main_font": "Noto Serif CJK SC",
    "cjk_main_bold_font": "Noto Serif CJK SC Bold",
    "cjk_auto_fake_bold": false
}
```

## 自定义 extension→language 映射与渲染类型

```jsonc
"PROGRAM": {
    "include_file_types": [".py", ".cpp", ".md", ".txt", ".tex"],
    "code_language_overrides": [
        ".md:markdown",     // 显式指定 pygments 高亮词法器
        ".tex:latex",       // 将 .tex 自身作为代码块展示（而非嵌入 LaTeX 正文）
        ".snippet:raw"      // 显式指示按原生 LaTeX 语法解析并插入正文
    ]
}
```

注：渲染类型与代码块处理规则如下：
1. **代码与 Markdown 高亮**：扩展名在映射表中对应具体词法器（如 `python`, `cpp`, `markdown`）时，采用对应语言的代码块高亮渲染；
2. **普通文本与未知扩展名**：如 `.txt`、`.csv`、`.log` 等未知扩展名文件，统一采用纯文本代码块（`\begin{minted}{text}`）保真渲染，完整保留所有特殊符号（%、_、&、$、#、\ 等）、缩进与换行，杜绝 LaTeX 编译错误；
3. **原生 LaTeX 内容**：仅未配置高亮覆盖的 `.tex` 文件，或用户显式配置为 `".ext:raw"` 的扩展名，才直接按原生 LaTeX 代码插入正文。若需将 `.tex` 自身作为代码展示，请显式覆盖为 `".tex:latex"`。

## 排版与设计刷新

### 关闭 microtype（恢复 LaTeX 默认排版）

microtype 默认开启 protrusion（字符悬挂伸出），关闭 expansion 与 kerning（因 XeLaTeX 引擎不支持）。如需彻底关闭 microtype：

```jsonc
"TYPESETTING": {
    "microtype": {
        "protrusion": false,
        "expansion": false,
        "kerning": false
    }
}
```

三选项全 `false` 时 `\usepackage[protrusion=false,expansion=false,kerning=false]{microtype}` 仍会发出（LaTeX 合法 no-op），任意一项改回 `true` 即激活对应功能。

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
- `<<COVER_PAGE_BLOCK>>` → 当 `TEX.title.enable_cover=true` 时动态生成的封面块（含 `\maketitle` 与独立分页），为 `false` 时为空，彻底消除无用空白页
- `<<TOC_BLOCK>>` → 当 `TEX.toc.enable_toc=true` 时动态生成的目录块（含 `\tableofcontents` 与分页），为 `false` 时为空
- `<<LAYOUT_TOC_OPENING>>` / `<<LAYOUT_BODY_OPENING>>` → 结合 `TEX.layout.columns`、`toc_in_columns` 与封面/目录状态自适应计算的列控制指令
- `<<MINTED_OUTPUTDIR>>` → minted 输出目录
- `<<METADATA_KEYWORDS>>` → `TEX.metadata.keywords` 数组拼接为 `"kw1, kw2"`
- `<<DOC_CLASS_COLUMNS>>` → 来自 `TEX.layout.columns` 的文档类基础分栏参数
- `<<CJK_FONT_BLOCK>>` → 动态拼装的 `\setCJKmainfont{...}[...]` 块（含 BoldFont/ItalicFont 空值剔除 + AutoFake 开关）
- `<<TOC_DOT_LEADERS_LINE>>` → 默认空；`TEX.toc.dot_leaders=false` 时替换为 `\def\@dotsep{10000}`
- `<<CONTENT>>` → CodeBlockGenerator 生成的代码块正文（在 `ReplaceMainPlaceholders` 之后才替换）

## 排版预设与源码小样预览 (UX-14)

### 1. 排版预设体系 (Presets)

template_builder 内置了 3 种面向不同用户核心诉求的排版预设，可通过命令行 `--preset <id>` 或 TUI 导航中心「应用排版预设」快速切换，同时保留后续细粒度自定义覆盖能力：

| 预设 ID | 预设名称 | 适用场景 | 核心排版参数 |
|---|---|---|---|
| `competition` | 比赛打印预设 | ICPC/CCPC 现场赛携带代码手册，最大化信息密度 | 横向 A4、双栏排版、代码 6pt / 行高 6pt、边距 1cm、跟随正文双栏目录 |
| `reading` | 屏幕阅读预设 | 电脑、平板屏幕自习阅读与代码审阅，舒适护眼 | 纵向 A4、单栏排版、代码 9pt / 行高 13pt、代码自动换行、parskip 现代段间距 |
| `compact` | 紧凑速查预设 | 兼顾随身打印便携与长代码行可读性的速查手册 | 纵向 A4、双栏排版、代码 7pt / 行高 8pt、单栏独立全宽目录 |

预设应用规则：
- **基线提升**：预设直接覆盖当前会话中的关联排版字段；未在预设中定义的其他个性化设置保持不变；
- **增量叠加**：应用预设后，仍可在 TUI 菜单中或通过特定参数对单个字段（如字体、页边距、语言）进行细调覆盖。

### 2. 源码小样预览 (Sample Preview)

在构建包含数十甚至数百个源文件的庞大工程前，使用小样功能可以毫秒/秒级编译 1~2 页 PDF，以最小成本验证中文字体、超长行折行、代码高亮与目录样式：

- **命令行使用**：
  ```bash
  # 抽取前 2 个文件快速生成排版小样
  ./template_builder build -s ./my_templates -o ./proof.pdf --sample

  # 指定抽取前 3 个源文件
  ./template_builder build -s ./my_templates -o ./proof.pdf --sample 3 --preset reading
  ```
- **TUI 交互使用**：在 TUI 导航中心直接选择「编译排版小样」，构建成功后可一键调用系统默认阅读器查看真实渲染效果，确认满意后再执行全量构建。

## 性能调优

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

`timeout_seconds=0` 等同「不限时」，仅在调试场景使用。值会自动 clamp 到 `[0, 600]`。

## 新增配置项 checklist

新增 `TEX` 配置项时需要：
1. 在 `Resources/DefaultConfig.jsonc` 注册（同时给出注释）。默认值保持向后兼容
2. 在 `Resources/Templates/Main.tex` 引用 `##NEW_KEY##`（注意大写）。**空值特殊处理**：若新选项的值可能为空（如 `cjk_main_bold_font`、`escapeinside`），用 runtime `<<BLOCK>>` 占位符 + PdfBuilder 条件拼装，避免 `BoldFont=,` 这种 LaTeX 非法语法
3. 跑 `dotnet run -- validate -s <src> -c <your-cfg.jsonc>` 确认无 `unresolved ##NEW_KEY##` 报告

`PROGRAM` 段的 `ignore_patterns` 走 .NET glob（`Microsoft.Extensions.FileSystemGlobbing`）。`Matcher.Match(name).HasMatches = false` 表示被 exclude 命中。

`PROGRAM.code_language_overrides` 走字符串对数组（`["ext:lang"]`）；普通文本及未知扩展名默认统一以纯文本代码块（`\begin{minted}{text}`）保真渲染以防特殊字符编译报错；仅未配置覆盖的 `.tex` 文件或显式指定为 `".ext:raw"` 的扩展名按原生 LaTeX 语法插入正文。

### 独立章节标题与排版排序覆盖 (UX-13)

无需重命名磁盘上的源文件或目录即可自定义其在 LaTeX 目录及正文中的显示标题与排版先后顺序：

- **`PROGRAM.section_title_overrides`**（字符串数组）：每项为 `"相对路径或名称:显示标题"`。例如 `["01_intro.py:系统设计引言", "math:核心数学算法库"]`。精确相对路径优先于纯名称匹配。
- **`PROGRAM.section_order_overrides`**（字符串数组）：每项为 `"相对路径或名称:排序整数权重"`。数值越小越靠前排版（例如 `["02_core.py:10", "01_intro.py:20"]`）。未配置权重的项默认走自然数字字母序（Natural Sort，即 `2.py` 排在 `10.py` 前）。
- **构建前内容预览**：在 TUI 菜单中选择「预览收录文件与章节树」，可查看完整章节树、各文件收录与排除状态及排除原因解释（如扩展名不在白名单、命中 ignore 模式或空目录），修改配置后实时同步预览。

## 覆盖 LaTeX 模板

无需改 C# 代码：在 `build` 时加 `--template-dir <dir>`，把同名 `Main.tex` 和/或 `CodeBlock.tex` 放进该目录即可。`init` 输出的末尾也会提示此用法。

## 构建速度：minted 缓存复用

`TEX.code.minted_outputdir` 把 minted 的 `_minted/` 缓存从 PDF 输出目录中分离出来，让 CI 可以把缓存放到 `actions/cache` 可达的位置。下次 CI 命中缓存时，首遍 xelatex 可跳过 pygmentize 重新高亮。

```jsonc
"TEX": {
    "code": {
        "minted_outputdir": "/tmp/tex-minted"   // 或 ${{ runner.temp }}/tex-minted
    }
}
```

GitHub Actions 示例：

```yaml
- name: Cache minted cache
  uses: actions/cache@v4
  with:
    path: ${{ runner.temp }}/tex-minted
    key: tex-minted-${{ hashFiles('**/*.tex', '**/*.cpp', '**/*.h', 'DefaultConfig.jsonc') }}

- name: Build PDF
  run: |
    dotnet run -- build \
      -s ./src -o ./out.pdf \
      -c ./DefaultConfig.jsonc
```

`_minted/` 故意不被 `CleanupAuxiliaryFiles` 删除（`Cleanup` 只删 6 个 LaTeX 扩展 + `mid-output.tex`），保证跨 run 复用。

## CI 推荐配置

在 CI/CD 流水线（如 GitHub Actions）中，推荐在独立的 pre-check job 中运行 `validate --check-xelatex`，以在正式构建前提前暴露依赖缺失问题。

### GitHub Actions 示例工作流

```yaml
jobs:
  pre-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: 安装 TeX Live
        run: sudo apt-get install -y texlive-xetex texlive-fonts-recommended python3-pygments
      - name: 校验配置与依赖
        run: |
          dotnet run -- validate \
            -s ./src \
            -c ./config.jsonc \
            --check-xelatex    # 提前验证 xelatex + pygmentize 在 PATH 上

  build:
    needs: pre-check
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: 构建 PDF
        run: |
          dotnet run -- build \
            -s ./src \
            -o ./output.pdf \
            -c ./config.jsonc
      - name: 上传产物
        uses: actions/upload-artifact@v4
        with:
          name: pdf-output
          path: output.pdf
```

### 推荐配置说明

| 场景 | 建议 |
|------|------|
| pre-check job | 加 `--check-xelatex` 标志，提前暴露 xelatex / pygmentize 依赖缺失 |
| 多分支保护 | 在 branch protection rules 中要求 pre-check 通过后再合并 |
| 缓存加速 | 使用 `minted_outputdir` + `actions/cache` 复用 minted 高亮缓存（见上方"构建速度"小节） |
| JSON 输出 | 添加 `--format json` 可获得机器可读的校验报告，便于下游解析 |
