# ACM 模板 PDF 生成脚本
# 自动检查依赖并生成 PDF

Write-Host "=== ACM 模板 PDF 生成器 ===" -ForegroundColor Green
Write-Host ""

# 检查是否安装了 Scoop
if (-not (Get-Command scoop -ErrorAction SilentlyContinue)) {
    Write-Host "未检测到 Scoop 包管理器" -ForegroundColor Yellow
    Write-Host "建议安装 Scoop 以便自动安装依赖"
    Write-Host "安装命令: Set-ExecutionPolicy RemoteSigned -Scope CurrentUser; irm get.scoop.sh | iex"
    Write-Host ""
}

# 检查并安装依赖
$dependencies = @{
    "python" = "python"
    "xelatex" = "latex"
}

$missing = @()
foreach ($dep in $dependencies.Keys) {
    if (-not (Get-Command $dep -ErrorAction SilentlyContinue)) {
        $missing += $dependencies[$dep]
        Write-Host "✗ $dep 未安装" -ForegroundColor Red
    } else {
        Write-Host "✓ $dep 已安装" -ForegroundColor Green
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "缺少以下依赖，尝试使用 Scoop 自动安装..." -ForegroundColor Yellow
    
    if (Get-Command scoop -ErrorAction SilentlyContinue) {
        foreach ($package in $missing) {
            Write-Host "安装 $package..." -ForegroundColor Cyan
            try {
                scoop install $package
                Write-Host "✓ $package 安装成功" -ForegroundColor Green
            } catch {
                Write-Host "✗ $package 安装失败" -ForegroundColor Red
                Write-Host "请手动安装: scoop install $package"
            }
        }
    } else {
        Write-Host "请手动安装以下依赖:" -ForegroundColor Red
        foreach ($package in $missing) {
            Write-Host "  scoop install $package" -ForegroundColor White
        }
        Write-Host ""
        Write-Host "或者访问官网下载:"
        Write-Host "  Python: https://www.python.org/"
        Write-Host "  LaTeX: https://miktex.org/ 或 https://tug.org/texlive/"
        return
    }
}

Write-Host ""
Write-Host "开始生成 PDF..." -ForegroundColor Cyan

# 运行 Python 脚本
try {
    python generate_latex.py
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "🎉 成功生成 PDF!" -ForegroundColor Green
        
        # 询问是否打开 PDF
        $open = Read-Host "是否打开生成的 PDF? (y/n)"
        if ($open -eq "y" -or $open -eq "Y") {
            if (Test-Path "ACM_Templates.pdf") {
                Start-Process "ACM_Templates.pdf"
            }
        }
    } else {
        Write-Host "生成失败，退出码: $LASTEXITCODE" -ForegroundColor Red
    }
} catch {
    Write-Host "运行脚本时出错: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "按任意键退出..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
