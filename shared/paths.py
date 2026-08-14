# -*- coding: utf-8 -*-
"""路径与全局常量（两个 exe 共用）"""
import os
import sys

APP_NAME = "DeepSeek Harness"
APP_SHORT = "dsh"
APP_KEY = "DeepSeekHarness"
TOOL_VERSION = "1.0.0"
PORT = 3080
WEB_URL = "http://127.0.0.1:3080"

# 检测与安装硬性要求
MIN_MEM_MB = 4096
MIN_DISK_MB = 1500


def is_windows() -> bool:
    return os.name == "nt"


def local_appdata() -> str:
    """%LOCALAPPDATA%，缺失时退回用户目录"""
    la = os.environ.get("LOCALAPPDATA")
    if la:
        return la
    if is_windows():
        return os.path.join(os.path.expanduser("~"), "AppData", "Local")
    return os.path.expanduser("~")


def install_root() -> str:
    """程序安装根目录（无需管理员权限）"""
    return os.path.join(local_appdata(), APP_KEY)


def runtime_node_dir() -> str:
    return os.path.join(install_root(), "runtime", "node")


def node_exe() -> str:
    return os.path.join(runtime_node_dir(), "node.exe")


def runtime_app_dir() -> str:
    return os.path.join(install_root(), "runtime", "app")


def dsh_entry() -> str:
    return os.path.join(runtime_app_dir(), "node_modules",
                        "@deepseek-ai", "dsh", "lib", "bin.js")


def switch_dir() -> str:
    return os.path.join(install_root(), "switch")


def switch_exe() -> str:
    return os.path.join(switch_dir(), "switch.exe")


def log_dir() -> str:
    return os.path.join(install_root(), "logs")


def dsh_log_file() -> str:
    return os.path.join(log_dir(), "dsh.log")


def switch_log_file() -> str:
    return os.path.join(log_dir(), "switch.log")


def pid_file() -> str:
    return os.path.join(install_root(), "runtime", "dsh.pid")


def install_json() -> str:
    return os.path.join(install_root(), "install.json")


def desktop_dir() -> str:
    if is_windows():
        import ctypes
        try:
            buf = ctypes.create_unicode_buffer(512)
            ctypes.windll.shell32.SHGetFolderPathW(None, 0x0000, None, 0, buf)
            if buf.value:
                return buf.value
        except Exception:
            pass
        return os.path.join(os.path.expanduser("~"), "Desktop")
    return os.path.expanduser("~")


def start_menu_dir() -> str:
    """开始菜单程序目录（用户级）"""
    if is_windows():
        import ctypes
        try:
            buf = ctypes.create_unicode_buffer(512)
            # CSIDL_PROGRAMS = 0x0002
            ctypes.windll.shell32.SHGetFolderPathW(None, 0x0002, None, 0, buf)
            if buf.value:
                return os.path.join(buf.value, APP_NAME)
        except Exception:
            pass
    return os.path.join(local_appdata(), "Programs", APP_NAME)


def bundle_dir() -> str:
    """安装器自带的离线资源目录（PyInstaller 兼容定位）"""
    if getattr(sys, "frozen", False):
        base = os.path.dirname(sys.executable)
    else:
        base = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    return os.path.join(base, "_assets")


def bundle_file(name: str) -> str:
    return os.path.join(bundle_dir(), name)


def log(msg: str, tag: str = "dsh", to: str | None = None):
    """写日志（文件 + 时间戳）"""
    try:
        import time
        line = f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] [{tag}] {msg}"
        path = to or switch_log_file()
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except Exception:
        pass
