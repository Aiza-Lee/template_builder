#!/bin/bash

PROJECT_DIR="."

# Check if dotnet CLI is installed
if ! command -v dotnet &> /dev/null; then
    echo "dotnet CLI 未安装，请先安装 .NET SDK。"
    exit 1
fi

# Check if xelatex CLI is installed
if ! command -v xelatex &> /dev/null; then
    echo "xelatex CLI 未安装，请先安装 TeX Live。"
    exit 1
fi

# Detect OS and architecture
OS=$(uname -s)
ARCH=$(uname -m)

case "$OS" in
    Linux)
        case "$ARCH" in
            x86_64)       RUNTIME="linux-x64" ;;
            aarch64|arm64) RUNTIME="linux-arm64" ;;
            armv7l)        RUNTIME="linux-arm" ;;
            *)
                echo "不支持的 Linux 架构: $ARCH"
                exit 1
                ;;
        esac
        ;;
    Darwin)
        case "$ARCH" in
            x86_64)        RUNTIME="osx-x64" ;;
            arm64)         RUNTIME="osx-arm64" ;;
            *)
                echo "不支持的 macOS 架构: $ARCH"
                exit 1
                ;;
        esac
        ;;
    CYGWIN*|MINGW*|MSYS*)
        case "$ARCH" in
            x86_64)       RUNTIME="win-x64" ;;
            aarch64|arm64) RUNTIME="win-arm64" ;;
            *)
                echo "不支持的 Windows 架构: $ARCH"
                exit 1
                ;;
        esac
        ;;
    *)
        echo "不支持的平台: $OS"
        exit 1
        ;;
esac

echo "目标平台：$RUNTIME"

# Build the project in Release mode
echo "编译项目..."
dotnet build "$PROJECT_DIR/template_builder.csproj" --configuration Release

# Create publish directory
mkdir -p "$PROJECT_DIR/publish"

# Publish for detected platform
echo "发布 $RUNTIME..."
dotnet publish "$PROJECT_DIR/template_builder.csproj" \
    --configuration Release \
    --runtime "$RUNTIME" \
    --output "$PROJECT_DIR/publish/template_builder-$RUNTIME" \
    --self-contained true

echo "清理无关文件..."
# 保留主程序、配置、资源目录，删除 pdb、xml、.DS_Store 等常见无关文件
find "$PROJECT_DIR/publish/template_builder-$RUNTIME" -type f \( -name "*.pdb" -o -name "*.xml" -o -name ".DS_Store" \) -delete

echo "打包压缩..."
cd "$PROJECT_DIR/publish"
tar -czvf "template_builder-$RUNTIME.tar.gz" "template_builder-$RUNTIME"

sha256sum "template_builder-$RUNTIME.tar.gz" > "template_builder-$RUNTIME.sha256"

echo "打包完成。产物：$PROJECT_DIR/publish/template_builder-$RUNTIME.tar.gz"