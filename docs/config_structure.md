# 配置文件结构

**配置文件位置**：默认在用户目录下的 `~/.config/NightingaleStudio/TemplateBuilder/config.json`（Linux/macOS）或 `%APPDATA%\NightingaleStudio\TemplateBuilder\config.json`（Windows）。可通过 `-c` 指定自定义路径。

配置文件采用 **JSONC 格式**（允许 `//` 与 `/* */` 注释以及尾随逗号）。配置时无需添加所有配置项，没有出现的配置项将使用默认值。

## 获取当前默认值

运行 `./template_builder init -o template_builder.config.jsonc` 即可获得一份带注释的当前默认配置（含所有 key 与 inline 注释）。这是与内置 `DefaultConfig.jsonc` 同步的权威来源。

## 配置结构示例

以下示例与内置默认配置对齐（实际值可能随版本变化，以 `init` 输出的为准）：

``` jsonc
// ===== TEX section: drives the LaTeX document layout & code rendering =====
"TEX": {
	"author": "Aiza",
	"subject": "文档主题",

	// 标题页：content 为主标题，note 为副标题/额外说明
	"title": {
		"content": "文档标题",
		"note": "标题页额外内容"
	},

	// 代码块（minted）的样式与字体设置
	"code": {
		"minted_style": "bw",				// pygments 样式：https://pygments.org/styles/
		"font_family": "Fira Code",
		"font_size": "6pt",					// 单位 pt
		"line_height": "6pt",				// 单位 pt
		"tab_size": 4,						// Tab 展开为空格的列数
		"bg_color": "gray!3"				// 背景色（xcolor 语法）
	},

	// hyperref 超链接 / PDF 书签
	"hyperref": {
		"enable_colorlinks": true,			// 启用彩色链接
		"link_color": "blue",				// 内部链接颜色
		"file_color": "magenta",			// 文件链接颜色
		"url_color": "cyan",				// URL 颜色
		"numbered_bookmarks": true,			// 书签显示章节编号
		"open_bookmarks": false				// 打开 PDF 时展开书签
	},

	// 页面尺寸与边距（geometry 包）
	"geometry": {
		"paper_size": "a4paper",			// 纸张大小
		"left_margin": "1cm",				// 左边距
		"right_margin": "1cm",				// 右边距
		"top_margin": "1cm",				// 上边距
		"bottom_margin": "1.2cm",			// 下边距
		"column_sep": "1cm"					// 列间距
	},

	// 文档级字体与页眉页脚
	"global": {
		"main_font": "Times New Roman",		// 主字体
		"cjk_main_font": "SimSun",			// CJK 字体
		"page_style": "plain"				// 页面样式：plain / empty / headings
	}
},

// ===== PROGRAM section: controls source-tree processing =====
"PROGRAM": {
	// 包含的源文件扩展名（白名单）
	"include_file_types": [
		".cpp", ".c", ".json", ".tex", ".hpp", ".h", ".py", ".txt"
	],
	// 排除的 glob 模式（Microsoft.Extensions.FileSystemGlobbing 语法）
	"ignore_patterns": [
		"*ignore*"
	]
}
```

## 占位符

- `Main.tex` 中的 `##KEY##` 占位符由 `ReplaceMainPlaceholders` 用 `TEX` 段对应 key 的值替换。
- `<<CONTENT>>` / `<<MINTED_OUTPUTDIR>>` / `<<LANGUAGE>>` / `<<CODE>>` 是运行时替换符（大小写敏感的「双尖括号」），由 `CodeBlockGenerator` 写入代码块正文。

## 新增配置项

新增 `TEX` 配置项时需要：
1. 在 `Resources/DefaultConfig.jsonc` 注册（同时给出注释）
2. 在 `Resources/Templates/Main.tex` 引用 `##NEW_KEY##`（注意大写）

`PROGRAM` 段的 `ignore_patterns` 走 .NET glob（`Microsoft.Extensions.FileSystemGlobbing`）。`Matcher.Match(name).HasMatches = false` 表示被 exclude 命中。
