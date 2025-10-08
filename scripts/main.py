#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ACM 模板构建工具 - 统一 CLI 入口

一个优雅的 C++ 算法竞赛模板 PDF 生成工具
提供简洁的命令行界面和完善的错误处理
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, Any, Optional

__version__ = "2.0.0"
__author__ = "Aiza"

PROJECT_ROOT = Path(__file__).parent
CONFIG_FILE = PROJECT_ROOT / "config.json"
BUILD_DIR = PROJECT_ROOT / "build"
OUTPUT_DIR = PROJECT_ROOT / "output"

# 延迟导入，提升启动速度
def _lazy_template_generator():
    """延迟导入模板生成器，提升启动速度"""
    try:
        from scripts.generate_latex import TemplateGenerator
        return TemplateGenerator()
    except ImportError as e:
        _print_error(f"导入模板生成器失败: {e}")
        sys.exit(1)

def _load_config() -> Dict[str, Any]:
    """加载配置文件，提供友好的错误处理"""
    try:
        with open(CONFIG_FILE, "r", encoding="utf-8") as f:
            config = json.load(f)
            if not config:
                _print_warning("配置文件为空，将使用默认配置")
                return {}
            return config
    except FileNotFoundError:
        _print_error(f"配置文件未找到: {CONFIG_FILE}")
        return {}
    except json.JSONDecodeError as e:
        _print_error(f"配置文件格式错误: {e}")
        return {}
    except Exception as e:
        _print_error(f"读取配置文件失败: {e}")
        return {}

def _print_success(message: str) -> None:
    """打印成功消息"""
    print(f"✅ {message}")

def _print_error(message: str) -> None:
    """打印错误消息"""
    print(f"❌ {message}")

def _print_warning(message: str) -> None:
    """打印警告消息"""
    print(f"⚠️  {message}")

def _print_info(message: str) -> None:
    """打印信息消息"""
    print(f"ℹ️  {message}")

def cmd_build(args) -> int:
    """构建 PDF 文件"""
    try:
        gen = _lazy_template_generator()
        
        # 设置配置项
        gen.config_manager.set("layout", "landscape")  # 固定横版布局
        if args.output:
            gen.config_manager.set("output_filename", args.output)
        if args.template_dir:
            gen.config_manager.set("template_dir", args.template_dir)
        
        gen.config_manager.save_config()
        
        _print_info("开始构建 PDF...")
        success = gen.build()
        
        if success:
            _print_success("PDF 构建完成")
            if not args.no_open:
                _open_pdf(gen.config_manager.get("output_filename", "ACM_Templates"))
            return 0
        else:
            _print_error("PDF 构建失败")
            return 1
    except Exception as e:
        _print_error(f"构建过程出错: {e}")
        return 1

def cmd_clean(_args) -> int:
    """清理构建产生的临时文件"""
    try:
        gen = _lazy_template_generator()
        gen.clean_temp_files()
        
        # 清理根目录的额外临时文件
        temp_patterns = ["*.aux", "*.log", "*.toc", "*.out", "*.synctex.gz", "*.fdb_latexmk", "*.fls"]
        removed_count = 0
        
        for pattern in temp_patterns:
            for file_path in PROJECT_ROOT.glob(pattern):
                try:
                    file_path.unlink()
                    _print_info(f"清理文件: {file_path.name}")
                    removed_count += 1
                except OSError:
                    pass  # 忽略无法删除的文件
        
        if removed_count > 0:
            _print_success(f"额外清理了 {removed_count} 个临时文件")
        else:
            _print_info("没有发现需要清理的临时文件")
        
        return 0
    except Exception as e:
        _print_error(f"清理过程出错: {e}")
        return 1

def _open_pdf(output_name: str) -> bool:
    """尝试打开生成的 PDF 文件"""
    pdf_path = OUTPUT_DIR / f"{output_name}.pdf"
    
    if not pdf_path.exists():
        _print_warning("PDF 文件未找到，跳过自动打开")
        return False
    
    try:
        import os
        if hasattr(os, 'startfile'):  # Windows
            os.startfile(str(pdf_path))
        else:  # Linux/macOS
            import subprocess
            if sys.platform.startswith('darwin'):  # macOS
                subprocess.run(['open', str(pdf_path)], check=True)
            else:  # Linux
                subprocess.run(['xdg-open', str(pdf_path)], check=True)
        
        _print_success(f"已打开 PDF: {pdf_path.name}")
        return True
    except Exception as e:
        _print_warning(f"无法自动打开 PDF: {e}")
        _print_info(f"请手动打开: {pdf_path}")
        return False

