#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
配置生效验证工具
检查config.json中的配置是否正确应用到生成的LaTeX文件中
"""

import json
from pathlib import Path

def verify_config_effectiveness():
    """验证配置是否生效"""
    project_root = Path(__file__).parent.parent
    config_file = project_root / 'config.json'
    latex_file = project_root / 'build' / 'ACM_Templates.tex'
    
    if not config_file.exists():
        print("❌ 配置文件不存在")
        return False
    
    if not latex_file.exists():
        print("❌ LaTeX文件不存在，请先运行 build")
        return False
    
    # 读取配置
    with open(config_file, 'r', encoding='utf-8') as f:
        config = json.load(f)
    
    # 读取LaTeX文件
    with open(latex_file, 'r', encoding='utf-8') as f:
        latex_content = f.read()
    
    print("=== 配置生效验证 ===\n")
    
    all_passed = True
    
    # 验证字体配置
    print("🔤 字体配置验证:")
    fonts = config.get('fonts', {})
    
    if 'main' in fonts:
        expected = f"\\setmainfont{{{fonts['main']}}}"
        if expected in latex_content:
            print(f"  ✅ 主字体: {fonts['main']}")
        else:
            print(f"  ❌ 主字体: {fonts['main']} (未应用)")
            all_passed = False
    
    if 'cjk_main' in fonts:
        pattern = f"setCJKmainfont[BoldFont={fonts.get('cjk_sans', 'SimHei')}, ItalicFont=KaiTi]{{{fonts['cjk_main']}}}"
        if pattern in latex_content:
            print(f"  ✅ 中文主字体: {fonts['cjk_main']}")
        else:
            print(f"  ❌ 中文主字体: {fonts['cjk_main']} (未应用)")
            all_passed = False
    
    if fonts.get('code') == 'Fira Code':
        if 'firafont' in latex_content and 'firatextstyle' in latex_content:
            print(f"  ✅ 代码字体: {fonts['code']} (连字符支持)")
        else:
            print(f"  ❌ 代码字体: {fonts['code']} (连字符支持未配置)")
            all_passed = False
    
    print()
    
    # 验证代码样式配置
    print("📝 代码样式配置验证:")
    code_style = config.get('code_style', {})
    
    if code_style:
        # 验证字体大小
        font_size = code_style.get('font_size', 'small')
        
        # 检查是否为数字+pt格式或纯数字
        is_numeric = font_size.endswith('pt') or font_size.replace('.', '').replace('-', '').isdigit()
        
        if is_numeric:
            # 对于数字字体大小，检查fontsize命令
            if f"fontsize{{{font_size}}}" in latex_content or f"fontsize{{{font_size}pt}}" in latex_content:
                print(f"  ✅ 字体大小: {font_size} (数字格式)")
            else:
                print(f"  ❌ 字体大小: {font_size} (数字格式，未应用)")
                all_passed = False
        else:
            # 对于预定义大小，检查命令形式
            if f"basicstyle=\\{font_size}\\firatextstyle" in latex_content:
                print(f"  ✅ 字体大小: {font_size}")
            else:
                print(f"  ❌ 字体大小: {font_size} (未应用)")
                all_passed = False
        
        # 验证制表符大小
        tab_size = code_style.get('tab_size', 2)
        if f"tabsize={tab_size}" in latex_content:
            print(f"  ✅ 制表符大小: {tab_size}")
        else:
            print(f"  ❌ 制表符大小: {tab_size} (未应用)")
            all_passed = False
        
        # 验证列对齐方式
        columns = code_style.get('columns', 'flexible')
        if f"columns={columns}" in latex_content:
            print(f"  ✅ 列对齐方式: {columns}")
        else:
            print(f"  ❌ 列对齐方式: {columns} (未应用)")
            all_passed = False
        
        # 验证字符基础宽度
        base_width = code_style.get('base_width', '0.6em')
        if base_width != '0.6em':
            if f"basewidth={base_width}" in latex_content:
                print(f"  ✅ 字符基础宽度: {base_width}")
            else:
                print(f"  ❌ 字符基础宽度: {base_width} (未应用)")
                all_passed = False
        else:
            print(f"  ⚪ 字符基础宽度: {base_width} (使用默认值)")
        
        # 验证行间距
        line_spacing = code_style.get('line_spacing', '1.0')
        if line_spacing != '1.0':
            if f"lineskip={line_spacing}ex" in latex_content:
                print(f"  ✅ 行间距: {line_spacing}")
            else:
                print(f"  ❌ 行间距: {line_spacing} (未应用)")
                all_passed = False
        else:
            print(f"  ⚪ 行间距: {line_spacing} (使用默认值)")
        
        # 验证边框样式
        frame_style = code_style.get('frame_style', 'single')
        if f"frame={frame_style}" in latex_content:
            print(f"  ✅ 边框样式: {frame_style}")
        else:
            print(f"  ❌ 边框样式: {frame_style} (未应用)")
            all_passed = False
        
        # 验证行号显示
        line_numbers = code_style.get('line_numbers', True)
        if line_numbers:
            if "numbers=left" in latex_content:
                print(f"  ✅ 行号显示: 启用")
            else:
                print(f"  ❌ 行号显示: 配置为启用但未应用")
                all_passed = False
        else:
            if "numbers=none" in latex_content:
                print(f"  ✅ 行号显示: 禁用")
            else:
                print(f"  ❌ 行号显示: 配置为禁用但未应用")
                all_passed = False
        
        # 验证上下间距
        above_skip = code_style.get('above_skip', '0.5em')
        below_skip = code_style.get('below_skip', '0.5em')
        if f"aboveskip={above_skip}" in latex_content:
            print(f"  ✅ 上间距: {above_skip}")
        else:
            print(f"  ❌ 上间距: {above_skip} (未应用)")
            all_passed = False
        
        if f"belowskip={below_skip}" in latex_content:
            print(f"  ✅ 下间距: {below_skip}")
        else:
            print(f"  ❌ 下间距: {below_skip} (未应用)")
            all_passed = False
    
    print()
    
    # 验证颜色配置
    print("🎨 颜色配置验证:")
    colors = config.get('colors', {})
    
    if colors:
        keyword_color = colors.get('code_keyword', 'blue')
        if f"keywordstyle=\\color{{{keyword_color}}}\\bfseries" in latex_content:
            print(f"  ✅ 关键字颜色: {keyword_color}")
        else:
            print(f"  ❌ 关键字颜色: {keyword_color} (未应用)")
            all_passed = False
        
        comment_color = colors.get('code_comment', 'green!50!black')
        if f"commentstyle=\\color{{{comment_color}}}" in latex_content:
            print(f"  ✅ 注释颜色: {comment_color}")
        else:
            print(f"  ❌ 注释颜色: {comment_color} (未应用)")
            all_passed = False
        
        string_color = colors.get('code_string', 'red')
        if f"stringstyle=\\color{{{string_color}}}" in latex_content:
            print(f"  ✅ 字符串颜色: {string_color}")
        else:
            print(f"  ❌ 字符串颜色: {string_color} (未应用)")
            all_passed = False
        
        number_color = colors.get('code_number', 'gray')
        if f"numberstyle=\\tiny\\color{{{number_color}}}" in latex_content:
            print(f"  ✅ 行号颜色: {number_color}")
        else:
            print(f"  ❌ 行号颜色: {number_color} (未应用)")
            all_passed = False
    
    print()
    
    # 检查重复配置
    print("🔍 重复配置检查:")
    has_old_font_size = 'code_font_size' in config
    has_new_font_size = 'code_style' in config and 'font_size' in config.get('code_style', {})
    
    if has_old_font_size and has_new_font_size:
        print("  ⚠️ 发现重复配置: code_font_size 和 code_style.font_size")
        print("     建议删除 code_font_size，使用 code_style.font_size")
        all_passed = False
    elif has_old_font_size:
        print("  ⚠️ 使用旧版配置: code_font_size")
        print("     建议迁移到 code_style.font_size")
    elif has_new_font_size:
        print("  ✅ 使用新版配置: code_style.font_size")
    
    print()
    
    # 总结
    if all_passed:
        print("🎉 所有配置都已正确应用！")
    else:
        print("⚠️ 部分配置未正确应用，请检查上述问题")
    
    return all_passed

if __name__ == "__main__":
    verify_config_effectiveness()
