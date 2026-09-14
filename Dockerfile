# syntax=docker/dockerfile:1

# ==============================================================================
# Multi-stage Dockerfile for template_builder
#
# Stage 1: Build single-file self-contained binary using official .NET 9 SDK
# Stage 2: Runtime image with Ubuntu 24.04, modern XeLaTeX, Python Pygments,
#          and native font compatibility layer (Times New Roman, SimSun, SimHei,
#          KaiTi, FangSong, Arial, Fira Code)
# ==============================================================================

ARG DOTNET_SDK_VERSION=9.0
ARG UBUNTU_VERSION=24.04

# ------------------------------------------------------------------------------
# Stage 1: Builder
# ------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS builder

WORKDIR /src

# Copy project files and restore dependencies
COPY template_builder.csproj .
RUN dotnet restore template_builder.csproj

# Copy source code and resources
COPY src/ src/
COPY Resources/ Resources/

# Publish self-contained single-file executable for target runtime
ARG TARGETARCH
RUN set -ex; \
    case "${TARGETARCH:-amd64}" in \
        amd64) RID="linux-x64" ;; \
        arm64) RID="linux-arm64" ;; \
        *) RID="linux-x64" ;; \
    esac; \
    echo "Publishing for RID: $RID"; \
    dotnet publish template_builder.csproj \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:EnableCompressionInSingleFile=true \
        -o /app; \
    find /app -type f \( -name "*.pdb" -o -name "*.xml" \) -delete

# ------------------------------------------------------------------------------
# Stage 2: Runtime environment
# ------------------------------------------------------------------------------
FROM ubuntu:${UBUNTU_VERSION} AS runner

ENV DEBIAN_FRONTEND=noninteractive \
    LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    HOME=/tmp \
    XDG_CACHE_HOME=/tmp/.cache \
    TEXMFVAR=/tmp/texmf-var \
    TEXMFCONFIG=/tmp/texmf-config

# Install minimal TeX Live, Python pygments, fonttools, and base fonts
RUN set -ex; \
    apt-get update -qq; \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        fontconfig \
        fonts-firacode \
        fonts-liberation \
        fonts-noto-cjk \
        python3 \
        python3-fonttools \
        python3-pygments \
        texlive-fonts-recommended \
        texlive-lang-chinese \
        texlive-latex-extra \
        texlive-latex-recommended \
        texlive-xetex \
    ; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Generate font compatibility layer for standard Windows / CJK fonts
COPY docker/setup-fonts.py /tmp/setup-fonts.py
RUN python3 /tmp/setup-fonts.py && rm -f /tmp/setup-fonts.py

# Refresh font cache
RUN fc-cache -fv

# Copy compiled template_builder binary
COPY --from=builder /app/template_builder /usr/local/bin/template_builder
RUN chmod +x /usr/local/bin/template_builder

# Prepare workspace directory with write permissions for non-root users
WORKDIR /workspace
RUN chmod 777 /workspace /tmp

ENTRYPOINT ["template_builder"]
CMD ["--help"]
