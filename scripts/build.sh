#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."

command -v dotnet >/dev/null || { echo "错误：构建需要 .NET SDK。" >&2; exit 1; }
PYTHON_BIN=$(command -v python3 || command -v python) || {
  echo "错误：打包需要 Python 3.12+。" >&2
  exit 1
}
"$PYTHON_BIN" -c 'import sys; assert sys.version_info >= (3, 12), "需要 Python 3.12+"'
export DOTNET_NOLOGO=true

# Let the installed .NET SDK identify its native runtime instead of duplicating OS rules.
RUNTIME=$(dotnet msbuild template_builder.csproj -nologo -getProperty:NETCoreSdkRuntimeIdentifier)
if [ -z "${APP_VERSION:-}" ]; then
  TAG=$(git describe --tags --exact-match 2>/dev/null || true)
  if [[ "$TAG" =~ ^v[0-9] ]]; then
    APP_VERSION="${TAG#v}"
  else
    APP_VERSION=$(dotnet msbuild template_builder.csproj -nologo -getProperty:Version)
  fi
fi

OUTPUT_DIR="publish/template_builder-$RUNTIME"
ARCHIVE="$OUTPUT_DIR.tar.gz"
if [[ "$RUNTIME" == win-* ]]; then ARCHIVE="$OUTPUT_DIR.zip"; fi
cleanup() {
  local status=$?
  if [ "$status" -ne 0 ]; then
    rm -rf -- "$OUTPUT_DIR"
    rm -f -- "$ARCHIVE" "$ARCHIVE.sha256"
  fi
}
trap cleanup EXIT
rm -rf -- "$OUTPUT_DIR"
rm -f -- "$ARCHIVE" "$ARCHIVE.sha256"

echo "构建 $RUNTIME，版本 $APP_VERSION..."
dotnet publish template_builder.csproj -c Release -r "$RUNTIME" \
  --self-contained true -p:Version="$APP_VERSION" -o "$OUTPUT_DIR"
"$PYTHON_BIN" scripts/ci.py package "$RUNTIME"
echo "打包完成：$ARCHIVE"
