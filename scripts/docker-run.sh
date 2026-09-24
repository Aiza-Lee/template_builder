#!/usr/bin/env bash
# ==============================================================================
# Helper script to run template_builder in Docker
#
# Mounts current directory to /workspace and executes template_builder
# with the current host user UID/GID to avoid file permission issues.
#
# Usage:
#   ./scripts/docker-run.sh [template_builder arguments...]
#
# Examples:
#   ./scripts/docker-run.sh --help
#   ./scripts/docker-run.sh init -o config.jsonc
#   ./scripts/docker-run.sh validate -s ./my_templates -c config.jsonc
#   ./scripts/docker-run.sh build -s ./my_templates -o ./preview/doc.pdf
# ==============================================================================

set -euo pipefail

# 统一默认发布镜像为 GHCR 镜像；如果本地存在 template_builder:latest 且未显式指定，智能优先复用本地
DEFAULT_REMOTE_IMAGE="ghcr.io/aiza-lee/template_builder:latest"
if [ -n "${TEMPLATE_BUILDER_IMAGE:-}" ]; then
    IMAGE_NAME="$TEMPLATE_BUILDER_IMAGE"
elif docker image inspect template_builder:latest &>/dev/null && ! docker image inspect "$DEFAULT_REMOTE_IMAGE" &>/dev/null; then
    IMAGE_NAME="template_builder:latest"
else
    IMAGE_NAME="$DEFAULT_REMOTE_IMAGE"
fi

# Ensure docker is available
if ! command -v docker &> /dev/null; then
    echo "错误: 未找到 docker 命令，请先安装 Docker。" >&2
    exit 1
fi

# 依据终端 TTY 实际状态动态分配交互与伪终端参数
# 当且仅当 stdin 与 stdout 均为终端时启用 -it（如 TUI 模式）；
# 否则在非交互、CI、重定向或管道环境下禁用 -t，杜绝 "input device is not a TTY" 报错
DOCKER_FLAGS=()
if [ -t 0 ] && [ -t 1 ]; then
    DOCKER_FLAGS+=(-it)
elif [ -t 0 ]; then
    DOCKER_FLAGS+=(-i)
fi

# Support optional external fonts directory via TEMPLATE_BUILDER_FONTS_DIR
EXTRA_MOUNTS=()
if [ -n "${TEMPLATE_BUILDER_FONTS_DIR:-}" ] && [ -d "$TEMPLATE_BUILDER_FONTS_DIR" ]; then
    EXTRA_MOUNTS+=(-v "$TEMPLATE_BUILDER_FONTS_DIR:/fonts:ro")
fi

# Run container with current directory mounted to /workspace
exec docker run --rm \
    ${DOCKER_FLAGS[@]+"${DOCKER_FLAGS[@]}"} \
    -u "$(id -u):$(id -g)" \
    -v "$(pwd)":/workspace \
    ${EXTRA_MOUNTS[@]+"${EXTRA_MOUNTS[@]}"} \
    "$IMAGE_NAME" "$@"