def cmd_view(_args) -> int:
    """打开已生成的 PDF 文件"""
    config = _load_config()
    output_name = config.get("output_filename", "ACM_Templates")
    
    if _open_pdf(output_name):
        return 0
    else:
        return 1

def cmd_status(_args) -> int:
    """显示项目当前状态"""
    config = _load_config()
    
    print("\n📊 项目状态")
    print("=" * 50)
    
    if not config:
        _print_error("无法读取配置文件")
        return 1
    
    # 基本信息
    print(f"📁 模板目录: {config.get('build', {}).get('template_dir', config.get('template_dir', 'src'))}")
    output_filename = config.get('build', {}).get('output_filename', config.get('output_filename', 'ACM_Templates'))
    print(f"� 输出文件: {output_filename}.pdf")
    layout = config.get('build', {}).get('layout', config.get('layout', 'landscape'))
    print(f"� 布局模式: {layout}")
    font_size = config.get('code_style', {}).get('font_size', 'N/A')
    print(f"🔤 代码字体大小: {font_size}")
    
    # 统计代码文件
    template_dir_name = config.get('build', {}).get('template_dir', config.get('template_dir', 'src'))
    template_dir = PROJECT_ROOT / template_dir_name
    if template_dir.exists():
        supported_extensions = config.get('files', {}).get('supported_extensions', 
                                        config.get('supported_extensions', ['.cpp', '.hpp', '.h']))
        file_count = 0
        for ext in supported_extensions:
            file_count += len(list(template_dir.rglob(f'*{ext}')))
        print(f"📝 代码文件数: {file_count}")
    else:
        _print_warning(f"模板目录不存在: {template_dir}")
    
    # PDF 状态
    pdf_path = OUTPUT_DIR / f"{output_filename}.pdf"
    if pdf_path.exists():
        import datetime
        mtime = datetime.datetime.fromtimestamp(pdf_path.stat().st_mtime)
        print(f"📚 PDF 状态: 已生成 ({mtime.strftime('%Y-%m-%d %H:%M:%S')})")
    else:
        print("📚 PDF 状态: 未生成")
    
    # 检查依赖
    print("\n🔧 依赖检查:")
    try:
        import subprocess
        
        # 检查 Python
        python_version = subprocess.run(
            ['python', '--version'], 
            capture_output=True, text=True, timeout=5,
            encoding='utf-8', errors='ignore'
        )
        if python_version.returncode == 0:
            print(f"✅ Python: {python_version.stdout.strip()}")
        else:
            print("❌ Python: 未安装或不可用")
        
        # 检查 XeLaTeX
        xelatex_check = subprocess.run(
            ['xelatex', '--version'], 
            capture_output=True, text=True, timeout=10,
            encoding='utf-8', errors='ignore'
        )
        if xelatex_check.returncode == 0:
            print("✅ XeLaTeX: 已安装")
        else:
            print("❌ XeLaTeX: 未安装或不可用")
            
    except Exception as e:
        _print_warning(f"依赖检查失败: {e}")
    
    return 0

def cmd_minimal_config(args) -> int:
    """生成或打印最小化配置"""
    try:
        from scripts.config_analyzer import generate_minimal_config
        minimal_config = generate_minimal_config()
        config_text = json.dumps(minimal_config, indent=2, ensure_ascii=False)
        
        if args.write:
            with open(CONFIG_FILE, "w", encoding="utf-8") as f:
                f.write(config_text + "\n")
            _print_success("已写入最小化配置到 config.json")
        else:
            print("\n📋 最小化配置预览:")
            print("-" * 40)
            print(config_text)
            print("-" * 40)
            print("\n💡 使用 --write 参数可直接写入配置文件")
        
        return 0
    except Exception as e:
        _print_error(f"生成最小化配置失败: {e}")
        return 1

def cmd_analyze(_args) -> int:
    """分析配置使用情况"""
    try:
        from scripts.config_analyzer import analyze_config_usage
        print("\n🔍 配置使用情况分析:")
        print("-" * 50)
        analyze_config_usage()
        return 0
    except Exception as e:
        _print_error(f"配置分析失败: {e}")
        return 1

