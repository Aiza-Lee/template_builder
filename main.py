#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ACM 模板生成器 - 主入口脚本
"""

import sys
import subprocess
import json
import shutil
from pathlib import Path

PROJECT_ROOT = Path(__file__).parent
SCRIPTS_DIR = PROJECT_ROOT / 'scripts'
CONFIG_FILE = PROJECT_ROOT / 'config.json'
BUILD_DIR = PROJECT_ROOT / 'build'
OUTPUT_DIR = PROJECT_ROOT / 'output'

def load_config():
    """加载配置文件"""
    try:
        with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"❌ 配置文件未找到: {CONFIG_FILE}")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"❌ 配置文件格式错误: {e}")
        sys.exit(1)

def save_config(config):
    """保存配置文件"""
    with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2, ensure_ascii=False)

def check_dependencies():
    """检查依赖"""
    # 检查xelatex
    try:
        result = subprocess.run(['xelatex', '--version'], 
                              capture_output=True, text=True, check=True, encoding='utf-8')
        print("✓ xelatex 已安装")
    except UnicodeDecodeError:
        # 如果编码有问题，尝试使用其他编码
        try:
            result = subprocess.run(['xelatex', '--version'], 
                                  capture_output=True, check=True, encoding='gbk')
            print("✓ xelatex 已安装")
        except:
            try:
                result = subprocess.run(['xelatex', '--version'], 
                                      capture_output=True, check=True)
                print("✓ xelatex 已安装")
            except:
                print("❌ xelatex 未安装，请先安装LaTeX环境")
                return False
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("❌ xelatex 未安装，请先安装LaTeX环境")
        return False
    
    # 检查python
    print(f"✓ python 已安装 (版本: {sys.version.split()[0]})")
    return True

def build_pdf(layout=None):
    """构建PDF"""
    print("🔨 正在构建 PDF...")
    print("=== ACM 模板 LaTeX/PDF 生成器 ===")
    
    # 加载配置并固定为 landscape 布局
    config = load_config()
    config['layout'] = 'landscape'
    save_config(config)
    print("布局模式: landscape")
    
    print()
    
    # 检查依赖
    if not check_dependencies():
        return False
    
    # 确保输出目录存在
    BUILD_DIR.mkdir(exist_ok=True)
    OUTPUT_DIR.mkdir(exist_ok=True)
    
    # 调用LaTeX生成器
    try:
        from scripts.generate_latex import TemplateGenerator
        generator = TemplateGenerator()
        
        print("正在生成 LaTeX 文件...")
        latex_success = generator.generate_latex()
        
        if latex_success:
            # 编译PDF
            pdf_success = generator.compile_pdf()
            
            if pdf_success:
                # 清理临时文件
                generator.clean_temp_files()
                
                print("🎉 构建成功！")
                
                # 询问是否打开PDF
                try:
                    response = input("是否打开 PDF? (y/n): ").strip().lower()
                    if response in ['y', 'yes']:
                        pdf_path = OUTPUT_DIR / f"{config.get('output_filename', 'ACM_Templates')}.pdf"
                        if pdf_path.exists():
                            import os
                            os.startfile(str(pdf_path))
                            print(f"📖 打开 PDF: {pdf_path.name}")
                        else:
                            print("❌ PDF文件未找到")
                except KeyboardInterrupt:
                    print("\n跳过打开PDF")
                
                return True
            else:
                print("❌ PDF编译失败")
                return False
        else:
            print("❌ LaTeX生成失败")
            return False
            
    except Exception as e:
        print(f"❌ 构建过程中出现错误: {e}")
        return False

def clean_files():
    """清理临时文件"""
    print("🧹 正在清理临时文件...")
    
    patterns = ['*.aux', '*.log', '*.out', '*.toc', '*.fls', '*.fdb_latexmk', '*.synctex.gz']
    cleaned_count = 0
    
    for pattern in patterns:
        for file_path in PROJECT_ROOT.glob(pattern):
            try:
                file_path.unlink()
                print(f"清理: {file_path.name}")
                cleaned_count += 1
            except Exception as e:
                print(f"无法清理 {file_path.name}: {e}")
    
    print(f"✓ 清理了 {cleaned_count} 个临时文件")

def view_pdf():
    """查看PDF"""
    config = load_config()
    pdf_path = OUTPUT_DIR / f"{config.get('output_filename', 'ACM_Templates')}.pdf"
    
    if pdf_path.exists():
        import os
        os.startfile(str(pdf_path))
        print(f"📖 打开 PDF: {pdf_path.name}")
    else:
        print("❌ PDF文件未找到，请先生成PDF")

def show_status():
    """显示项目状态"""
    print("📊 项目状态")
    print("=" * 40)
    
    config = load_config()
    print(f"📁 模板目录: {config.get('template_dir', 'src')}")
    print(f"📄 输出文件名: {config.get('output_filename', 'ACM_Templates')}")
    print(f"🎨 当前布局: {config.get('layout', 'landscape')}")
    print(f"🔤 代码字体: {config.get('fonts', {}).get('code', 'ttfamily')}")
    print(f"📏 字体大小: {config.get('code_style', {}).get('font_size', 'small')}")
    
    # 检查文件状态
    src_dir = PROJECT_ROOT / config.get('template_dir', 'src')
    if src_dir.exists():
        cpp_files = list(src_dir.rglob('*.cpp'))
        print(f"📂 模板文件数量: {len(cpp_files)}")
    
    pdf_path = OUTPUT_DIR / f"{config.get('output_filename', 'ACM_Templates')}.pdf"
    if pdf_path.exists():
        import datetime
        mtime = datetime.datetime.fromtimestamp(pdf_path.stat().st_mtime)
        print(f"📕 PDF状态: 已生成 ({mtime.strftime('%Y-%m-%d %H:%M:%S')})")
    else:
        print("📕 PDF状态: 未生成")

def setup_project():
    """初始化项目"""
    print("🛠️ 正在初始化项目...")
    
    # 创建必要的目录
    directories = [BUILD_DIR, OUTPUT_DIR, SCRIPTS_DIR / '../docs']
    for dir_path in directories:
        dir_path.mkdir(exist_ok=True)
        print(f"✓ 创建目录: {dir_path.relative_to(PROJECT_ROOT)}")
    
    # 检查配置文件
    if CONFIG_FILE.exists():
        print(f"✓ 配置文件已存在: {CONFIG_FILE.name}")
    else:
        print(f"❌ 配置文件不存在: {CONFIG_FILE.name}")
        return False
    
    print("✓ 项目初始化完成")
    return True

def main():
    """主入口函数"""
    if len(sys.argv) < 2:
        print("ACM 模板 LaTeX/PDF 生成器")
        print()
        print("用法:")
        print("  python main.py build                    # 生成PDF（横版布局）")
        print("  python main.py setup                    # 初始化项目")
        print("  python main.py clean                    # 清理临时文件")
        print("  python main.py view                     # 查看PDF")
        print("  python main.py status                   # 查看状态")
        print()
        print("示例:")
        print("  python main.py build")
        return
    
    command = sys.argv[1].lower()
    
    try:
        if command == 'build':
            # 固定使用 landscape 布局
            success = build_pdf('landscape')
            sys.exit(0 if success else 1)
            
        elif command == 'setup':
            success = setup_project()
            sys.exit(0 if success else 1)
            
        elif command == 'clean':
            clean_files()
            
        elif command == 'view':
            view_pdf()
            
        elif command == 'status':
            show_status()
            
        else:
            print(f"❌ 未知命令: {command}")
            sys.exit(1)
            
    except KeyboardInterrupt:
        print("\n操作已取消")
        sys.exit(1)
    except Exception as e:
        print(f"错误: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()
