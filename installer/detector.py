# -*- coding: utf-8 -*-
"""环境检测：逐项检查这台电脑能不能装、缺什么"""
from __future__ import annotations

import os
import platform
import shutil
import socket
import sys
import time
from dataclasses import dataclass, field

from shared import paths
from shared.dsh_core import port_open, read_pid, process_alive

LEVEL_OK = "ok"
LEVEL_WARN = "warn"
LEVEL_FAIL = "fail"
LEVEL_INFO = "info"


@dataclass
class CheckResult:
    key: str                       # 唯一标识
    label: str                     # 显示名
    level: str                     # ok / warn / fail / info
    message: str                   # 一句大白话结论
    detail: str = ""               # 通俗解释 + 怎么办
    order: int = 0

    def to_ui(self) -> tuple[str, str, str]:
        return self.label, self.level, self.message


# ---------- 底层探测（平台相关，运行时才执行） ----------

def _os_ver_windows() -> tuple[str, int, int] | None:
    """返回 (版本名, major, build)；非 Windows 返回 None"""
    if not paths.is_windows():
        return None
    try:
        import ctypes
        class OSVERSIONINFOEXW(ctypes.Structure):
            _fields_ = [("dwOSVersionInfoSize", ctypes.c_ulong),
                        ("dwMajorVersion", ctypes.c_ulong),
                        ("dwMinorVersion", ctypes.c_ulong),
                        ("dwBuildNumber", ctypes.c_ulong),
                        ("dwPlatformId", ctypes.c_ulong),
                        ("szCSDVersion", ctypes.c_wchar * 128)]
        info = OSVERSIONINFOEXW()
        info.dwOSVersionInfoSize = ctypes.sizeof(OSVERSIONINFOEXW)
        try:
            fn = ctypes.windll.ntdll.RtlGetVersion
        except AttributeError:
            return None
        fn(ctypes.byref(info))
        major, build = info.dwMajorVersion, info.dwBuildNumber
        if major == 10 and build >= 22000:
            name = "Windows 11"
        elif major == 10:
            name = "Windows 10"
        elif major == 6 and info.dwMinorVersion == 1:
            name = "Windows 7"
        elif major == 6:
            name = "Windows 8/8.1"
        else:
            name = f"Windows (NT {major}.{info.dwMinorVersion})"
        return name, int(major), int(build)
    except Exception:
        return None


