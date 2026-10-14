#!/bin/bash
set -euo pipefail

PROJECT_DIR="."

# Check if dotnet CLI is installed
if ! command -v dotnet &> /dev/null; then
    echo "错误：dotnet CLI 未安装，请先安装 .NET SDK。" >&2
    exit 1
fi

# Check if xelatex CLI is installed
if ! command -v xelatex &> /dev/null; then
    echo "错误：xelatex CLI 未安装，请先安装 TeX Live。" >&2
    exit 1
fi

# Detect OS and architecture
OS=$(uname -s)
ARCH=$(uname -m)

case "$OS" in
    Linux)
        case "$ARCH" in
            x86_64)        RUNTIME="linux-x64" ;;
            aarch64|arm64) RUNTIME="linux-arm64" ;;
            armv7l)        RUNTIME="linux-arm" ;;
            *)
                echo "错误：不支持的 Linux 架构: $ARCH" >&2
                exit 1
                ;;
        esac
        ;;
    Darwin)
        case "$ARCH" in
            x86_64)        RUNTIME="osx-x64" ;;
            arm64)         RUNTIME="osx-arm64" ;;
            *)
                echo "错误：不支持的 macOS 架构: $ARCH" >&2
                exit 1
                ;;
        esac
        ;;
    CYGWIN*|MINGW*|MSYS*)
        case "$ARCH" in
            x86_64)        RUNTIME="win-x64" ;;
            aarch64|arm64) RUNTIME="win-arm64" ;;
            *)
                echo "错误：不支持的 Windows 架构: $ARCH" >&2
                exit 1
                ;;
        esac
        ;;
    *)
        echo "错误：不支持的平台: $OS" >&2
        exit 1
        ;;
esac

echo "目标平台：$RUNTIME"

# Determine version
if [ -z "${APP_VERSION:-}" ]; then
    EXACT_TAG=$(git describe --tags --exact-match 2>/dev/null || true)
    if [[ "$EXACT_TAG" =~ ^v[0-9] ]]; then
        APP_VERSION="${EXACT_TAG#v}"
    else
        APP_VERSION="2.0.0-rc.1"
    fi
fi
echo "构建版本：$APP_VERSION"

# Setup publish and target directories
PUBLISH_DIR="$PROJECT_DIR/publish"
OUTPUT_DIR="$PUBLISH_DIR/template_builder-$RUNTIME"
ARCHIVE="template_builder-$RUNTIME.tar.gz"
CHECKSUM="template_builder-$RUNTIME.sha256"

# 严格隔离与清理旧产物，防止复用或残留历史包伪装为新包
rm -rf "$OUTPUT_DIR"
rm -f "$PUBLISH_DIR/$ARCHIVE" "$PUBLISH_DIR/$CHECKSUM"
mkdir -p "$PUBLISH_DIR"

# 错误时清理未完成的半成品产物
cleanup_on_error() {
    local exit_code=$?
    if [ $exit_code -ne 0 ]; then
        echo "构建失败（退出码 $exit_code），正在清理临时产物..." >&2
        rm -rf "$OUTPUT_DIR"
        rm -f "$PUBLISH_DIR/$ARCHIVE" "$PUBLISH_DIR/$CHECKSUM"
    fi
    exit $exit_code
}
trap cleanup_on_error EXIT

# Build the project in Release mode
echo "编译项目..."
dotnet build "$PROJECT_DIR/template_builder.csproj" --configuration Release -p:Version="$APP_VERSION"

# Publish for detected platform
echo "发布 $RUNTIME..."
dotnet publish "$PROJECT_DIR/template_builder.csproj" \
    --configuration Release \
    --runtime "$RUNTIME" \
    --output "$OUTPUT_DIR" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:Version="$APP_VERSION"

# 验证发布产物主二进制是否存在
BIN_NAME="template_builder"
if [[ "$RUNTIME" == win-* ]]; then
    BIN_NAME="template_builder.exe"
fi

if [ ! -f "$OUTPUT_DIR/$BIN_NAME" ]; then
    echo "错误：发布目录中未找到主二进制文件 $OUTPUT_DIR/$BIN_NAME！" >&2
    exit 1
fi

echo "清理无关文件..."
# 保留主程序、配置、资源目录，删除 pdb、xml、.DS_Store 等常见无关文件
find "$OUTPUT_DIR" -type f \( -name "*.pdb" -o -name "*.xml" -o -name ".DS_Store" \) -delete

echo "打包压缩..."
(
    cd "$PUBLISH_DIR"
    tar -czvf "$ARCHIVE" "template_builder-$RUNTIME"
    sha256sum "$ARCHIVE" > "$CHECKSUM"
)

# 验证归档与校验和文件已生成且非空
if [ ! -s "$PUBLISH_DIR/$ARCHIVE" ] || [ ! -s "$PUBLISH_DIR/$CHECKSUM" ]; then
    echo "错误：打包归档或校验和生成失败！" >&2
    exit 1
fi

# 解除错误陷阱，正常退出
trap - EXIT

echo "打包完成。产物：$PUBLISH_DIR/$ARCHIVE"