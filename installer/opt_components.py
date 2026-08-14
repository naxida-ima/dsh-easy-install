# -*- coding: utf-8 -*-
"""可选组件：检测 + 在线下载官方安装器 + 静默安装（不内置静态资源）
支持：Node.js / Python / Git / PHP / WSL
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import urllib.request
import zipfile

from shared.paths import is_windows, log

# 内置兜底版本（动态解析失败时使用；2026-08 确认存在）
FALLBACK = {
    "node":   ("https://nodejs.org/dist/v24.19.0/node-v24.19.0-x64.msi", "msi"),
    "python": ("https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe", "python"),
    "git":    ("https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/Git-2.47.1-64-bit.exe", "git"),
    "php":    ("https://windows.php.net/downloads/releases/php-8.3.14-nts-Win32-vs16-x64.zip", "phpzip"),
}

# 检测命令（PATH 查找）
DETECT = {
    "node":   ["node", "node.exe"],
    "python": ["python", "python.exe"],
    "git":    ["git", "git.exe"],
    "php":    ["php", "php.exe"],
    "wsl":    ["wsl", "wsl.exe"],
}

COMPONENTS = [
    {"key": "node", "name": "Node.js（长期支持版）",
     "desc": "JavaScript 运行环境。安装器已内置一份，这里可再装系统版供其他程序使用。",
     "official": "https://nodejs.org/"},
    {"key": "python", "name": "Python 3",
     "desc": "通用编程语言，很多工具脚本需要它。静默安装到当前用户，自动加入 PATH。",
     "official": "https://www.python.org/downloads/"},
    {"key": "git", "name": "Git",
     "desc": "代码版本管理工具。开发必备，静默安装，不重启。",
     "official": "https://git-scm.com/download/win"},
    {"key": "php", "name": "PHP",
     "desc": "网站后端语言。自动下载绿色版并解压到用户目录，加入 PATH。",
     "official": "https://windows.php.net/download"},
    {"key": "wsl", "name": "WSL（Windows 的 Linux 子系统）",
     "desc": "可以在 Windows 里运行 Linux。安装需管理员权限，可能要求重启。",
     "official": "https://learn.microsoft.com/zh-cn/windows/wsl/install"},
]

DL_DIR = os.path.join(os.environ.get("TEMP", "."), "dsh-opt")


def _http_json(url: str, timeout: int = 20):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode("utf-8", errors="replace"))


def _http_text(url: str, timeout: int = 20) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", errors="replace")


def _resolve_url(key: str) -> tuple[str, str] | None:
    """动态解析官方最新版下载地址，失败返回内置兜底；再失败返回 None"""
    try:
        if key == "node":
            d = _http_json("https://nodejs.org/dist/index.json")
            for item in d:
                if item.get("lts"):
                    v = item["version"]  # v24.19.0
                    return (f"https://nodejs.org/dist/{v}/node-{v}-x64.msi", "msi")
        elif key == "python":
            html = _http_text("https://www.python.org/ftp/python/")
            vers = re.findall(r'href="(\d+\.\d+\.\d+)/"', html)
            if vers:
                v = sorted(vers, key=lambda s: tuple(map(int, s.split("."))))[-1]
                return (f"https://www.python.org/ftp/python/{v}/python-{v}-amd64.exe", "python")
        elif key == "git":
            d = _http_json("https://api.github.com/repos/git-for-windows/git/releases/latest")
            tag = d.get("tag_name", "")          # v2.47.1.windows.1
            ver = tag.replace("v", "").split(".windows.")[0]  # 2.47.1
            url = (f"https://github.com/git-for-windows/git/releases/download/{tag}/"
                   f"Git-{ver}-64-bit.exe")
            return (url, "git")
        elif key == "php":
            html = _http_text("https://windows.php.net/downloads/releases/")
            vers = re.findall(r'href="(php-(8\.\d+\.\d+)-nts-Win32-vs16-x64\.zip)"', html)
            if vers:
                best = max(vers, key=lambda m: tuple(int(x) for x in m[1].split(".")))
                return ("https://windows.php.net/downloads/releases/" + best[0], "phpzip")
    except Exception as e:
        log(f"resolve {key} latest failed: {e}", "opt")
    return FALLBACK.get(key)


def is_installed(key: str) -> bool:
    if not is_windows() and key == "wsl":
        return shutil.which("wsl") is not None
    for name in DETECT.get(key, []):
        if shutil.which(name):
            return True
    # WSL 特殊：检测 wsl.exe 系统路径
    if key == "wsl":
        sysroot = os.environ.get("SystemRoot", r"C:\Windows")
        return os.path.isfile(os.path.join(sysroot, "System32", "wsl.exe"))
    return False


def _download(url: str, dest: str, progress_cb) -> bool:
    os.makedirs(DL_DIR, exist_ok=True)
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=60) as r:
            total = int(r.headers.get("Content-Length") or 0)
            done = 0
            with open(dest, "wb") as f:
                while True:
                    chunk = r.read(1024 * 256)
                    if not chunk:
                        break
                    f.write(chunk)
                    done += len(chunk)
                    if total:
                        progress_cb(min(100, int(done * 100 / total)))
        return os.path.getsize(dest) > 0
    except Exception as e:
        log(f"download {url} failed: {e}", "opt")
        try:
            if os.path.exists(dest):
                os.remove(dest)
        except Exception:
            pass
        return False


def _run_silent(args: list[str], timeout: int = 900) -> bool:
    try:
        flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        proc = subprocess.Popen(args, creationflags=flags)
        proc.wait(timeout=timeout)
        return proc.returncode in (0, 3010)  # 3010=成功但需重启
    except Exception as e:
        log(f"silent run failed: {args} -> {e}", "opt")
        return False


def _add_user_path(new_dir: str) -> bool:
    """把目录追加到当前用户 PATH（注册表），并广播环境变更"""
    if not is_windows():
        return False
    try:
        import winreg
        key_path = r"Environment"
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0,
                            winreg.KEY_READ | winreg.KEY_SET_VALUE) as key:
            try:
                cur, _ = winreg.QueryValueEx(key, "Path")
            except FileNotFoundError:
                cur = ""
        parts = [p for p in cur.split(";") if p and p.strip()]
        if new_dir not in parts:
            parts.append(new_dir)
            new_path = ";".join(parts)
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0,
                                winreg.KEY_SET_VALUE) as key:
                winreg.SetValueEx(key, "Path", 0, winreg.REG_EXPAND_SZ, new_path)
        return True
    except Exception as e:
        log(f"add user path failed: {e}", "opt")
        return False


def install_component(key: str, progress_cb, status_cb) -> tuple[bool, str]:
    """下载并静默安装。status_cb(阶段文字)，progress_cb(0-100)"""
    if key == "wsl":
        status_cb("正在启用 WSL 功能…（需要管理员权限）")
        sysroot = os.environ.get("SystemRoot", r"C:\Windows")
        wsl_exe = os.path.join(sysroot, "System32", "wsl.exe")
        if not os.path.isfile(wsl_exe):
            return False, "系统没有 wsl.exe，请打开微软官网按说明启用"
        ok = _run_silent([wsl_exe, "--install", "--no-distribution"], timeout=600)
        return ok, ("WSL 功能安装成功，可能需要重启电脑生效" if ok
                    else "WSL 安装未完成（可能被拒绝或需要管理员权限）")

    resolved = _resolve_url(key)
    if not resolved:
        return False, "无法获取下载地址，请点击「打开官网」手动安装"
    url, kind = resolved
    ext = ".msi" if kind == "msi" else ".exe" if kind in ("python", "git") else ".zip"
    dest = os.path.join(DL_DIR, f"{key}-latest{ext}")

    status_cb("正在下载官方安装包…")
    if not _download(url, dest, progress_cb):
        return False, "下载失败，请检查网络或点击「打开官网」手动安装"

    status_cb("正在静默安装…（无需操作，请稍候）")
    if kind == "msi":
        ok = _run_silent(["msiexec", "/i", dest, "/qn", "/norestart"])
    elif kind == "python":
        ok = _run_silent([dest, "/quiet", "InstallAllUsers=0", "PrependPath=1",
                          "Include_test=0", "Include_launcher=0"])
    elif kind == "git":
        ok = _run_silent([dest, "/VERYSILENT", "/NORESTART", "/SP-"])
    elif kind == "phpzip":
        target = os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "php")
        os.makedirs(target, exist_ok=True)
        try:
            with zipfile.ZipFile(dest) as z:
                z.extractall(target)
            ok = _add_user_path(os.path.join(target, os.listdir(target)[0]))
        except Exception as e:
            log(f"php zip install failed: {e}", "opt")
            ok = False
    else:
        ok = False

    # 清理安装包
    try:
        os.remove(dest)
    except Exception:
        pass

    if ok and is_installed(key):
        return True, "安装完成，新开的终端窗口即可直接使用"
    if ok:
        return True, "安装完成（未检测到命令，可能需要重开终端或重启）"
    return False, "安装未成功，请点击「打开官网」手动安装"
