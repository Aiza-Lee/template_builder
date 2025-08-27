@echo off
chcp 65001 > nul
echo.
echo ================================================================
echo                ACM 模板 LaTeX/PDF 生成器
echo                    一键安装和使用脚本
echo ================================================================
echo.

echo 1. 检查环境...
echo.

REM 检查Python
python --version > nul 2>&1
if errorlevel 1 (
    echo [❌] Python 未安装
    echo.
    echo 请选择安装方式：
    echo   方式1: 使用 Scoop （推荐）
    echo     - 如果没有安装 Scoop：
    echo       PowerShell: Set-ExecutionPolicy RemoteSigned -Scope CurrentUser; irm get.scoop.sh ^| iex
    echo     - 然后运行: scoop install python
    echo.
    echo   方式2: 官网下载
    echo     - 访问 https://www.python.org/ 下载并安装
    echo.
    pause
    exit /b 1
) else (
    for /f "tokens=2" %%i in ('python --version') do echo [✓] Python %%i 已安装
)

REM 检查XeLaTeX
xelatex --version > nul 2>&1
if errorlevel 1 (
    echo [❌] XeLaTeX 未安装
    echo.
    echo 请选择安装方式：
    echo   方式1: 使用 Scoop （推荐）
    echo     scoop install latex
    echo.
    echo   方式2: 手动安装
    echo     - MiKTeX: https://miktex.org/
    echo     - TeX Live: https://tug.org/texlive/
    echo.
    pause
    exit /b 1
) else (
    echo [✓] XeLaTeX 已安装
)

echo.
echo 2. 环境检查完成！
echo.

REM 检查模板目录
if not exist "src" (
    echo [⚠️] 警告: 找不到模板目录 "src"
    echo.
    echo 请确保：
    echo   1. 在当前目录下有 "src" 文件夹
    echo   2. 该文件夹包含你的 C++ 模板文件
    echo.
    set /p continue="是否继续？这可能会生成空的PDF (y/n): "
    if /i not "%continue%"=="y" (
        exit /b 0
    )
) else (
    echo [✓] 找到模板目录 "src"
)

echo.
echo 3. 开始生成PDF...
echo.

python main.py

if errorlevel 1 (
    echo.
    echo [❌] 生成失败！
    echo.
    echo 可能的解决方案：
    echo   1. 检查模板文件编码是否为 UTF-8
    echo   2. 确保没有语法错误的 C++ 代码
    echo   3. 检查 LaTeX 安装是否完整
    echo   4. 查看控制台输出的详细错误信息
    echo.
    pause
    exit /b 1
)

echo.
echo ================================================================
echo                        🎉 生成成功！
echo ================================================================
echo.
echo 生成的文件：
if exist "output\ACM_Templates.pdf" (
    for %%i in ("output\ACM_Templates.pdf") do echo   📕 PDF 文件: output\ACM_Templates.pdf [%%~zi bytes]
)
if exist "build\ACM_Templates.tex" (
    for %%i in ("build\ACM_Templates.tex") do echo   📄 LaTeX 源文件: build\ACM_Templates.tex [%%~zi bytes]
)

echo.
echo 快速使用命令：
echo   python main.py                    # 重新生成PDF
echo   python main.py --layout landscape # 横版布局
echo   python main.py --layout portrait  # 竖版布局
echo   python scripts\generate_latex.py  # 直接调用生成器
echo.

set /p open="是否现在打开PDF？(y/n): "
if /i "%open%"=="y" (
    if exist "output\ACM_Templates.pdf" (
        start output\ACM_Templates.pdf
    ) else (
        echo 找不到PDF文件
    )
)

echo.
echo 感谢使用！如有问题请查看 README.md 或查看项目文档
pause
