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

# ---------- 工具函数：修复 dpkg 中断状态（激进版 v2） ----------
fix_dpkg() {
    echo "[*] 激进修复 dpkg（自动应答 conffile 冲突）..."
    # 0. 非交互模式 + 全局自动应答（写 apt 配置，后续 pkg upgrade 也不弹提示）
    export DEBIAN_FRONTEND=noninteractive
    printf 'Dpkg::Options {\n  "--force-confold";\n};\nAPT::Get::Assume-Yes "true";\n' \
        > "$PREFIX/etc/apt/apt.conf.d/99dsh-noninteractive" 2>/dev/null
    # 1. 清锁文件与中断的更新状态
    rm -f "$PREFIX"/var/lib/dpkg/lock* 2>/dev/null
    rm -rf "$PREFIX"/var/lib/dpkg/updates/* 2>/dev/null
    # 2. 重新配置所有未配置的包（--force-confold 自动应答，不弹交互）
    dpkg --force-confold --configure -a 2>&1 | tail -3
    # 3. 若 apt 包自身仍卡住，冻结它跳过（apt 升级不重要）
    apt-mark hold apt 2>/dev/null
    dpkg --force-confold --configure -a 2>&1 | tail -3
    # 4. 修复破损依赖（同样自动应答）
    apt --fix-broken install -y 2>&1 | tail -3
    echo "[*] dpkg 修复流程结束"
}

# ---------- 安装函数：逐个安装，失败跳过 ----------
install_pkgs() {
    local failed=""
    for p in "$@"; do
        printf '  - %-18s' "$p"
        if pkg install -y "$p" >/dev/null 2>&1; then
            echo "[✔]"
        else
            echo "[✘] 失败，跳过"
            failed="$failed $p"
        fi
    done
    if [ -n "$failed" ]; then
        echo "[!] 以下包安装失败（已跳过，不影响后续）: $failed"
    fi
}

# ---------- 0.4 预写 apt 自动应答配置（防止 conffile 交互卡死） ----------
export DEBIAN_FRONTEND=noninteractive
printf 'Dpkg::Options {\n  "--force-confold";\n};\nAPT::Get::Assume-Yes "true";\n' \
    > "$PREFIX/etc/apt/apt.conf.d/99dsh-noninteractive" 2>/dev/null

# ---------- 0.5 启动预检：dpkg 状态 ----------
if dpkg --audit 2>/dev/null | grep -q .; then
    echo "[*] 发现未完成的 dpkg 任务，先修复..."
    fix_dpkg
fi

# ---------- 1. 更新源 ----------
echo ""
echo "[1/6] 更新软件源..."
pkg update -y || { echo "[!] pkg update 失败，尝试修复后重试"; fix_dpkg; pkg update -y || { echo "[!] 仍然失败，请检查网络"; exit 1; }; }
if ! pkg upgrade -y; then
    echo "[*] pkg upgrade 失败，自动修复中..."
    fix_dpkg
    pkg upgrade -y || echo "[*] upgrade 仍有问题，继续安装（后面会再次校验）"
fi

# ---------- 2. 授权存储 ----------
echo ""
echo "[2/6] 授权内部存储..."
termux-setup-storage >/dev/null 2>&1 || echo "[*] 稍后可手动执行 termux-setup-storage"

# ---------- 3. 安装刚需工具 ----------
echo ""
echo "[3/6] 安装刚需工具 (逐个安装，失败自动跳过)..."
install_pkgs \
    curl wget git vim nano tree zip unzip tar \
    htop tmux openssh rsync jq \
    python php make clang pkg-config \
    openssl-tool bash-completion termux-api proot-distro \
    cmake ninja libvips libjpeg-turbo libpng
echo "[*] 工具安装完成"

# ---------- 4. 安装 Node.js ----------
echo ""
echo "[4/6] 安装 Node.js (nodejs-lts)..."
if ! command -v node >/dev/null 2>&1; then
    echo "[*] 安装 nodejs-lts ..."
    pkg install -y nodejs-lts >/dev/null 2>&1 || { echo "[!] nodejs 安装失败，修复后重试"; fix_dpkg; pkg install -y nodejs-lts >/dev/null 2>&1 || { echo "[!] nodejs 仍未安装成功"; exit 1; }; }
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
        if npm install -g "$TMP_TGZ" > $HOME/dsh-npm-install.log 2>&1; then
            echo "[✔] npm 安装成功"
        else
            echo "[!] npm 安装失败，错误日志（尾部 30 行）："
            tail -30 $HOME/dsh-npm-install.log
            echo "[!] 若为 node-gyp 编译错误（node-pty/sharp/koffi 等原生模块），说明 dsh 原生依赖与 Termux 不兼容"
        fi
        rm -f "$TMP_TGZ"
    else
        echo "[!] 镜像下载失败，改用 npm 官方源"
        if npm install -g @deepseek-ai/dsh > $HOME/dsh-npm-install.log 2>&1; then
            echo "[✔] npm 安装成功"
        else
            echo "[!] npm 安装失败，错误日志（尾部 30 行）："
            tail -30 $HOME/dsh-npm-install.log
        fi
    fi
fi

# ---------- 5.5 Termux/Android 特殊处理：sharp 装 wasm 运行时 ----------
# sharp 无 android-arm64 原生二进制，官方方案是使用 WebAssembly 版本
if [ -n "${TERMUX_VERSION:-}" ] || [ "$(uname -o 2>/dev/null)" = "Android" ]; then
    echo ""
    echo "[5.5] 检测到 Termux，为 sharp 安装 wasm 运行时（否则 dsh web 无法启动）..."
    if [ -d "$PREFIX/lib/node_modules/@deepseek-ai/dsh" ]; then
        (cd "$PREFIX/lib/node_modules/@deepseek-ai/dsh" && \
         npm install --cpu=wasm32 sharp --registry="$NPM_REGISTRY" >> "$HOME/dsh-npm-install.log" 2>&1 \
         && echo "[✔] sharp wasm 安装成功" \
         || echo "[!] sharp wasm 安装失败，可稍后手动执行: cd $PREFIX/lib/node_modules/@deepseek-ai/dsh && npm install --cpu=wasm32 sharp")
    fi
fi

# ---------- 5.6 包装 dsh 命令（Termux 需要 --expose-internals） ----------
# HMR 插件要求 node --expose-internals，但该标志不允许放 NODE_OPTIONS，
# 只能通过 node 命令行传入 → 用包装脚本替换 dsh 命令，dsh web 直接可用
# 注意：dsh.orig 必须是指向 npm bin.js 的符号链接，重复执行本函数也会干净重建
wrap_dsh() {
    local PKG_BIN="$PREFIX/lib/node_modules/@deepseek-ai/dsh/lib/bin.js"
    if [ ! -f "$PKG_BIN" ]; then
        echo "[!] 未找到 dsh 入口 $PKG_BIN，跳过包装"
        return
    fi
    # 干净重建：删掉旧的 dsh 与 dsh.orig（可能是被污染的脚本），重建 symlink + 包装
    rm -f "$PREFIX/bin/dsh" "$PREFIX/bin/dsh.orig"
    ln -s "$PKG_BIN" "$PREFIX/bin/dsh.orig"
    cat > "$PREFIX/bin/dsh" <<EOF
#!/bin/bash
PREFIX="\$(dirname "\$(dirname "\$(readlink -f "\$0")")")"
exec node --expose-internals "\$PREFIX/bin/dsh.orig" "\$@"
EOF
    chmod +x "$PREFIX/bin/dsh"
    echo "[✔] dsh 已包装（自动附带 --expose-internals，dsh web 直接可用）"
}
wrap_dsh
# 兼容旧版：保留 dshx 快捷命令
if [ -x "$PREFIX/bin/dsh" ] && [ ! -e "$PREFIX/bin/dshx" ]; then
    cat > "$PREFIX/bin/dshx" <<EOF
#!/bin/bash
exec "$PREFIX/bin/dsh" "\$@"
EOF
    chmod +x "$PREFIX/bin/dshx"
fi

# ---------- 5.7 应用会话持久化硬链接补丁（Android 禁止 hardlink） ----------
# dsh 写会话日志用 link() 原子替换，Android/Termux 禁止硬链接 → EACCES 崩溃
# 补丁：link 失败时降级为 rename（效果等价，仅少一点原子性保证）
apply_link_patch() {
    local SP_DIR=""
    for d in \
        "$PREFIX/lib/node_modules/@deepseek-ai/dsh-session-persistence-jsonl" \
        "$PREFIX/lib/node_modules/@deepseek-ai/dsh/node_modules/@deepseek-ai/dsh-session-persistence-jsonl"; do
        if [ -f "$d/lib/index.js" ]; then SP_DIR="$d"; break; fi
    done
    [ -z "$SP_DIR" ] && SP_DIR=$(find "$PREFIX/lib/node_modules" -maxdepth 4 -type d -name "dsh-session-persistence-jsonl" 2>/dev/null | head -1)
    if [ -z "$SP_DIR" ] || [ ! -f "$SP_DIR/lib/index.js" ]; then
        echo "[!] 未找到 session-persistence 插件，跳过补丁"
        return
    fi
    local TARGET="$SP_DIR/lib/index.js"
    if grep -q "fall back to rename" "$TARGET" 2>/dev/null; then
        echo "[✔] 硬链接补丁已应用"
        return
    fi
    python3 - "$TARGET" <<'PY'
import sys
p = sys.argv[1]
s = open(p, encoding="utf-8").read()
if "rename," not in s.split("from \"node:fs/promises\"")[0]:
    s = s.replace("import { link,", "import { link, rename,", 1)
old = """await link(tmp, finalPath);
\t\t\tlinked = true;
\t\t} finally {"""
new = """await link(tmp, finalPath);
\t\t\tlinked = true;
\t\t} catch {
\t\t\t/* Android/Termux forbids hardlinks: fall back to rename */
\t\t\ttry { await rename(tmp, finalPath); linked = true; } catch {}
\t\t} finally {"""
if old in s:
    s = s.replace(old, new, 1)
else:
    import re
    pat = re.compile(r"await link\(tmp, finalPath\);\s*linked = true;\s*\} finally \{")
    s, n = pat.subn(new, s, count=1)
    if n == 0:
        print("[!] 未能匹配 link 代码段（dsh 版本可能已变化）")
        sys.exit(1)
open(p, "w", encoding="utf-8").write(s)
print("[✔] 硬链接补丁应用成功")
PY
}
apply_link_patch

# ---------- 5.8 校正 .dsh 权限（dsh 安全校验要求凭据文件 600） ----------
if [ -f "$HOME/.dsh/.credentials.yaml" ]; then
    chmod 600 "$HOME/.dsh/.credentials.yaml" 2>/dev/null
fi
if [ -f "$HOME/.dsh/settings.yaml" ]; then
    chmod 600 "$HOME/.dsh/settings.yaml" 2>/dev/null
fi

# ---------- 6. 验证与说明 ----------
echo ""
echo "[6/6] 验证安装..."
if command -v dsh >/dev/null 2>&1; then
    echo "[✔] dsh 安装成功: $(dsh --version 2>/dev/null || echo v?.?.?)"
elif [ -x "$PREFIX/bin/dsh" ]; then
    echo "[✔] dsh 已安装到 $PREFIX/bin/dsh"
    echo "    [*] 但 PATH 中未找到，执行以下命令后可正常使用:"
    echo "        export PATH=\"\$PATH:\$PREFIX/bin\""
else
    echo "[!] dsh 命令未找到，可能原因与对策："
    echo "    1) npm 安装失败（上面有日志）→ 查看 \$HOME/dsh-npm-install.log"
    echo "    2) 原生模块编译失败（koffi 需 CMake / node-pty 需 make+clang / sharp 需 libvips）"
    echo "       → 脚本已自动安装 cmake/ninja/libvips 等编译依赖，重跑即可"
    echo "       pkg install -y cmake ninja libvips libjpeg-turbo libpng"
    echo "    3) 仍不兼容 → 用 proot 装完整 Linux 运行 dsh："
    echo "       proot-distro install ubuntu"
    echo "       proot-distro login ubuntu"
    echo "       # 在 Ubuntu 里：apt update && apt install -y nodejs npm"
    echo "       # npm install -g @deepseek-ai/dsh && dsh web"
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
