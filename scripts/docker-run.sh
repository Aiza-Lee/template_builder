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
#   ./scripts/docker-run.sh init
#   ./scripts/docker-run.sh build -s ./my_code -o ./preview/doc.pdf
# ==============================================================================

set -euo pipefail

IMAGE_NAME="${TEMPLATE_BUILDER_IMAGE:-template_builder:latest}"

# Ensure docker is available
if ! command -v docker &> /dev/null; then
    echo "错误: 未找到 docker 命令，请先安装 Docker。" >&2
    exit 1
fi

# Run container with current directory mounted to /workspace
exec docker run --rm -it \
    -u "$(id -u):$(id -g)" \
    -v "$(pwd)":/workspace \
    "$IMAGE_NAME" "$@"