def _total_mem_mb() -> int:
    if paths.is_windows():
        try:
            import ctypes
            class MEMORYSTATUSEX(ctypes.Structure):
                _fields_ = [("dwLength", ctypes.c_ulong),
                            ("dwMemoryLoad", ctypes.c_ulong),
                            ("ullTotalPhys", ctypes.c_ulonglong),
                            ("ullAvailPhys", ctypes.c_ulonglong),
                            ("ullTotalPageFile", ctypes.c_ulonglong),
                            ("ullAvailPageFile", ctypes.c_ulonglong),
                            ("ullTotalVirtual", ctypes.c_ulonglong),
                            ("ullAvailVirtual", ctypes.c_ulonglong),
                            ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]
            ms = MEMORYSTATUSEX()
            ms.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
            if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(ms)):
                return int(ms.ullTotalPhys // (1024 * 1024))
        except Exception:
            pass
        return 0
    try:
        with open("/proc/meminfo") as f:
            for line in f:
                if line.startswith("MemTotal"):
                    return int(line.split()[1]) // 1024
    except Exception:
        pass
    return 0


def _free_disk_mb(target_dir: str) -> int:
    try:
        if not os.path.isdir(target_dir):
            os.makedirs(target_dir, exist_ok=True)
        return shutil.disk_usage(target_dir).free // (1024 * 1024)
    except Exception:
        return 0


def _browser_detect() -> list[str]:
    """检测常见浏览器可执行文件"""
    found: list[str] = []
    if not paths.is_windows():
        for name in ("google-chrome", "chromium", "firefox", "microsoft-edge"):
            if shutil.which(name):
                found.append(name)
        return found
    pf = os.environ.get("ProgramFiles", r"C:\Program Files")
    pf86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
    candidates = {
        "Edge": [os.path.join(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
                 os.path.join(pf86, "Microsoft", "Edge", "Application", "msedge.exe")],
        "Chrome": [os.path.join(pf, "Google", "Chrome", "Application", "chrome.exe"),
                   os.path.join(pf86, "Google", "Chrome", "Application", "chrome.exe")],
        "Firefox": [os.path.join(pf, "Mozilla Firefox", "firefox.exe"),
                    os.path.join(pf86, "Mozilla Firefox", "firefox.exe")],
        "360安全浏览器": [os.path.join(pf, "360", "360se6", "Application", "360se.exe")],
        "QQ浏览器": [os.path.join(pf, "Tencent", "QQBrowser", "QQBrowser.exe")],
    }
    for name, cands in candidates.items():
        for c in cands:
            if os.path.isfile(c):
                found.append(name)
                break
    return found


def _path_node() -> str | None:
    return shutil.which("node")


def _check_network(timeout: float = 4.0) -> bool:
    """探测外网连通（能连上 DeepSeek API 域名即可）"""
    try:
        socket.setdefaulttimeout(timeout)
        s = socket.create_connection(("api.deepseek.com", 443), timeout=timeout)
        s.close()
        return True
    except OSError:
        return False


# ---------- 检测主入口 ----------

def run_all() -> list[CheckResult]:
    results: list[CheckResult] = []
    add = results.append

    # 1. 系统位数
    machine = platform.machine().lower()
    is_64 = machine in ("amd64", "x86_64", "arm64", "aarch64")
    if paths.is_windows() and machine == "arm64":
        add(CheckResult("arch", "系统位数", LEVEL_WARN,
                        "检测到 ARM64 处理器，支持不完整",
                        "本安装包为 64 位 (x64) 版本。若您的电脑是 ARM 芯片，请使用官方源码方式安装。", 1))
    elif is_64:
        add(CheckResult("arch", "系统位数", LEVEL_OK, "64 位系统 ✓",
                        "安装包需要 64 位 Windows，您的电脑符合要求。", 1))
    else:
        add(CheckResult("arch", "系统位数", LEVEL_FAIL, "32 位系统，无法安装",
                        "DeepSeek Harness 需要 64 位 Windows。32 位电脑无法运行本工具。", 1))

    # 2. Windows 版本
    ver = _os_ver_windows()
    if ver is None:
        add(CheckResult("os", "操作系统", LEVEL_INFO, f"正在运行 {platform.system()} {platform.release()}",
                        "本安装包面向 Windows，检测结果仅供参考。", 2))
    else:
        name, major, build = ver
        if major >= 10:
            add(CheckResult("os", "操作系统", LEVEL_OK, f"{name} (内部版本 {build}) ✓",
                            "DeepSeek Harness 支持 Windows 10 及更新版本，您的系统符合要求。", 2))
        elif major == 6 and build <= 7601:
            add(CheckResult("os", "操作系统", LEVEL_FAIL, f"{name} 太旧，无法安装",
                            "DeepSeek Harness 需要 Windows 10 或 11（内置运行环境已不支持 Win7）。建议升级系统，或使用云服务器。", 2))
        else:
            add(CheckResult("os", "操作系统", LEVEL_WARN, f"{name}，可用性不保证",
                            "系统版本较旧，安装可以尝试，但官方不保证支持。", 2))

    # 3. 内存
    mem = _total_mem_mb()
    if mem == 0:
        add(CheckResult("mem", "内存", LEVEL_WARN, "无法检测内存大小",
                        "跳过检测不影响安装，但建议 4GB 以上内存。", 3))
    elif mem >= paths.MIN_MEM_MB:
        add(CheckResult("mem", "内存", LEVEL_OK, f"{mem//1024} GB 内存 ✓",
                        "内存充足，可以流畅运行。", 3))
    else:
        add(CheckResult("mem", "内存", LEVEL_WARN, f"内存仅 {mem//1024} GB",
                        "运行 DeepSeek Harness 建议 4GB 以上内存。内存不足时界面可能较慢，但一般仍能使用。", 3))

    # 4. 磁盘
    disk = _free_disk_mb(paths.install_root())
    if disk == 0:
        add(CheckResult("disk", "磁盘空间", LEVEL_WARN, "无法检测磁盘空间",
                        "安装需要约 1.5GB 可用空间。", 4))
    elif disk >= paths.MIN_DISK_MB:
        add(CheckResult("disk", "磁盘空间", LEVEL_OK, f"可用 {disk//1024} GB ✓",
                        "安装全部文件需要约 1.5GB，空间充足。", 4))
    else:
        add(CheckResult("disk", "磁盘空间", LEVEL_FAIL, f"可用空间不足（仅 {disk//1024} GB）",
                        "安装需要约 1.5GB 可用空间，请清理磁盘后再安装。", 4))

    # 5. 端口 3080
    if port_open():
        pid = read_pid()
        if pid and process_alive(pid):
            add(CheckResult("port", "端口 3080", LEVEL_INFO, "检测到已安装的实例正在运行",
                            "您的电脑已装有 DeepSeek Harness 且正在运行。可以继续，安装会覆盖更新程序文件。", 5))
        else:
            add(CheckResult("port", "端口 3080", LEVEL_WARN, "端口 3080 被其他程序占用",
                            "DeepSeek Harness 需要用到 3080 端口，当前被其他程序占用。安装可以继续，但启动时可能失败，请先关闭占用它的程序。", 5))
    else:
        add(CheckResult("port", "端口 3080", LEVEL_OK, "端口空闲 ✓",
                        "DeepSeek Harness 的界面端口（3080）未被占用。", 5))

    # 6. 浏览器
    browsers = _browser_detect()
    if browsers:
        add(CheckResult("browser", "浏览器", LEVEL_OK, f"检测到：{'、'.join(browsers[:3])} ✓",
                        "DeepSeek Harness 的界面在浏览器里打开，您有可用的浏览器。", 6))
    else:
        add(CheckResult("browser", "浏览器", LEVEL_WARN, "未找到常见浏览器",
                        "DeepSeek Harness 的界面需要浏览器打开。如果没有浏览器，安装后可复制网址 http://127.0.0.1:3080 到能访问的设备上（仅限本机）。", 6))

    # 7. 已有 Node
    nd = _path_node()
    if nd:
        add(CheckResult("node", "电脑已有 Node.js", LEVEL_INFO, f"已安装：{nd}",
                        "您电脑已装有 Node.js。本安装包会使用自带的内置运行环境，保证版本一致，互不影响。", 7))
    else:
        add(CheckResult("node", "电脑已有 Node.js", LEVEL_OK, "无需另外安装 ✓",
                        "安装包已内置完整的运行环境，您不需要自己安装任何东西。", 7))

    # 8. 已安装检测
    if os.path.exists(paths.install_json()):
        try:
            import json
            with open(paths.install_json(), "r", encoding="utf-8") as f:
                data = json.load(f)
            ver = data.get("dsh_version", "?")
            add(CheckResult("installed", "已有安装", LEVEL_INFO, f"已安装过（版本 {ver}）",
                            "本安装包会重新校验并覆盖程序文件，放心继续。", 8))
        except Exception:
            add(CheckResult("installed", "已有安装", LEVEL_INFO, "检测到安装记录",
                            "本安装包会重新校验并覆盖程序文件，放心继续。", 8))
    else:
        add(CheckResult("installed", "已有安装", LEVEL_OK, "全新安装 ✓",
                        "这是一次全新安装。", 8))

    # 9. 网络
    try:
        net_ok = _check_network()
    except Exception:
        net_ok = False
    if net_ok:
        add(CheckResult("net", "网络", LEVEL_OK, "可以联网 ✓",
                        "安装过程不需要网络（所有文件已内置），使用 AI 功能时需要联网。", 9))
    else:
        add(CheckResult("net", "网络", LEVEL_WARN, "暂时检测不到外网",
                        "安装过程不需要网络（所有文件已内置），可正常安装。但使用 AI 对话功能时需要联网，请留意。", 9))

    results.sort(key=lambda r: r.order)
    return results


def summary(results: list[CheckResult]) -> tuple[str, str]:
    """返回 (等级, 一句话总结)"""
    fails = [r for r in results if r.level == LEVEL_FAIL]
    warns = [r for r in results if r.level == LEVEL_WARN]
    if fails:
        return "fail", f"发现 {len(fails)} 个需要处理的问题，处理后可继续安装"
    if warns:
        return "warn", f"有 {len(warns)} 个小提示（不阻塞安装），可以继续"
    return "ok", "环境检查全部通过，可以放心安装"


def check_bundle_files() -> list[CheckResult]:
    """检查安装包自带的离线资源是否齐全（node.zip / dsh.zip / switch.zip）"""
    results: list[CheckResult] = []
    need = ["node.zip", "dsh.zip", "switch.zip"]
    for name in need:
        p = paths.bundle_file(name)
        if os.path.isfile(p):
            sz = os.path.getsize(p) / (1024 * 1024)
            results.append(CheckResult("bundle_" + name, f"离线资源 {name}", LEVEL_OK,
                                       f"已就绪（{sz:.1f} MB）", "", 20))
        else:
            results.append(CheckResult("bundle_" + name, f"离线资源 {name}", LEVEL_FAIL,
                                       "缺失！", f"安装包缺少 {name}，请重新下载完整安装包。", 20))
    return results
