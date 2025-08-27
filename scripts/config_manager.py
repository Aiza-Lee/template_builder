#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
配置管理器
负责加载、验证和管理所有配置选项
"""

import json
from pathlib import Path
from typing import Dict, Any, Optional

class ConfigManager:
    """配置管理器类"""
    
    # 默认配置
    DEFAULT_CONFIG = {
        "layout": "landscape",  # 固定为 landscape 布局
        "template_dir": "src",
        "output_filename": "ACM_Templates",
        "clean_after_build": True,
        "page_margins": {
            "landscape": {"left": "1.5cm", "right": "1.5cm", "top": "2cm", "bottom": "2cm"}
        },
        "supported_extensions": [".cpp", ".hpp", ".h", ".c", ".cc", ".cxx"],
        "exclude_patterns": ["*.tmp", "*.bak", "*~", "*.swp"],
        "fonts": {
            "main": "Times New Roman",
            "cjk_main": "SimHei",
            "cjk_sans": "SimHei", 
            "cjk_mono": "SimSun",
            "code": "Fira Code"
        },
        "colors": {
            "section": "blue!70!black",
            "subsection": "green!60!black",
            "subsubsection": "orange!80!black",
            "code_background": "gray!5",
            "code_keyword": "blue",
            "code_comment": "green!50!black",
            "code_string": "red",
            "code_number": "gray"
        },
        "code_style": {
            "font_size": "8pt",
            "line_numbers": True,
            "frame_style": "single",
            "frame_round": True,
            "break_lines": True,
            "tab_size": 2,
            "columns": "flexible",
            "base_width": "0.6em",
            "scale_factor": 0.85,
            "line_spacing": "1.0",
            "left_margin": "0pt",
            "right_margin": "0pt",
            "above_skip": "0.5em",
            "below_skip": "0.5em"
        }
    }
    
    def __init__(self, config_path: Optional[Path] = None):
        self.config_path = config_path
        self.config = self._load_config()
    
    def _load_config(self) -> Dict[str, Any]:
        """加载配置文件"""
        config = self.DEFAULT_CONFIG.copy()
        
        # 尝试从指定路径加载
        if self.config_path and self.config_path.exists():
            try:
                with open(self.config_path, 'r', encoding='utf-8') as f:
                    user_config = json.load(f)
                    config = self._merge_config(config, user_config)
            except (json.JSONDecodeError, IOError) as e:
                print(f"警告: 无法加载配置文件 {self.config_path}: {e}")
                print("使用默认配置")
        
        return config
    
    def _merge_config(self, default: Dict[str, Any], user: Dict[str, Any]) -> Dict[str, Any]:
        """递归合并配置"""
        for key, value in user.items():
            if key in default and isinstance(default[key], dict) and isinstance(value, dict):
                default[key] = self._merge_config(default[key], value)
            else:
                default[key] = value
        return default
    
    def save_config(self) -> bool:
        """保存配置到文件"""
        if not self.config_path:
            return False
            
        try:
            with open(self.config_path, 'w', encoding='utf-8') as f:
                json.dump(self.config, f, indent=2, ensure_ascii=False)
            return True
        except IOError as e:
            print(f"错误: 无法保存配置文件: {e}")
            return False
    
    def get(self, key: str, default: Any = None) -> Any:
        """获取配置值"""
        keys = key.split('.')
        value = self.config
        
        for k in keys:
            if isinstance(value, dict) and k in value:
                value = value[k]
            else:
                return default
        
        return value
    
    def set(self, key: str, value: Any) -> None:
        """设置配置值"""
        keys = key.split('.')
        config = self.config
        
        for k in keys[:-1]:
            if k not in config:
                config[k] = {}
            config = config[k]
        
        config[keys[-1]] = value
    
    def validate(self) -> bool:
        """验证配置的有效性"""
        try:
            # 验证布局（现在固定为 landscape）
            layout = self.get('layout')
            if layout != 'landscape':
                print(f"警告: 布局已固定为 landscape，忽略设置: {layout}")
                self.set('layout', 'landscape')
            
            # 验证文件扩展名
            extensions = self.get('supported_extensions', [])
            if not isinstance(extensions, list) or not extensions:
                print("错误: supported_extensions 必须是非空列表")
                return False
            
            # 验证字体大小
            font_size = self.get('code_style.font_size', 'small')
            if not self._is_valid_font_size(font_size):
                print(f"警告: 字体大小可能无效: {font_size}")
            
            return True
            
        except Exception as e:
            print(f"配置验证失败: {e}")
            return False
    
    def _is_valid_font_size(self, font_size: str) -> bool:
        """验证字体大小是否有效"""
        # 数字+pt格式
        if font_size.endswith('pt'):
            try:
                float(font_size[:-2])
                return True
            except ValueError:
                pass
        
        # 纯数字
        try:
            float(font_size)
            return True
        except ValueError:
            pass
        
        # LaTeX预定义大小
        predefined = ['tiny', 'scriptsize', 'footnotesize', 'small', 
                     'normalsize', 'large', 'Large', 'LARGE', 'huge', 'Huge']
        return font_size in predefined
