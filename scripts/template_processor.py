#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""统一 LaTeX 模板处理器 (精简版)

职责: 读取模板 -> 占位符映射 -> 替换 -> 验证
保持占位符集合最小化, 仅服务当前 `unified_template.tex`。
"""

from __future__ import annotations
import re
from pathlib import Path
from typing import Any, Dict, List


class LaTeXTemplateProcessor:
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self._cache: Dict[Path, str] = {}

    # ---------------- IO ----------------
    def load_template(self, path: Path) -> str:
        if path in self._cache:
            return self._cache[path]
        try:
            txt = path.read_text(encoding="utf-8")
        except Exception as e:  # pragma: no cover
            raise FileNotFoundError(f"无法读取模板 {path}: {e}")
        self._cache[path] = txt
        return txt

    # ---------------- Public API ----------------
    def process_template(self, template: str, body: str) -> str:
        mapping = self._build_mapping()
        mapping["AUTO_INSERTED_CONTENT"] = body
        return self._replace(template, mapping)

    def validate_template(self, template: str) -> Dict[str, Any]:
        found = set(re.findall(r"\{\{([A-Z_]+)\}\}", template))
        available = set(self.get_available_placeholders()) | {"AUTO_INSERTED_CONTENT"}
        undefined = found - available
        missing = {"AUTO_INSERTED_CONTENT"} - found
        return {
            "valid": not undefined and not missing,
            "found_placeholders": sorted(found),
            "available_placeholders": sorted(available),
            "undefined_placeholders": sorted(undefined),
            "missing_required": sorted(missing),
            "validation_messages": self._messages(undefined, missing),
        }

    def get_available_placeholders(self) -> List[str]:
        m = self._build_mapping()
        return sorted(m.keys())

    # ---------------- Internal ----------------
    def _replace(self, text: str, mapping: Dict[str, str]) -> str:
        pat = re.compile(r"\{\{([A-Z_]+)\}\}")
        return pat.sub(lambda m: mapping.get(m.group(1), m.group(0)), text)

    def _messages(self, undefined: set, missing: set) -> List[str]:
        out: List[str] = []
        if undefined:
            out.append("发现未定义的占位符: " + ", ".join(sorted(undefined)))
        if missing:
            out.append("缺少必需的占位符: " + ", ".join(sorted(missing)))
        if not out:
            out.append("模板验证通过")
        return out

    def _build_mapping(self) -> Dict[str, str]:
        m: Dict[str, str] = {}
        m.update(self._doc_class())
        m.update(self._page())
        m.update(self._fonts())
        m.update(self._colors())
        m.update(self._code())
        m.update(self._layout())
        m.update(self._doc_info())
        m.update(self._spacing())
        return m

    # ---- mapping sections ----
    def _doc_class(self) -> Dict[str, str]:
        layout = self.config.get("build", {}).get("layout", "landscape")
        opts = "10pt,landscape,twocolumn" if layout == "landscape" else "10pt"
        return {"DOCUMENT_CLASS_OPTIONS": opts}

    def _page(self) -> Dict[str, str]:
        layout = self.config.get("build", {}).get("layout", "landscape")
        margins = self.config.get("page_layout", {}).get("margins", {}).get(layout, {})
        column_sep = self.config.get("page_layout", {}).get("column_separation", "1cm")
        return {
            "PAGE_ORIENTATION": "landscape" if layout == "landscape" else "portrait",
            "MARGIN_LEFT": margins.get("left", "1.5cm"),
            "MARGIN_RIGHT": margins.get("right", "1.5cm"),
            "MARGIN_TOP": margins.get("top", "2cm"),
            "MARGIN_BOTTOM": margins.get("bottom", "2cm"),
            "COLUMN_SEP_OPTION": "columnsep=1cm" if layout == "landscape" else "",
            "COLUMN_SEPARATION": column_sep if layout == "landscape" else "0cm",
            "COLUMN_RULE_WIDTH": "0pt",
        }

    def _fonts(self) -> Dict[str, str]:
        f = self.config.get("typography", {}).get("fonts", {})
        return {
            "MAIN_FONT": f.get("main", "Times New Roman"),
            "CJK_MAIN_FONT": f.get("cjk_main", "SimSun"),
            "CJK_BOLD_FONT": f.get("cjk_bold", "SimHei"),
            "CJK_ITALIC_FONT": f.get("cjk_italic", "KaiTi"),
        }

    def _colors(self) -> Dict[str, str]:
        c = self.config.get("typography", {}).get("colors", {})
        def g(k: str, d: str): return c.get(k, d)
        return {
            "SECTION_COLOR": g("section", "blue!70!black"),
            "SUBSECTION_COLOR": g("subsection", "green!60!black"),
            "SUBSUBSECTION_COLOR": g("subsubsection", "orange!80!black"),
            "CODE_BACKGROUND_COLOR": g("code_background", "gray!5"),
            "CODE_KEYWORD_COLOR": g("code_keyword", "blue"),
            "CODE_COMMENT_COLOR": g("code_comment", "green!50!black"),
            "CODE_STRING_COLOR": g("code_string", "red"),
            "CODE_NUMBER_COLOR": g("code_number", "gray"),
        }

    def _code(self) -> Dict[str, str]:
        s = self.config.get("code_style", {})
        f = self.config.get("typography", {}).get("fonts", {})
        
        # 合并所有 code_style 子配置
        appearance = s.get("appearance", {})
        formatting = s.get("formatting", {})
        spacing = s.get("spacing", {})
        
        font_size = appearance.get("font_size", "6pt")
        
        # Calculate line height based on font size and line_spacing config
        line_spacing = formatting.get("line_spacing", "0.5")  # 默认行间距
        if font_size.endswith("pt"):
            try:
                base = float(font_size[:-2])
                spacing_factor = float(line_spacing)
                lh = f"{base + base * spacing_factor:.1f}pt"
            except Exception:
                lh = "7pt"
        else:
            lh = "7pt"
        
        # Handle Fira Code font setup
        code_font = f.get("code", "")
        if code_font == "Fira Code":
            code_font_setup = """% Setup Fira Code for listings
