#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
代码重构验证脚本
验证重构后的所有模块是否正常工作
"""

import sys
from pathlib import Path

# 添加脚本目录到Python路径
PROJECT_ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(PROJECT_ROOT / 'scripts'))

def test_config_manager():
    """测试配置管理器"""
    print("测试配置管理器...")
    
    from config_manager import ConfigManager
    
    # 测试配置加载
    config_manager = ConfigManager(PROJECT_ROOT / 'config.json')
    assert config_manager.config is not None
    
    # 测试配置访问
    layout = config_manager.get('layout')
    assert layout in ['landscape', 'portrait']
    
    # 测试嵌套配置访问
    font_size = config_manager.get('code_style.font_size')
    assert font_size is not None
    
    # 测试配置设置
    config_manager.set('test.value', 'test')
    assert config_manager.get('test.value') == 'test'
    
    # 测试配置验证
    assert config_manager.validate() == True
    
    print("✓ 配置管理器测试通过")

def test_latex_styler():
    """测试LaTeX样式配置器"""
    print("测试LaTeX样式配置器...")
    
    from latex_styler import LaTeXStyler
    from config_manager import ConfigManager
    
    config_manager = ConfigManager(PROJECT_ROOT / 'config.json')
    styler = LaTeXStyler(config_manager.config)
    
    # 测试字体大小格式化
    formatted = styler.format_font_size('8pt')
    assert 'fontsize' in formatted
    
    formatted = styler.format_font_size('small')
    assert formatted == 'small'
    
    # 测试LaTeX特殊字符处理
    test_content = r'\setmainfont{Times New Roman}'
    result = styler.apply_font_config(test_content)
    assert result is not None
    
    print("✓ LaTeX样式配置器测试通过")

def test_file_processor():
    """测试文件处理器"""
    print("测试文件处理器...")
    
    from file_processor import FileProcessor
    from config_manager import ConfigManager
    
    config_manager = ConfigManager(PROJECT_ROOT / 'config.json')
    processor = FileProcessor(config_manager.config)
    
    # 测试文件扩展名检查
    test_file = Path('test.cpp')
    assert processor.is_supported_file(test_file) == True
    
    test_file = Path('test.txt')
    assert processor.is_supported_file(test_file) == False
    
    # 测试特殊字符转义
    escaped = processor.escape_latex_special_chars('test_file')
    assert escaped == 'test\\_file'
    
    # 测试文件名清理
    cleaned = processor.clean_filename_for_latex('test_file')
    assert cleaned == 'test\\_file'
    
    print("✓ 文件处理器测试通过")

def test_build_manager():
    """测试构建管理器"""
    print("测试构建管理器...")
    
    from build_manager import BuildManager
    from config_manager import ConfigManager
    
    config_manager = ConfigManager(PROJECT_ROOT / 'config.json')
    build_manager = BuildManager(
        config_manager.config,
        PROJECT_ROOT / 'build',
        PROJECT_ROOT / 'output'
    )
    
    # 测试构建信息获取
    build_info = build_manager.get_build_info()
    assert 'tex_file' in build_info
    assert 'pdf_file' in build_info
    assert 'layout' in build_info
    
    print("✓ 构建管理器测试通过")

def test_template_generator():
    """测试主模板生成器"""
    print("测试主模板生成器...")
    
    from generate_latex import TemplateGenerator
    
    # 测试生成器初始化
    generator = TemplateGenerator()
    assert generator.config is not None
    assert generator.config_manager is not None
    assert generator.styler is not None
    assert generator.file_processor is not None
    assert generator.build_manager is not None
    
    print("✓ 主模板生成器测试通过")

def main():
    """主测试函数"""
    print("=== 代码重构验证测试 ===\n")
    
    try:
        test_config_manager()
        test_latex_styler()
        test_file_processor()
        test_build_manager()
        test_template_generator()
        
        print(f"\n🎉 所有测试通过！")
        print("代码重构成功，所有模块正常工作。")
        return True
        
    except Exception as e:
        print(f"\n❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == '__main__':
    success = main()
    sys.exit(0 if success else 1)
