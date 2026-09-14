#!/usr/bin/env python3
"""
Create font compatibility layer for template_builder Docker image.
Clones Liberation and Fandol open-source fonts with standard Windows/CJK font family names:
  - Liberation Serif -> Times New Roman
  - Liberation Sans  -> Arial
  - Liberation Mono  -> Courier New
  - FandolSong       -> SimSun
  - FandolHei        -> SimHei
  - FandolKai        -> KaiTi
  - FandolFang       -> FangSong

This enables template_builder to compile documents configured with standard
Windows fonts out of the box on Linux containers without proprietary font requirements.
"""

import os
from fontTools.ttLib import TTFont

TARGET_DIR = "/usr/local/share/fonts/compat"

FONT_MAPPINGS = [
    # (source_path, target_filename, family_name, style_name, ps_name)
    # Times New Roman (from Liberation Serif)
    (
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
        "TimesNewRoman-Regular.ttf",
        "Times New Roman",
        "Regular",
        "TimesNewRoman-Regular",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf",
        "TimesNewRoman-Bold.ttf",
        "Times New Roman",
        "Bold",
        "TimesNewRoman-Bold",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSerif-Italic.ttf",
        "TimesNewRoman-Italic.ttf",
        "Times New Roman",
        "Italic",
        "TimesNewRoman-Italic",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSerif-BoldItalic.ttf",
        "TimesNewRoman-BoldItalic.ttf",
        "Times New Roman",
        "Bold Italic",
        "TimesNewRoman-BoldItalic",
    ),
    # Arial (from Liberation Sans)
    (
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "Arial-Regular.ttf",
        "Arial",
        "Regular",
        "Arial-Regular",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "Arial-Bold.ttf",
        "Arial",
        "Bold",
        "Arial-Bold",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf",
        "Arial-Italic.ttf",
        "Arial",
        "Italic",
        "Arial-Italic",
    ),
    (
        "/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf",
        "Arial-BoldItalic.ttf",
        "Arial",
        "Bold Italic",
        "Arial-BoldItalic",
    ),
    # Courier New (from Liberation Mono)
    (
        "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
        "CourierNew-Regular.ttf",
        "Courier New",
        "Regular",
        "CourierNew-Regular",
    ),
    # SimSun (from FandolSong)
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolSong-Regular.otf",
        "SimSun-Regular.otf",
        "SimSun",
        "Regular",
        "SimSun-Regular",
    ),
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolSong-Bold.otf",
        "SimSun-Bold.otf",
        "SimSun",
        "Bold",
        "SimSun-Bold",
    ),
    # SimHei (from FandolHei)
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolHei-Regular.otf",
        "SimHei-Regular.otf",
        "SimHei",
        "Regular",
        "SimHei-Regular",
    ),
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolHei-Bold.otf",
        "SimHei-Bold.otf",
        "SimHei",
        "Bold",
        "SimHei-Bold",
    ),
    # KaiTi (from FandolKai)
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolKai-Regular.otf",
        "KaiTi-Regular.otf",
        "KaiTi",
        "Regular",
        "KaiTi-Regular",
    ),
    # FangSong (from FandolFang)
    (
        "/usr/share/texlive/texmf-dist/fonts/opentype/public/fandol/FandolFang-Regular.otf",
        "FangSong-Regular.otf",
        "FangSong",
        "Regular",
        "FangSong-Regular",
    ),
]


def main():
    os.makedirs(TARGET_DIR, exist_ok=True)
    count = 0
    for src, fname, fam, style, ps in FONT_MAPPINGS:
        if not os.path.exists(src):
            print(f"[Warning] Font source not found: {src}, skipping.")
            continue
        try:
            font = TTFont(src)
            for rec in font["name"].names:
                if rec.nameID in (1, 16):
                    font["name"].setName(
                        fam, rec.nameID, rec.platformID, rec.platEncID, rec.langID
                    )
                elif rec.nameID in (2, 17):
                    font["name"].setName(
                        style, rec.nameID, rec.platformID, rec.platEncID, rec.langID
                    )
                elif rec.nameID == 4:
                    font["name"].setName(
                        f"{fam} {style}",
                        4,
                        rec.platformID,
                        rec.platEncID,
                        rec.langID,
                    )
                elif rec.nameID == 6:
                    font["name"].setName(
                        ps, 6, rec.platformID, rec.platEncID, rec.langID
                    )
            out_path = os.path.join(TARGET_DIR, fname)
            font.save(out_path)
            count += 1
            print(f"[OK] Generated compat font: {fname} -> Family: '{fam}'")
        except Exception as e:
            print(f"[Error] Failed to process {src}: {e}")

    print(f"Font compatibility layer generated: {count} fonts created in {TARGET_DIR}")


if __name__ == "__main__":
    main()