\\newfontfamily\\firafont[Ligatures=TeX,Contextuals=Alternate]{Fira Code}
\\newcommand{\\firatextstyle}{\\firafont}"""
            code_basic_style = f"\\firatextstyle\\fontsize{{{font_size}}}{{{lh}}}\\selectfont"
        else:
            code_font_setup = "% Using default TTY font"
            code_basic_style = f"\\ttfamily\\fontsize{{{font_size}}}{{{lh}}}\\selectfont"
        
        return {
            "CODE_FONT_SETUP": code_font_setup,
            "CODE_BASIC_STYLE": code_basic_style,
            "CODE_FONT_SIZE": font_size,
            "CODE_LINE_HEIGHT": lh,
            "CODE_FRAME_STYLE": appearance.get("frame_style", "leftline"),
            "CODE_BREAK_LINES": "true" if appearance.get("break_lines", True) else "false",
            "CODE_TAB_SIZE": str(formatting.get("tab_size", 4)),
            "CODE_ABOVE_SKIP": spacing.get("above_skip", "3pt"),
            "CODE_BELOW_SKIP": spacing.get("below_skip", "3pt"),
            "CODE_NUMBERS_OPTION": "numbers=left," if appearance.get("line_numbers", True) else "",
        }

    def _layout(self) -> Dict[str, str]:
        if self.config.get("build", {}).get("layout", "landscape") == "landscape":
            return {"TITLE_PAGE_LAYOUT": "\\onecolumn", "TOC_LAYOUT": "\\twocolumn", "MAIN_CONTENT_LAYOUT": "% 双列"}
        return {"TITLE_PAGE_LAYOUT": "% 单列", "TOC_LAYOUT": "% 单列", "MAIN_CONTENT_LAYOUT": "% 单列"}

    def _doc_info(self) -> Dict[str, str]:
        d = self.config.get("project", {})
        return {
            "DOCUMENT_TITLE": d.get("title", "ACM 模板集合"),
            "DOCUMENT_AUTHOR": d.get("author", "Aiza"),
            "DOCUMENT_SUBTITLE": d.get("subtitle", "编译时间: \\today"),
            "DOCUMENT_DATE": d.get("date", ""),
        }

    def _spacing(self) -> Dict[str, str]:
        s = self.config.get("spacing", {})
        toc_config = self.config.get("table_of_contents", {})
        
        # 从新的结构中获取 TOC 配置
        toc_structure = toc_config.get("structure", {})
        toc_styling = toc_config.get("styling", {})
        toc_spacing = toc_config.get("spacing", {})
        
        return {
            "SECTION_SPACING_BEFORE": s.get("section_before", "4pt"),
            "SECTION_SPACING_AFTER": s.get("section_after", "2pt"),
            "SUBSECTION_SPACING_BEFORE": s.get("subsection_before", "3pt"),
            "SUBSECTION_SPACING_AFTER": s.get("subsection_after", "1.5pt"),
            "SUBSUBSECTION_SPACING_BEFORE": s.get("subsubsection_before", "2pt"),
            "SUBSUBSECTION_SPACING_AFTER": s.get("subsubsection_after", "1pt"),
            "PARAGRAPH_SKIP": s.get("paragraph_skip", "1pt"),
            "PARAGRAPH_INDENT": s.get("paragraph_indent", "0pt"),
            "TOC_SPACING_BEFORE": self._format_spacing_command(toc_spacing.get("before_toc", "-1.2cm")),
            "TOC_SPACING_AFTER": self._format_spacing_command(toc_spacing.get("after_toc", "-1cm")),
            "TOC_DEPTH": str(toc_structure.get("depth", 3)),
            "TOC_FONT_SIZE": toc_structure.get("font_size", "\\normalsize"),  # 新增：目录整体字体大小
            "TOC_SEC_SPACING": toc_spacing.get("before_section", "0.5pt"),
            "TOC_SUBSEC_SPACING": toc_spacing.get("before_subsection", "0.2pt"),
            "TOC_SUBSUBSEC_SPACING": toc_spacing.get("before_subsubsection", "0pt"),
            "TOC_SEC_FONT": toc_styling.get("fonts", {}).get("section", "\\footnotesize\\bfseries"),
            "TOC_SUBSEC_FONT": toc_styling.get("fonts", {}).get("subsection", "\\scriptsize\\bfseries"),
            "TOC_SUBSUBSEC_FONT": toc_styling.get("fonts", {}).get("subsubsection", "\\tiny"),
            "PAGE_STYLE": s.get("page_style", "plain"),
            "HEADER_FOOTER_SETTINGS": s.get("header_footer", ""),
            "CUSTOM_TITLE_CONTENT": self._custom_title(),
        }

    def _custom_title(self) -> str:
        tp = self.config.get("title_page_content", {})
        if not tp.get("enabled", True):
            return ""
        desc = tp.get("description", "本文档包含常用 ACM 模板。")
        return f"\\begin{{center}}\n{desc}\n\\end{{center}}"
    
    def _format_spacing_command(self, spacing_value: str) -> str:
        """格式化间距命令，将原始值包装为 LaTeX vspace 命令"""
        if not spacing_value:
            return ""
        
        # 如果已经是完整的 LaTeX 命令，直接返回
        if spacing_value.startswith("\\"):
            return spacing_value
        
        # 否则包装为 vspace 命令
        return f"\\vspace*{{{spacing_value}}}"

