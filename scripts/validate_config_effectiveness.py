#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
配置生效验证工具
检查配置文件中的设置是否正确应用到生成的 LaTeX 文件中
"""

import json
import sys
from pathlib import Path

# 项目路径
PROJECT_ROOT = Path(__file__).parent.parent
CONFIG_FILE = PROJECT_ROOT / 'config.json'
LATEX_FILE = PROJECT_ROOT / 'build' / 'ACM_Templates.tex'

def main():
    try:
        # 加载配置文件
        if not CONFIG_FILE.exists():
            print('配置文件不存在')
            return 1
            
        with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
            config = json.load(f)
        
        # 加载LaTeX文件
        if not LATEX_FILE.exists():
            print('LaTeX文件不存在，请先运行构建命令')
            return 1
            
        with open(LATEX_FILE, 'r', encoding='utf-8') as f:
            latex_content = f.read()
        
        print('开始验证配置生效情况...')
        print('=' * 50)
        
        total_checks = 0
        passed_checks = 0
        
        # 验证项目信息
        print('\n[项目信息模块验证]')
        title = config.get('project', {}).get('title', '')
        if title and title in latex_content:
            print(f'  [OK] 文档标题: {title}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 文档标题未应用: {title}')
        total_checks += 1
        
        author = config.get('project', {}).get('author', '')
        if author and author in latex_content:
            print(f'  [OK] 作者信息: {author}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 作者信息未应用: {author}')
        total_checks += 1
        
        # 验证字体设置
        print('\n[排版设置模块验证]')
        main_font = config.get('typography', {}).get('fonts', {}).get('main', '')
        if main_font and main_font in latex_content:
            print(f'  [OK] 主字体: {main_font}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 主字体未应用: {main_font}')
        total_checks += 1
        
        # 验证代码样式
        print('\n[代码样式模块验证]')
        font_size = config.get('code_style', {}).get('appearance', {}).get('font_size', '')
        if font_size and f'fontsize{{{font_size}}}' in latex_content:
            print(f'  [OK] 代码字体大小: {font_size}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 代码字体大小未应用: {font_size}')
        total_checks += 1
        
        # 验证行间距设置
        line_spacing = config.get('code_style', {}).get('formatting', {}).get('line_spacing', '')
        font_size = config.get('code_style', {}).get('appearance', {}).get('font_size', '')
        if line_spacing and font_size:
            try:
                # 计算期望的行高
                base = float(font_size.replace('pt', ''))
                spacing_factor = float(line_spacing)
                expected_lh = base + base * spacing_factor
                expected_pattern = f'fontsize{{{font_size}}}{{{expected_lh:.1f}pt}}'
                
                if expected_pattern in latex_content:
                    print(f'  [OK] 代码行间距: {line_spacing} (行高: {expected_lh:.1f}pt)')
                    passed_checks += 1
                else:
                    print(f'  [FAIL] 代码行间距未应用: {line_spacing} (期望行高: {expected_lh:.1f}pt)')
            except (ValueError, TypeError):
                print(f'  [FAIL] 代码行间距配置格式错误: {line_spacing}')
        else:
            print(f'  [FAIL] 代码行间距配置缺失')
        total_checks += 1
        
        # 验证目录设置  
        print('\n[目录设置模块验证]')
        show_toc = config.get('table_of_contents', {}).get('structure', {}).get('show', True)
        if show_toc and 'tableofcontents' in latex_content:
            print(f'  [OK] 目录显示: 启用')
            passed_checks += 1
        elif not show_toc and 'tableofcontents' not in latex_content:
            print(f'  [OK] 目录显示: 禁用')
            passed_checks += 1
        else:
            print(f'  [FAIL] 目录显示设置未正确应用')
        total_checks += 1
        
        # 验证目录字体样式 - 详细检查您选中的配置
        print('\n[目录字体样式验证]')
        toc_fonts = config.get('table_of_contents', {}).get('styling', {}).get('fonts', {})
        
        # 验证一级标题字体
        section_font = toc_fonts.get('section', '')
        if section_font and section_font in latex_content:
            print(f'  [OK] 一级标题字体: {section_font}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 一级标题字体未应用: {section_font}')
        total_checks += 1
        
        # 验证二级标题字体
        subsection_font = toc_fonts.get('subsection', '')
        if subsection_font and subsection_font in latex_content:
            print(f'  [OK] 二级标题字体: {subsection_font}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 二级标题字体未应用: {subsection_font}')
        total_checks += 1
        
        # 验证三级标题字体
        subsubsection_font = toc_fonts.get('subsubsection', '')
        if subsubsection_font and subsubsection_font in latex_content:
            print(f'  [OK] 三级标题字体: {subsubsection_font}')
            passed_checks += 1
        else:
            print(f'  [FAIL] 三级标题字体未应用: {subsubsection_font}')
        total_checks += 1
        
        print('\n' + '=' * 50)
        print(f'验证结果: {passed_checks}/{total_checks} 项通过')
        
        if passed_checks == total_checks:
            print('所有配置都已正确生效！')
            return 0
        else:
            print('部分配置未正确生效，请检查上述问题')
            return 1
            
    except Exception as e:
        print(f'验证失败: {e}')
        return 1

if __name__ == '__main__':
    sys.exit(main())
