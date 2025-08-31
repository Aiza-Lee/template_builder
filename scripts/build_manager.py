#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
构建管理器
负责管理整个构建流程，包括依赖检查、编译、清理等
"""

import os
import sys
import subprocess
import shutil
from pathlib import Path
from typing import List, Dict, Any

class BuildManager:
    """构建管理器类"""
    
    def __init__(self, config: Dict[str, Any], build_dir: Path, output_dir: Path):
        self.config = config
        self.build_dir = build_dir
        self.output_dir = output_dir
        
        # 确保目录存在
        self.build_dir.mkdir(exist_ok=True)
        self.output_dir.mkdir(exist_ok=True)
    
    def check_dependencies(self) -> bool:
        """检查必要的依赖"""
        dependencies = {
            'xelatex': 'xelatex --version',
            'python': 'python --version'
        }
        
        missing = []
        for name, cmd in dependencies.items():
            if not self._check_command(cmd):
                missing.append(name)
            else:
                print(f"✓ {name} 已安装")
        
        if missing:
            self._print_missing_dependencies(missing)
            return False
        
        return True
    
    def _check_command(self, cmd: str) -> bool:
        """检查命令是否可用"""
        try:
            result = subprocess.run(
                cmd.split(),
                capture_output=True,
                text=True,
                timeout=10,
                encoding='utf-8',
                errors='ignore'
            )
            return result.returncode == 0
        except (subprocess.TimeoutExpired, FileNotFoundError, UnicodeDecodeError):
            return False
    
    def _print_missing_dependencies(self, missing: List[str]) -> None:
        """打印缺失依赖的安装建议"""
        print(f"\n缺少以下依赖: {', '.join(missing)}")
        print("\n建议使用 scoop 安装:")
        
        for dep in missing:
            if dep == 'xelatex':
                print("  scoop install latex")
            elif dep == 'python':
                print("  scoop install python")
    
    def compile_pdf(self, tex_filename: str) -> bool:
        """编译PDF"""
        print("正在编译 PDF...")
        
        tex_file = self.build_dir / tex_filename
        if not tex_file.exists():
            print(f"错误: LaTeX 文件不存在: {tex_file}")
            return False
        
        # 切换到build目录进行编译
        original_cwd = os.getcwd()
        os.chdir(self.build_dir)
        
        try:
            # 编译两次以生成正确的目录和引用
            for i in range(2):
                print(f"第 {i+1} 次编译...")
                
                if not self._run_xelatex(tex_file.name):
                    if i == 1:  # 第二次编译失败才返回False
                        return False
            
            # 移动PDF到output目录
            return self._move_output_pdf(tex_filename)
            
        finally:
            os.chdir(original_cwd)
    
    def _run_xelatex(self, tex_filename: str) -> bool:
        """运行xelatex编译"""
        try:
            result = subprocess.run([
                'xelatex', 
                '-interaction=nonstopmode',
                '-halt-on-error',
                tex_filename
            ], capture_output=True, text=True, timeout=300, 
               encoding='utf-8', errors='ignore')
            
            if result.returncode != 0:
                self._print_compile_errors(result.stdout)
                return False
            
            return True
            
        except subprocess.TimeoutExpired:
            print("编译超时")
            return False
        except FileNotFoundError:
            print("错误: 找不到 xelatex 命令")
            print("请使用以下命令安装: scoop install latex")
            return False
    
    def _print_compile_errors(self, stdout: str) -> None:
        """打印编译错误信息"""
        print("编译出错:")
        if stdout:
            lines = stdout.split('\n')
            error_found = False
            
            for line in lines:
                if 'error' in line.lower() or 'failed' in line.lower() or line.startswith('!'):
                    print(f"  {line}")
                    error_found = True
                elif error_found and line.strip().startswith('l.'):
                    print(f"  {line}")
                    error_found = False
    
    def _move_output_pdf(self, tex_filename: str) -> bool:
        """移动PDF文件到输出目录"""
        output_filename = self.config.get('build', {}).get('output_filename', 'ACM_Templates')
        pdf_file = self.build_dir / f"{output_filename}.pdf"
        output_pdf = self.output_dir / f"{output_filename}.pdf"
        
        if pdf_file.exists():
            if output_pdf.exists():
                output_pdf.unlink()
            
            shutil.move(str(pdf_file), str(output_pdf))
            print(f"✓ PDF 编译成功: {output_pdf}")
            return True
        else:
            print("✗ PDF 编译失败")
            return False
    
    def clean_temp_files(self) -> None:
        """清理临时文件"""
        if not self.config.get('build', {}).get('clean_after_build', True):
            return
        
        temp_extensions = ['.aux', '.log', '.out', '.fdb_latexmk', '.fls', '.toc', '.pyg']
        temp_dirs = ['_minted-*']
        
        print("清理临时文件...")
        cleaned = 0
        
        # 清理临时文件
        for ext in temp_extensions:
            for file in self.build_dir.glob(f'*{ext}'):
                try:
                    file.unlink()
                    print(f"清理: {file.name}")
                    cleaned += 1
                except Exception:
                    pass
        
        # 清理临时目录
        for pattern in temp_dirs:
            for dir_path in self.build_dir.glob(pattern):
                if dir_path.is_dir():
                    try:
                        shutil.rmtree(dir_path)
                        print(f"清理目录: {dir_path.name}")
                        cleaned += 1
                    except Exception:
                        pass
        
        if cleaned > 0:
            print(f"✓ 清理了 {cleaned} 个临时文件")
    
    def get_build_info(self) -> Dict[str, Any]:
        """获取构建信息"""
        output_filename = self.config.get('build', {}).get('output_filename', 'ACM_Templates')
        
        return {
            'tex_file': self.build_dir / f"{output_filename}.tex",
            'pdf_file': self.output_dir / f"{output_filename}.pdf",
            'layout': self.config.get('build', {}).get('layout', 'landscape'),
            'layout_name': '横版双列' if self.config.get('build', {}).get('layout') == 'landscape' else '竖版单列'
        }
