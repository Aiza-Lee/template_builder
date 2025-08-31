#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ACM 模板 LaTeX/PDF 生成器 - 重构版
支持横版双列和竖版单列布局
模块化的项目结构，统一模板系统
"""

import sys
import argparse
from pathlib import Path

# 添加脚本目录到Python路径，以便导入自定义模块
sys.path.insert(0, str(Path(__file__).parent))

from config_manager import ConfigManager
from file_processor import FileProcessor
from build_manager import BuildManager
from template_processor import LaTeXTemplateProcessor

# 项目结构路径
PROJECT_ROOT = Path(__file__).parent.parent
TEMPLATE_ROOT = PROJECT_ROOT / 'src'
TEMPLATES_DIR = PROJECT_ROOT / 'templates'
BUILD_DIR = PROJECT_ROOT / 'build'
OUTPUT_DIR = PROJECT_ROOT / 'output'
CONFIG_FILE = PROJECT_ROOT / 'config.json'

INSERT_MARK = '%__AUTO_INSERTED_CONTENT__'
UNIFIED_PLACEHOLDER = '{{AUTO_INSERTED_CONTENT}}'

class TemplateGenerator:
    """主模板生成器类 - 重构版，支持统一模板系统"""
    
    def __init__(self, config_path=None):
        # 初始化配置管理器
        config_file = Path(config_path) if config_path else CONFIG_FILE
        self.config_manager = ConfigManager(config_file)
        
        # 验证配置
        if not self.config_manager.validate():
            raise ValueError("配置验证失败")
        
        # 初始化各个组件
        self.file_processor = FileProcessor(self.config_manager.config)
        self.build_manager = BuildManager(
            self.config_manager.config,
            BUILD_DIR,
            OUTPUT_DIR
        )
        
        # 初始化模板处理器（如果启用统一模板）
        if self.config_manager.get('build.use_unified_template', False):
            self.template_processor = LaTeXTemplateProcessor(self.config_manager.config)
        else:
            self.template_processor = None
    
    @property
    def config(self):
        """获取配置对象（向后兼容）"""
        return self.config_manager.config
    
    def generate_latex(self):
        """生成LaTeX文件 - 支持统一模板和传统模板"""
        print("正在生成 LaTeX 文件...")
        
        # 生成内容
        insert_content = self._generate_content()
        if not insert_content:
            print("警告: 没有找到任何模板文件")
        
        # 根据配置选择模板处理方式
        if self.config_manager.get('build.use_unified_template', False):
            final_content = self._process_unified_template(insert_content)
        else:
            # 传统模板处理方式
            template_content = self._load_legacy_template()
            if not template_content:
                return False
            # 注意：在统一模板系统中，样式处理由模板处理器完成
            # 传统模式下直接插入内容，不再应用单独的样式处理
            final_content = self._insert_content(template_content, insert_content)
        
        if not final_content:
            return False
            
        # 保存文件
        return self._save_latex_file(final_content)
    
    def _process_unified_template(self, insert_content):
        """处理统一模板系统"""
        print("使用统一模板系统...")
        
        # 加载统一模板
        unified_template_name = self.config_manager.get('build.unified_template_name', 'unified_template.tex')
        template_file = TEMPLATES_DIR / unified_template_name
        
        if not template_file.exists():
            print(f"错误: 找不到统一模板文件 {template_file}")
            return None
        
        try:
            template_content = self.template_processor.load_template(template_file)
            
            # 验证模板
            validation_result = self.template_processor.validate_template(template_content)
            if not validation_result['valid']:
                print("模板验证失败:")
                for msg in validation_result['validation_messages']:
                    print(f"  - {msg}")
                return None
            
            print("✓ 模板验证通过")
            
            # 处理模板，替换所有占位符
            final_content = self.template_processor.process_template(template_content, insert_content)
            return final_content
            
        except Exception as e:
            print(f"错误: 处理统一模板时发生错误: {e}")
            return None
    
    def _load_legacy_template(self):
        """加载传统LaTeX模板文件（向后兼容）"""
        layout = self.config_manager.get('layout', 'landscape')
        template_file = TEMPLATES_DIR / f'{layout}_template.tex'
        
        if not template_file.exists():
            print(f"错误: 找不到模板文件 {template_file}")
            return None
        
        try:
            with open(template_file, 'r', encoding='utf-8') as f:
                return f.read()
        except Exception as e:
            print(f"错误: 无法读取模板文件: {e}")
            return None
    
    def _generate_content(self):
        """生成文档内容"""
        template_root = PROJECT_ROOT / self.config_manager.get('build.template_dir', 'src')
        
        if not template_root.exists():
            print(f"错误: 模板目录 {template_root} 不存在")
            print("请确保你的 C++ 模板文件放在 'src' 目录下")
            return ""
        
        print("正在遍历模板文件...")
        return self.file_processor.walk_templates(template_root)
    
    def _insert_content(self, template_content, insert_content):
        """将生成的内容插入模板"""
        if INSERT_MARK in template_content:
            return template_content.replace(INSERT_MARK, insert_content + INSERT_MARK)
        else:
            print(f"警告: 在模板文件中找不到插入标记 {INSERT_MARK}")
            return template_content
    
    def _save_latex_file(self, content):
        """保存LaTeX文件"""
        output_filename = self.config_manager.get('output_filename', 'ACM_Templates')
        output_tex = BUILD_DIR / f"{output_filename}.tex"
        
        try:
            with open(output_tex, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"✓ LaTeX 文件已生成: {output_tex}")
            return True
        except Exception as e:
            print(f"错误: 无法写入 LaTeX 文件: {e}")
            return False
    
    def compile_pdf(self):
        """编译PDF"""
        output_filename = self.config_manager.get('output_filename', 'ACM_Templates')
        return self.build_manager.compile_pdf(f"{output_filename}.tex")
    
    def clean_temp_files(self):
        """清理临时文件"""
        self.build_manager.clean_temp_files()
    
    def build(self):
        """完整的构建流程"""
        build_info = self.build_manager.get_build_info()
        
        print("=== ACM 模板 LaTeX/PDF 生成器 (重构版) ===")
        print(f"布局模式: {build_info['layout_name']}")
        print()
        
        # 检查依赖
        if not self.build_manager.check_dependencies():
            print("\n请先安装必要的依赖，然后重新运行此脚本。")
            return False
        
        print()
        
        # 生成LaTeX
        if not self.generate_latex():
            return False
        
        # 编译PDF
        if not self.compile_pdf():
            return False
        
        # 清理临时文件
        self.clean_temp_files()
        
        print(f"\n🎉 完成! PDF 文件已生成: {build_info['pdf_file']}")
        print(f"   LaTeX 源文件: {build_info['tex_file']}")
        
        return True
def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='ACM 模板 LaTeX/PDF 生成器')
    parser.add_argument('--config', help='配置文件路径')
    parser.add_argument('--output', help='输出文件名（不含扩展名）')
    parser.add_argument('--template-dir', help='模板目录名')
    
    args = parser.parse_args()
    
    try:
        generator = TemplateGenerator(args.config)
        
        # 应用命令行参数
        if args.output:
            generator.config_manager.set('output_filename', args.output)
        if args.template_dir:
            generator.config_manager.set('template_dir', args.template_dir)
        
        return generator.build()
        
    except Exception as e:
        print(f"错误: {e}")
        return False


if __name__ == '__main__':
    success = main()
    sys.exit(0 if success else 1)
