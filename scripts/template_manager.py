#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""模板管理工具 (精简版)
提供最小功能: list / validate / placeholders / create-sample / migrate / config。
"""

from __future__ import annotations
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from template_processor import LaTeXTemplateProcessor  # type: ignore
from config_manager import ConfigManager  # type: ignore

ROOT = Path(__file__).parent.parent
TEMPLATES = ROOT / "templates"
CONFIG = ROOT / "config.json"


class TemplateManager:
    def __init__(self):
        self.cfg = ConfigManager(CONFIG)
        self.proc = LaTeXTemplateProcessor(self.cfg.config)

    def list(self):
        cur = self.cfg.get("unified_template_name", "unified_template.tex")
        files = sorted(TEMPLATES.glob("*.tex"))
        if not files:
            print("(空)"); return
        for f in files:
            mark = " *" if f.name == cur else ""
            print(f" - {f.name}{mark}")

    def validate(self, name: str | None):
        if not name:
            name = self.cfg.get("unified_template_name", "unified_template.tex")
        path = TEMPLATES / name
        if not path.exists():
            print("不存在:", path); return False
        content = self.proc.load_template(path)
        result = self.proc.validate_template(content)
        print("结果:", "通过" if result["valid"] else "失败")
        for msg in result["validation_messages"]:
            print(" -", msg)
        return result["valid"]

    def placeholders(self):
        for p in self.proc.get_available_placeholders():
            print("{{" + p + "}}")

    def create_sample(self, name: str):
        sample = ("\\documentclass[{DOCUMENT_CLASS_OPTIONS}]{{ctexart}}\n"
                  "\\begin{document}\n"
                  "{{AUTO_INSERTED_CONTENT}}\n"
                  "\\end{document}\n")
        out = TEMPLATES / name
        out.write_text(sample, encoding="utf-8")
        print("已创建:", out)
        self.validate(name)

    def migrate(self):
        if self.cfg.get("use_unified_template", True):
            print("已启用统一模板")
            return
        backup = CONFIG.with_name(CONFIG.stem + "_backup.json")
        backup.write_text(CONFIG.read_text(encoding="utf-8"), encoding="utf-8")
        data = self.cfg.config
        data["use_unified_template"] = True
        data.setdefault("unified_template_name", "unified_template.tex")
        CONFIG.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
        print("已迁移, 备份于", backup.name)

    def config_preview(self):
        print("统一模板:", "启用" if self.cfg.get("use_unified_template", True) else "禁用")
        print("模板文件:", self.cfg.get("unified_template_name", "unified_template.tex"))
        print("输出文件:", self.cfg.get("output_filename", "ACM_Templates"))
        print("代码字号:", self.cfg.get("code_style.font_size"))


def _parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="python -m scripts.template_manager", description="模板管理")
    sp = p.add_subparsers(dest="cmd")
    sp.add_parser("list")
    v = sp.add_parser("validate"); v.add_argument("--template")
    sp.add_parser("placeholders")
    cs = sp.add_parser("create-sample"); cs.add_argument("--name", default="sample_template.tex")
    sp.add_parser("migrate")
    sp.add_parser("config")
    return p


def main(argv=None):  # noqa: D401
    args = _parser().parse_args(argv)
    if not args.cmd:
        _parser().print_help(); return 0
    tm = TemplateManager()
    match args.cmd:
        case "list": tm.list()
        case "validate": tm.validate(args.template)
        case "placeholders": tm.placeholders()
        case "create-sample": tm.create_sample(args.name)
        case "migrate": tm.migrate()
        case "config": tm.config_preview()
    return 0


if __name__ == "__main__":  # pragma: no cover
    sys.exit(main())
