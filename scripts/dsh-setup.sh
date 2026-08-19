#!/data/data/com.termux/files/usr/bin/bash
# ============================================================
#  DeepSeek Harness (dsh) Termux 一键安装脚本
#  香港镜像加速版 | 用法: bash dsh-setup.sh
#  或一条命令: curl -fsSL http://dsh.xn--rhyr4ib67a.top/dsh-setup.sh | bash
# ============================================================

set -u

# ---------- 配置 ----------
MIRROR="${DSH_MIRROR:-http://dsh.xn--rhyr4ib67a.top/mirror}"   # 镜像根（可环境变量覆盖）
NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmmirror.com}" # npm 加速源（国内快）
REQUIRED_NODE=22.19                                             # dsh 要求 Node >= 22.19

# ---------- 0. 环境检查 ----------
echo "=============================================="
echo "  DeepSeek Harness 一键安装 (Termux)"
echo "=============================================="
if [ -z "${PREFIX:-}" ] || [ ! -d "$PREFIX" ]; then
    echo "[!] 这不是 Termux 环境，请在 Termux 中运行"
    exit 1
fi
if ! command -v pkg >/dev/null 2>&1; then
    echo "[!] 未找到 pkg，请先执行: pkg install -y bash"
    exit 1
fi

# ---------- 1. 更新源 ----------
echo ""
echo "[1/6] 更新软件源..."
pkg update -y || { echo "[!] pkg update 失败，检查网络后重试"; exit 1; }
pkg upgrade -y || echo "[*] pkg upgrade 部分失败，继续"

# ---------- 2. 授权存储 ----------
echo ""
echo "[2/6] 授权内部存储..."
termux-setup-storage >/dev/null 2>&1 || echo "[*] 稍后可手动执行 termux-setup-storage"

# ---------- 3. 安装刚需工具 ----------
echo ""
echo "[3/6] 安装刚需工具 (git/curl/python/php/clang...)..."
pkg install -y \
    curl wget git vim nano tree zip unzip tar \
    htop tmux openssh rsync jq \
    python php make clang pkg-config \
    openssl-tool bash-completion termux-api proot-distro \
    >/dev/null 2>&1 || {
        echo "[!] 部分工具安装失败，逐个重试..."
        pkg install -y curl wget git vim nano zip unzip tar openssh 2>&1 | tail -3
    }
echo "[*] 工具安装完成"

# ---------- 4. 安装 Node.js ----------
echo ""
echo "[4/6] 安装 Node.js (nodejs-lts)..."
if ! command -v node >/dev/null 2>&1; then
    pkg install -y nodejs-lts >/dev/null 2>&1 || { echo "[!] nodejs 安装失败"; exit 1; }
fi
NODE_VER=$(node -v 2>/dev/null | sed 's/^v//')
echo "[*] Node.js 版本: v${NODE_VER:-未知}"
if [ -n "${NODE_VER:-}" ]; then
    MAJOR=$(echo "$NODE_VER" | cut -d. -f1)
    MINOR=$(echo "$NODE_VER" | cut -d. -f2)
    if [ "$MAJOR" -lt 22 ] || { [ "$MAJOR" -eq 22 ] && [ "$MINOR" -lt 19 ]; }; then
        echo "[!] Node 版本过低 (需 >= $REQUIRED_NODE)，尝试升级..."
        pkg upgrade -y nodejs-lts >/dev/null 2>&1 || true
    fi
fi

# ---------- 5. 安装 dsh ----------
echo ""
echo "[5/6] 安装 DeepSeek Harness (dsh)..."
# 配置 npm 加速源（仅本会话+用户级）
npm config set registry "$NPM_REGISTRY" 2>/dev/null || true
echo "[*] npm registry: $NPM_REGISTRY"

# 从香港镜像获取最新版本号
DSH_VER=""
VINFO=$(curl -fsSL -m 20 "$MIRROR/VERSION.txt" 2>/dev/null || echo "")
DSH_VER=$(echo "$VINFO" | grep '^npm 最新:' | awk '{print $3}')
if [ -z "$DSH_VER" ]; then
    # 兜底：尝试镜像目录已知版本
    for v in 0.1.0-rc.7 0.1.0-rc.6 0.1.0-rc.3; do
        if curl -fsSL -m 10 -o /dev/null "$MIRROR/dsh/npm/dsh-$v.tgz" 2>/dev/null; then
            DSH_VER=$v; break
        fi
    done
fi
if [ -z "$DSH_VER" ]; then
    echo "[!] 无法从镜像获取 dsh 版本，改用 npm 官方源安装"
    npm install -g @deepseek-ai/dsh 2>&1 | tail -5 || { echo "[!] npm 安装失败"; exit 1; }
else
    echo "[*] 镜像版本: dsh-$DSH_VER"
    TMP_TGZ="$HOME/dsh-$DSH_VER.tgz"
    echo "[*] 从香港镜像下载: dsh-$DSH_VER.tgz ..."
    if curl -fsSL -m 120 -o "$TMP_TGZ" "$MIRROR/dsh/npm/dsh-$DSH_VER.tgz"; then
        echo "[*] 下载完成 ($(du -h "$TMP_TGZ" | cut -f1))，npm 安装中（含依赖，需几分钟）..."
        npm install -g "$TMP_TGZ" 2>&1 | tail -8
        rm -f "$TMP_TGZ"
    else
        echo "[!] 镜像下载失败，改用 npm 官方源"
        npm install -g @deepseek-ai/dsh 2>&1 | tail -5
    fi
fi

# ---------- 6. 验证与说明 ----------
echo ""
echo "[6/6] 验证安装..."
if command -v dsh >/dev/null 2>&1; then
    echo "[✔] dsh 安装成功: $(dsh --version 2>/dev/null || echo v?.?.?)"
else
    echo "[!] dsh 命令未找到。若上方有编译错误，可能缺少原生依赖，尝试:"
    echo "    pkg install -y libvips libjpeg-turbo libpng nodejs-lts"
    echo "    然后重跑本脚本"
fi

echo ""
echo "=============================================="
echo "  安装完成！使用指南："
echo "  --------------------------------------------"
echo "  1. 启动 Web UI:   dsh web"
echo "                     浏览器打开 http://127.0.0.1:3080"
echo "  2. 首次使用:      在 Web UI 中填入 DeepSeek API Key"
echo "  --------------------------------------------"
echo "  更新 dsh:         重新运行本脚本 (自动取镜像最新版)"
echo "  更新工具:         pkg upgrade"
echo "  npm 加速:         registry = $NPM_REGISTRY"
echo "=============================================="
