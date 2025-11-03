# 配置文件结构

**注意：** 配置文件应放在程序所在目录的 `configuration` 文件夹内，命名为 `config.json`。

配置文件采用 JSON 格式，包含以下部分：

``` json
{
	"_metadata": {
		"version": "3.0",
		"description": "配置文件，定义LaTeX文档的各项参数"
	},
	"TEX": {
		"author": "Aiza",
		"subject": "文档主题",
		"title": {	// 标题页设置
			"content": "文档标题",
			"note": "标题页额外内容"
		},
		"code": {	// 代码块设置
			"font_family": "Fira Code",
			"font_size": "8pt",				// 注意单位是 pt
			"line_height": "8.1pt",			// 注意单位是 pt
			"auto_break_lines": true,		// 自动换行
			"tab_size": 4,					// Tab 键宽度，单位是空格数
			"space_before": "1em",			// 段落前间距
			"space_after": "1em",			// 段落后间距
			"keyword_color": "blue",		// 关键字颜色
			"comment_color": "green",		// 注释颜色
			"string_color": "red",			// 字符串颜色
			"bg_color": "gray!10"			// 背景颜色
		},
		"hyperref": {
			"enable_colorlinks": true,		// 启用彩色链接
			"link_color": "blue",			// 内部链接颜色
			"numbered_bookmarks": true,		// 书签显示章节编号
			"open_bookmarks": false			// 打开PDF时展开书签
		},
		"geometry": {	// 页面布局设置
			"paper_size": "a4paper",		// 纸张大小
			"left_margin": "1.4cm",			// 左边距
			"right_margin": "1.4cm",		// 右边距
			"top_margin": "1.4cm",			// 上边距
			"bottom_margin": "1.4cm",		// 下边距
			"column_sep": "1.5cm"			// 列间距
		},
		"global": {		// 全局设置
			"main_font": "Times New Roman",	// 主字体
			"cjk_main_font": "SimSun",		// CJK 字体
			"page_style": "plain"			// 页面样式，如 plain, empty, headings
		}
	},
	"PROGRAM": {		// 构建源相关设置
		"include_file_types": [				// 包含的代码文件类型
			".cpp", ".c", ".json", ".hpp"
		],
		"output_file_name": "output"		// 输出PDF文件名
	}
}
```

**提示：** 配置文件中的注释仅供参考，实际 JSON 文件中不应包含注释。