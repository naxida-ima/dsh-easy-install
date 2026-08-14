# -*- coding: utf-8 -*-
"""DeepSeek Harness 服务管理核心：启动 / 停止 / 状态检测 / PID 管理
Windows 专用逻辑全部延迟到运行时，Linux 可安全导入用于测试。
"""
import os
import re
import socket
import subprocess
import sys
import time

from .paths import (dsh_entry, dsh_log_file, install_root, is_windows,
                    log, node_exe, pid_file, PORT, runtime_app_dir, runtime_node_dir)

START_TIMEOUT = 45     # 启动等待端口时间（秒）
STOP_TIMEOUT = 15      # 停止等待端口关闭时间（秒）


def port_open(host: str = "127.0.0.1", port: int = PORT, timeout: float = 0.7) -> bool:
    """检测端口是否可连接（=服务在监听）"""
    try:
        s = socket.create_connection((host, port), timeout=timeout)
        s.close()
        return True
    except OSError:
        return False


def get_status() -> str:
    """running / stopped"""
    return "running" if port_open() else "stopped"


# ---------- PID 管理 ----------

def read_pid() -> int | None:
    try:
        if os.path.exists(pid_file()):
            with open(pid_file(), "r", encoding="utf-8") as f:
                return int(f.read().strip())
    except Exception:
        pass
    return None


def write_pid(pid: int):
    try:
        os.makedirs(os.path.dirname(pid_file()), exist_ok=True)
        with open(pid_file(), "w", encoding="utf-8") as f:
            f.write(str(pid))
    except Exception as e:
        log(f"write_pid failed: {e}", "core")


def clear_pid():
    try:
        if os.path.exists(pid_file()):
            os.remove(pid_file())
    except Exception:
        pass


# ---------- 进程操作 ----------

def process_alive(pid: int) -> bool:
    if not is_windows():
        try:
            os.kill(pid, 0)
            return True
        except OSError:
            return False
    try:
        import ctypes
        PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
        h = ctypes.windll.kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, int(pid))
        if not h:
            return False
        code = ctypes.c_ulong()
        ok = ctypes.windll.kernel32.GetExitCodeProcess(h, ctypes.byref(code))
        ctypes.windll.kernel32.CloseHandle(h)
        return bool(ok) and code.value == 259  # STILL_ACTIVE
    except Exception:
        return False


def find_pid_by_port(port: int = PORT) -> int | None:
    """netstat 找占用端口的 PID（零外部依赖）"""
    if not is_windows():
        return None
    try:
        out = subprocess.run(
            ["netstat", "-ano"], capture_output=True, text=True,
            timeout=10, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)
        ).stdout
        pat = re.compile(r"TCP\s+.*:{}\s+.*LISTENING\s+(\d+)".format(port))
        for line in out.splitlines():
            m = pat.search(line)
            if m:
                pid = int(m.group(1))
                if pid != 0:
                    return pid
    except Exception as e:
        log(f"find_pid_by_port failed: {e}", "core")
    return None


def taskkill(pid: int) -> bool:
    if not is_windows():
        return False
    try:
        subprocess.run(
            ["taskkill", "/PID", str(pid), "/T", "/F"],
            capture_output=True, timeout=15,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)
        )
        return True
    except Exception as e:
        log(f"taskkill failed: {e}", "core")
        return False


# ---------- 服务启停 ----------

def _base_env() -> dict:
    env = dict(os.environ)
    env["PATH"] = runtime_node_dir() + os.pathsep + env.get("PATH", "")
    return env


def start_service(wait: bool = True) -> tuple[bool, str]:
    """启动 dsh web 服务。返回 (是否成功, 提示语)"""
    if port_open():
        return True, "服务已在运行"

    if not os.path.exists(node_exe()):
        return False, "未找到 Node 运行时，请先运行安装程序"
    if not os.path.exists(dsh_entry()):
        return False, "未找到 DeepSeek Harness 程序文件，请先运行安装程序"

    log(f"start_service: node={node_exe()}", "core")
    log(f"start_service: entry={dsh_entry()}", "core")
    try:
        os.makedirs(os.path.dirname(dsh_log_file()), exist_ok=True)
        logf = open(dsh_log_file(), "a", encoding="utf-8", errors="replace")
        flags = 0
        if is_windows():
            flags = (getattr(subprocess, "CREATE_NO_WINDOW", 0)
                     | getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0))
        proc = subprocess.Popen(
            [node_exe(), dsh_entry(), "web"],
            cwd=runtime_app_dir(),
            env=_base_env(),
            stdout=logf,
            stderr=subprocess.STDOUT,
            creationflags=flags,
        )
        write_pid(proc.pid)
        log(f"start_service: launched pid={proc.pid}", "core")
    except Exception as e:
        log(f"start_service error: {e}", "core")
        return False, f"启动失败：{e}"

    if not wait:
        return True, "启动中…"

    # 轮询端口就绪
    deadline = time.time() + START_TIMEOUT
    while time.time() < deadline:
        if port_open():
            log("start_service: port ready", "core")
            return True, "服务已启动"
        time.sleep(0.5)
    log("start_service: timeout waiting port", "core")
    return False, "启动超时，请查看日志"


def stop_service(wait: bool = True) -> tuple[bool, str]:
    """停止 dsh 服务"""
    if not port_open():
        clear_pid()
        return True, "服务已停止"

    pid = read_pid()
    killed = False
    if pid and process_alive(pid):
        killed = taskkill(pid)
        log(f"stop_service: taskkill pid={pid} -> {killed}", "core")
    if not killed:
        by_port = find_pid_by_port()
        if by_port:
            killed = taskkill(by_port)
            log(f"stop_service: taskkill by-port pid={by_port} -> {killed}", "core")

    if wait:
        deadline = time.time() + STOP_TIMEOUT
        while time.time() < deadline:
            if not port_open():
                break
            time.sleep(0.4)
    clear_pid()
    if port_open():
        return False, "未能完全停止（端口仍占用），请关闭占用程序后重试"
    return True, "服务已停止"


# ---------- 自检（供安装向导最后验证） ----------

def self_check() -> dict:
    """安装完成后自检：返回各项关键路径与状态"""
    result = {
        "node_exe": node_exe(),
        "node_ok": os.path.exists(node_exe()),
        "entry": dsh_entry(),
        "entry_ok": os.path.exists(dsh_entry()),
        "runtime_app": runtime_app_dir(),
        "app_ok": os.path.isdir(runtime_app_dir()),
        "port": PORT,
        "status": get_status(),
        "install_root": install_root(),
    }
    if result["node_ok"]:
        try:
            out = subprocess.run(
                [node_exe(), "--version"], capture_output=True, text=True,
                timeout=10, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)
            ).stdout.strip()
            result["node_version"] = out
        except Exception:
            result["node_version"] = "unknown"
    return result
