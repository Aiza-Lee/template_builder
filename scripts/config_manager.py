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
    """配置管理器类 - 仅依赖用户配置文件"""
    
    def __init__(self, config_path: Optional[Path] = None):
        self.config_path = config_path
        self.config = self._load_config()
    
    def _load_config(self) -> Dict[str, Any]:
        """加载配置文件"""
        if not self.config_path or not self.config_path.exists():
            raise FileNotFoundError(f"配置文件不存在: {self.config_path}")
        
        try:
            with open(self.config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
                if not config:
                    raise ValueError("配置文件为空")
                return config
        except json.JSONDecodeError as e:
            raise ValueError(f"配置文件格式错误: {e}")
        except IOError as e:
            raise IOError(f"无法读取配置文件: {e}")
    
    def _merge_config(self, default: Dict[str, Any], user: Dict[str, Any]) -> Dict[str, Any]:
        """递归合并配置（已移除，保留方法以防代码引用）"""
        return user  # 直接返回用户配置，不再合并默认配置
    
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
            # 验证必需的顶级配置模块
            required_modules = ['project', 'build', 'files', 'page_layout', 'typography', 'code_style', 'table_of_contents']
            for module in required_modules:
                if module not in self.config:
                    print(f"错误: 缺少必需的配置模块: {module}")
                    return False
            
            # 验证布局（现在固定为 landscape）
            layout = self.get('build.layout')
            if layout != 'landscape':
                print(f"警告: 布局必须为 landscape，当前设置: {layout}")
                return False
            
            # 验证文件扩展名
            extensions = self.get('files.supported_extensions', [])
            if not isinstance(extensions, list) or not extensions:
                print("错误: files.supported_extensions 必须是非空列表")
                return False
            
            # 验证字体大小
            font_size = self.get('code_style.appearance.font_size')
            if font_size and not self._is_valid_font_size(font_size):
                print(f"警告: 字体大小可能无效: {font_size}")
            
            # 验证项目信息
            title = self.get('project.title')
            author = self.get('project.author')
            if not title or not author:
                print("警告: 项目信息不完整")
            
            print("✓ 配置验证通过")
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
