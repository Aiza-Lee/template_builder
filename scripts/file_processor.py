#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
文件处理器
负责遍历模板文件、读取内容、生成LaTeX代码结构
"""

import os
from pathlib import Path
from typing import List, Dict, Any

class FileProcessor:
    """文件处理器类"""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.supported_extensions = config.get('files', {}).get('supported_extensions', ['.cpp'])
        self.exclude_patterns = config.get('files', {}).get('exclude_patterns', [])
    
    def should_exclude_file(self, filepath: Path) -> bool:
        """检查文件是否应该被排除"""
        filename = filepath.name
        
        for pattern in self.exclude_patterns:
            if pattern.startswith('*'):
                if filename.endswith(pattern[1:]):
                    return True
            elif pattern.endswith('*'):
                if filename.startswith(pattern[:-1]):
                    return True
            elif pattern in filename:
                return True
        
        return False
    
    def get_file_description(self, filepath: Path) -> str:
        """从文件开头的注释中提取描述，过滤掉文档注释"""
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                lines = f.readlines()[:15]  # 读取前15行
                descriptions = []
                
                for line in lines:
                    line = line.strip()
                    if line.startswith('//') and len(line) > 3:
                        desc = line[2:].strip()
                        
                        # 过滤掉包含特定关键字的行
                        exclude_keywords = [
                            '#include', 'using', 'namespace', 'define',
                            '@brief', '@param', '@return', '@author', 
                            '@date', '@version', '@todo', '@note',
                            '@warning', '@see', '@since', '@deprecated',
                            '@file', '@class', '@struct', '@enum'
                        ]
                        
                        # 检查是否包含要排除的关键字
                        should_exclude = any(keyword in desc.lower() for keyword in exclude_keywords)
                        
                        # 过滤掉纯粹的文档标签行（如 "/// @brief" 开头的行）
                        if desc.startswith('@'):
                            should_exclude = True
                        
                        if desc and not should_exclude:
                            descriptions.append(desc)
                            if len(descriptions) >= 2:  # 最多取两行描述
                                break
                
                return ' '.join(descriptions) if descriptions else ""
        except Exception:
            return ""
    
    def read_file_content(self, filepath: Path) -> str:
        """读取文件内容"""
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
            
            # 标准化换行符
            content = content.replace('\r\n', '\n').replace('\r', '\n')
            return content
        except Exception as e:
            print(f"警告: 无法读取文件 {filepath}: {e}")
            return f"// 无法读取文件: {str(e)}"
    
    def escape_latex_special_chars(self, text: str) -> str:
        """转义LaTeX特殊字符"""
        replacements = {
            '\\': '\\textbackslash{}',
            '{': '\\{',
            '}': '\\}',
            '$': '\\$',
            '&': '\\&',
            '%': '\\%',
            '#': '\\#',
            '^': '\\textasciicircum{}',
            '_': '\\_',
            '~': '\\textasciitilde{}'
        }
        
        for char, replacement in replacements.items():
            text = text.replace(char, replacement)
        
        return text
    
    def clean_filename_for_latex(self, filename: str) -> str:
        """清理文件名以适应LaTeX"""
        return filename.replace('_', '\\_')
    
    def get_sorted_items(self, directory: Path) -> List[Path]:
        """获取排序后的目录项（目录在前，文件在后，按名称排序）"""
        try:
            items = list(directory.iterdir())
            return sorted(items, key=lambda x: (x.is_file(), x.name.lower()))
        except Exception:
            return []
    
    def is_supported_file(self, filepath: Path) -> bool:
        """检查文件是否为支持的代码文件"""
        return filepath.suffix.lower() in self.supported_extensions
    
    def walk_templates(self, root: Path, level: int = 1) -> str:
        """递归遍历目录，生成章节结构和代码块"""
        tex = ''
        items = self.get_sorted_items(root)
        
        for item in items:
            if self.should_exclude_file(item):
                continue
            
            if item.is_dir():
                tex += self._process_directory(item, level)
            elif self.is_supported_file(item):
                tex += self._process_code_file(item)
        
        return tex
    
    def _process_directory(self, directory: Path, level: int) -> str:
        """处理目录，生成相应的章节标题"""
        section_name = self.clean_filename_for_latex(directory.name)
        
        if level == 1:
            tex = f"\\section{{{section_name}}}\n\n"
        elif level == 2:
            tex = f"\\subsection{{{section_name}}}\n\n"
        else:
            tex = f"\\subsubsection{{{section_name}}}\n\n"
        
        # 递归处理子目录
        tex += self.walk_templates(directory, level + 1)
        return tex
    
    def _process_code_file(self, filepath: Path) -> str:
        """处理代码文件，生成LaTeX代码块"""
        filename = self.clean_filename_for_latex(filepath.name)
        description = self.get_file_description(filepath)
        
        # 将文件名作为 subsubsection 添加到目录中
        tex = f"\\subsubsection{{{filename}}}\n"
        
        # 添加文件描述（如果有的话）
        if description:
            escaped_desc = self.escape_latex_special_chars(description)
            tex += f"\\textit{{{escaped_desc}}}\\par\n"
            tex += "\\vspace{0.1em}\n"
        
        # 读取代码内容
        code = self.read_file_content(filepath)
        
        # 生成代码块
        tex += "\\begin{lstlisting}\n"
        tex += code
        if not code.endswith('\n'):
            tex += '\n'
        tex += "\\end{lstlisting}\n\n"
        
        # 根据布局调整间距 - 更紧凑
        layout = self.config.get('build', {}).get('layout', 'landscape')
        if layout == 'landscape':
            tex += "\\vspace{0.1em}\n\n"
        else:
            tex += "\\vspace{0.3em}\n\n"
        
        return tex
