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

# Detect OS platform
OS=$(uname)
case "$OS" in
    Linux)
        RUNTIME="linux-x64"
        ;;
    Darwin)
        RUNTIME="osx-x64"
        ;;
    CYGWIN*|MINGW*|MSYS*)
        RUNTIME="win-x64"
        ;;
    *)
        echo "不支持的平台: $OS"
        exit 1
        ;;
esac

# Build the project in Release mode
echo "Building the project..."
dotnet build "$PROJECT_DIR/template_builder.csproj" --configuration Release

# Create publish directory
mkdir -p "$PROJECT_DIR/publish"

# Publish for detected platform
echo "Publishing for $RUNTIME..."
dotnet publish "$PROJECT_DIR/template_builder.csproj" --configuration Release --runtime $RUNTIME --output "$PROJECT_DIR/publish/template_builder-$RUNTIME" --self-contained true

echo "Cleaning unnecessary files..."
# 保留主程序、配置、资源目录，删除 pdb、xml、.DS_Store 等常见无关文件
find "$PROJECT_DIR/publish/template_builder-$RUNTIME" -type f \( -name "*.pdb" -o -name "*.xml" -o -name ".DS_Store" \) -delete

echo "Packaging to compressed archive..."
cd "$PROJECT_DIR/publish"
tar -czvf "template_builder-$RUNTIME.tar.gz" "template_builder-$RUNTIME"

sha256sum "template_builder-$RUNTIME.tar.gz" > "template_builder-$RUNTIME.sha256"

echo "Packaging complete. Output is in $PROJECT_DIR/publish/template_builder-$RUNTIME.tar.gz"