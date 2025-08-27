@echo off
chcp 65001 > nul
echo === ACM 模板 PDF 生成器 ===
echo.

echo 检查 Python 安装...
python --version > nul 2>&1
if errorlevel 1 (
    echo ✗ Python 未安装
    echo 请使用以下命令安装: scoop install python
    echo 或访问 https://www.python.org/ 下载
    pause
    exit /b 1
) else (
    echo ✓ Python 已安装
)

echo 检查 XeLaTeX 安装...
xelatex --version > nul 2>&1
if errorlevel 1 (
    echo ✗ XeLaTeX 未安装
    echo 请使用以下命令安装: scoop install latex
    echo 或访问 https://miktex.org/ 下载
    pause
    exit /b 1
) else (
    echo ✓ XeLaTeX 已安装
)

echo.
echo 开始生成 PDF...
python generate_latex.py

if errorlevel 1 (
    echo.
    echo ✗ 生成失败
    pause
    exit /b 1
)

echo.
echo 🎉 成功生成 PDF!
echo.

if exist "ACM_Templates.pdf" (
    set /p open="是否打开生成的 PDF? (y/n): "
    if /i "%open%"=="y" (
        start ACM_Templates.pdf
    )
)

pause