def cmd_validate_config(_args) -> int:
    """验证配置有效性和生效情况"""
    try:
        # 首先验证配置格式
        config = _load_config()
        if not config:
            _print_error("配置文件无效或不存在")
            return 1
        
        from scripts.config_manager import ConfigManager
        config_manager = ConfigManager(CONFIG_FILE)
        if not config_manager.validate():
            _print_error("配置验证失败")
            return 1
        
        _print_success("配置格式验证通过")
        
        # 验证配置实际生效情况
        print("\n" + "="*50)
        print("🔍 检查配置实际生效情况")
        print("="*50)
        
        import subprocess
        result = subprocess.run(
            ['python', 'scripts/validate_config_effectiveness.py'],
            capture_output=False,  # 直接显示输出
            cwd=str(PROJECT_ROOT),
            text=True
        )
        
        return result.returncode
            
    except Exception as e:
        _print_error(f"配置验证失败: {e}")
        return 1

def build_parser() -> argparse.ArgumentParser:
    """构建命令行参数解析器"""
    parser = argparse.ArgumentParser(
        prog="python main.py",
        description="ACM 算法竞赛模板 PDF 构建工具",
        epilog=f"Version {__version__} by {__author__}"
    )
    
    # 添加版本信息
    parser.add_argument(
        '--version', action='version', 
        version=f'%(prog)s {__version__}'
    )
    
    subparsers = parser.add_subparsers(dest="command", help="可用命令")
    
    # build 命令
    build_parser = subparsers.add_parser(
        "build", 
        help="构建 PDF 文件",
        description="从 C++ 模板文件构建 PDF 手册"
    )
    build_parser.add_argument(
        "--output", 
        help="指定输出文件名 (不含扩展名)",
        metavar="NAME"
    )
    build_parser.add_argument(
        "--template-dir", 
        help="指定模板目录 (默认: src)",
        metavar="DIR"
    )
    build_parser.add_argument(
        "--no-open", 
        action="store_true", 
        help="构建完成后不自动打开 PDF"
    )
    build_parser.set_defaults(func=cmd_build)
    
    # clean 命令
    clean_parser = subparsers.add_parser(
        "clean", 
        help="清理临时文件",
        description="删除构建过程中产生的临时文件"
    )
    clean_parser.set_defaults(func=cmd_clean)
    
    # view 命令
    view_parser = subparsers.add_parser(
        "view", 
        help="打开 PDF 文件",
        description="打开最近生成的 PDF 文件"
    )
    view_parser.set_defaults(func=cmd_view)
    
    # status 命令
    status_parser = subparsers.add_parser(
        "status", 
        help="查看项目状态",
        description="显示项目配置和构建状态信息"
    )
    status_parser.set_defaults(func=cmd_status)
    
    # minimal-config 命令
    minimal_parser = subparsers.add_parser(
        "minimal-config", 
        help="生成最小化配置",
        description="生成或打印经过精简的配置文件"
    )
    minimal_parser.add_argument(
        "--write", 
        action="store_true", 
        help="直接写入 config.json (会覆盖现有配置)"
    )
    minimal_parser.set_defaults(func=cmd_minimal_config)
    
    # analyze-config 命令
    analyze_parser = subparsers.add_parser(
        "analyze-config", 
        help="分析配置使用情况",
        description="分析当前配置文件中各项的使用情况"
    )
    analyze_parser.set_defaults(func=cmd_analyze)
    
    # validate-config 命令
    validate_parser = subparsers.add_parser(
        "validate-config", 
        help="验证配置有效性",
        description="验证配置格式正确性和实际生效情况"
    )
    validate_parser.set_defaults(func=cmd_validate_config)
    
    return parser

def main(argv: Optional[list] = None) -> int:
    """主程序入口点"""
    parser = build_parser()
    args = parser.parse_args(argv)
    
    if not hasattr(args, "func"):
        parser.print_help()
        return 1
    
    try:
        return args.func(args)
    except KeyboardInterrupt:
        _print_warning("操作被用户中断")
        return 130  # SIGINT 标准退出码
    except Exception as e:
        _print_error(f"未预期的错误: {e}")
        if "--debug" in (argv or sys.argv):
            import traceback
            traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(main())
