#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LaTeX 样式配置器
负责处理字体、颜色、代码样式等LaTeX格式设置
"""

from typing import Dict, Any

class LaTeXStyler:
    """LaTeX样式配置器"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
    
    def format_font_size(self, font_size: str, line_spacing: str = "1.0") -> str:
        """格式化字体大小配置"""
        if not font_size:
            return 'small'
        
        # 数字+pt格式
        if font_size.endswith('pt'):
            try:
                size_float = float(font_size[:-2])
                if line_spacing != "1.0":
                    # 使用自定义行间距
                    line_spacing_float = float(line_spacing)
                    line_spacing_pt = round(size_float * line_spacing_float, 1)
                else:
                    # 使用标准行间距（1.2倍字体大小）
                    line_spacing_pt = round(size_float * 1.2, 1)
                
                return f'fontsize{{{font_size}}}{{{line_spacing_pt}pt}}\\selectfont'
            except ValueError:
                pass
        
        # 纯数字
        try:
            size_float = float(font_size)
            size_pt = f"{size_float}pt"
            if line_spacing != "1.0":
                line_spacing_float = float(line_spacing)
                line_spacing_pt = round(size_float * line_spacing_float, 1)
            else:
                line_spacing_pt = round(size_float * 1.2, 1)
            
            return f'fontsize{{{size_pt}}}{{{line_spacing_pt}pt}}\\selectfont'
        except ValueError:
            pass
        
        # LaTeX预定义大小
        predefined_sizes = ['tiny', 'scriptsize', 'footnotesize', 'small', 
                           'normalsize', 'large', 'Large', 'LARGE', 'huge', 'Huge']
        if font_size in predefined_sizes:
            return font_size
        
        return 'small'
    
    def apply_font_config(self, tex_content: str) -> str:
        """应用字体配置到LaTeX内容"""
        fonts = self.config.get('fonts', {})
        
        # 替换主字体
        if 'main' in fonts:
            tex_content = tex_content.replace(
                r'\setmainfont{Times New Roman}',
                f'\\setmainfont{{{fonts["main"]}}}'
            )
        
        # 替换中文字体设置
        if 'cjk_main' in fonts:
            old_pattern = r'\setCJKmainfont[BoldFont=SimHei, ItalicFont=KaiTi]{SimSun}'
            new_pattern = f'\\setCJKmainfont[BoldFont={fonts.get("cjk_sans", "SimHei")}, ItalicFont=KaiTi]{{{fonts["cjk_main"]}}}'
            tex_content = tex_content.replace(old_pattern, new_pattern)
        
        if 'cjk_sans' in fonts:
            tex_content = tex_content.replace(
                r'\setCJKsansfont{SimHei}',
                f'\\setCJKsansfont{{{fonts["cjk_sans"]}}}'
            )
        
        if 'cjk_mono' in fonts:
            # 处理中文等宽字体
            cjk_mono = fonts['cjk_mono']
            if cjk_mono in ['Fira Code', 'Consolas', 'Courier New']:
                # 英文等宽字体不适合中文，使用默认
                cjk_mono = 'SimSun'
            
            tex_content = tex_content.replace(
                r'\setCJKmonofont[Scale=0.85]{SimSun}',
                f'\\setCJKmonofont[Scale=0.85]{{{cjk_mono}}}'
            )
        
        return tex_content
    
    def apply_code_style_config(self, tex_content: str) -> str:
        """应用代码样式配置"""
        code_style = self.config.get('code_style', {})
        fonts = self.config.get('fonts', {})
        
        # 获取字体大小和行间距
        font_size_raw = code_style.get('font_size', 'small')
        line_spacing = code_style.get('line_spacing', '1.0')
        font_size_formatted = self.format_font_size(font_size_raw, line_spacing)
        
        # 处理Fira Code字体
        if fonts.get('code') == 'Fira Code':
            tex_content = self._apply_fira_code_config(tex_content, font_size_formatted, code_style)
        else:
            tex_content = self._apply_standard_font_config(tex_content, font_size_formatted, font_size_raw)
        
        # 应用lstset配置
        tex_content = self._apply_lstset_config(tex_content, code_style)
        
        return tex_content
    
    def _apply_fira_code_config(self, tex_content: str, font_size_formatted: str, code_style: Dict[str, Any]) -> str:
        """应用Fira Code字体配置"""
        # 更新firatextstyle命令
        if 'newcommand{\\firatextstyle}{\\firafont}' in tex_content:
            tex_content = tex_content.replace(
                r'\newcommand{\firatextstyle}{\firafont}',
                f'\\newcommand{{\\firatextstyle}}{{\\firafont\\{font_size_formatted}}}'
            )
        
        return tex_content
    
    def _apply_standard_font_config(self, tex_content: str, font_size_formatted: str, font_size_raw: str) -> str:
        """应用标准字体配置"""
        # 替换基本样式中的字体大小
        for size in ['tiny', 'small', 'normalsize', 'large']:
            if size != font_size_raw:
                tex_content = tex_content.replace(
                    f'basicstyle=\\{size}\\ttfamily',
                    f'basicstyle=\\{font_size_formatted}\\ttfamily'
                )
        
        return tex_content
    
    def _apply_lstset_config(self, tex_content: str, code_style: Dict[str, Any]) -> str:
        """应用lstset配置"""
        # 找到lstset设置部分
        lstset_start = tex_content.find('\\lstset{')
        if lstset_start == -1:
            return tex_content
        
        # 找到对应的结束括号
        lstset_end = self._find_matching_brace(tex_content, lstset_start + 7)
        if lstset_end == -1:
            return tex_content
        
        # 构建新的lstset配置
        new_lstset = self._build_lstset_options(code_style)
        
        # 替换现有的lstset
        tex_content = tex_content[:lstset_start] + new_lstset + tex_content[lstset_end:]
        
        return tex_content
    
    def _find_matching_brace(self, text: str, start_pos: int) -> int:
        """找到匹配的结束括号位置"""
        brace_count = 0
        for i in range(start_pos, len(text)):
            if text[i] == '{':
                brace_count += 1
            elif text[i] == '}':
                brace_count -= 1
                if brace_count == 0:
                    return i + 1
        return -1
    
    def _build_lstset_options(self, code_style: Dict[str, Any]) -> str:
        """构建lstset配置选项"""
        options = ["language=C++"]
        
        # 基本样式
        fonts = self.config.get('fonts', {})
        font_size_raw = code_style.get('font_size', 'small')
        line_spacing = code_style.get('line_spacing', '1.0')
        
        if fonts.get('code') == 'Fira Code':
            options.append("basicstyle=\\firatextstyle")
        else:
            font_size_formatted = self.format_font_size(font_size_raw, line_spacing)
            if font_size_raw.endswith('pt') or font_size_raw.replace('.', '').isdigit():
                options.append(f"basicstyle=\\{font_size_formatted}\\ttfamily")
            else:
                options.append(f"basicstyle=\\{font_size_formatted}\\ttfamily")
        
        # 颜色配置
        colors = self.config.get('colors', {})
        options.extend([
            f"keywordstyle=\\color{{{colors.get('code_keyword', 'blue')}}}\\bfseries",
            f"commentstyle=\\color{{{colors.get('code_comment', 'green!50!black')}}}",
            f"stringstyle=\\color{{{colors.get('code_string', 'red')}}}",
            f"numberstyle=\\tiny\\color{{{colors.get('code_number', 'gray')}}}"
        ])
        
        # 其他选项
        self._add_lstset_basic_options(options, code_style)
        
        return "\\lstset{\n    " + ",\n    ".join(options) + "\n}"
    
    def _add_lstset_basic_options(self, options: list, code_style: Dict[str, Any]) -> None:
        """添加基本的lstset选项"""
        # 行号
        if code_style.get('line_numbers', True):
            options.extend(["numbers=left", "numbersep=3pt"])
        else:
            options.append("numbers=none")
        
        # 框架
        frame_style = code_style.get('frame_style', 'single')
        options.append(f"frame={frame_style}")
        
        if code_style.get('frame_round', True):
            options.append("frameround=tttt")
        
        # 其他设置
        options.extend([
            f"framerule={code_style.get('frame_rule', '0.3pt')}",
            f"breaklines={str(code_style.get('break_lines', True)).lower()}",
            "breakatwhitespace=false",
            f"tabsize={code_style.get('tab_size', 2)}",
            "showspaces=false",
            "showtabs=false",
            "keepspaces=true",
            f"columns={code_style.get('columns', 'flexible')}",
            f"aboveskip={code_style.get('above_skip', '0.5em')}",
            f"belowskip={code_style.get('below_skip', '0.5em')}"
        ])
        
        # 间距配置
        base_width = code_style.get('base_width', '0.6em')
        if base_width != '0.6em':
            options.append(f"basewidth={base_width}")
        
        line_spacing = code_style.get('line_spacing', '1.0')
        if line_spacing != '1.0':
            options.append(f"lineskip={line_spacing}ex")
        
        # 边距配置
        left_margin = code_style.get('left_margin', '0pt')
        right_margin = code_style.get('right_margin', '0pt')
        if left_margin != '0pt':
            options.append(f"xleftmargin={left_margin}")
        if right_margin != '0pt':
            options.append(f"xrightmargin={right_margin}")
